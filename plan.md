# WorldWeaver 总体规划

**日期：2026-05-30**
**版本：1.0**

---

## 0. 序

本文档记录 WorldWeaver 从单机沙盒到联机世界的关键技术决策与施工路线。

> 这是一个伟大的进程诞生的过程。
> —— Yabuki77

---

## 1. 核心架构决策（已确定）

### 1.1 运行时模式（Profile）

| Profile | Godot 模式 | 有渲染 | 有节点树 | 入口 |
|---------|-----------|--------|---------|------|
| `Standalone` | 普通 | ✅ | GameManager→Save→World→Layer→MapVisualLayer | standalone.tscn |
| `Client` | 普通 | ✅ | GameManager→Save→World→Layer→MapVisualLayer | client.tscn |
| `Server` | headless | ❌ | GameManager→Save→World→Layer（无 MapVisualLayer）| server.tscn |

### 1.2 项目结构

```
WorldWeaver.sln
├── WorldWeaver.Core/          ← class library，纯 C#，**不引用 Godot**
├── WorldWeaver.Client/        ← Godot 项目，有画面有声音，引用 Core
└── WorldWeaver.Server/        ← headless Godot，无渲染，引用 Core
```

Core 为共享模拟层（纯 C# 逻辑）。Client 和 Server 各自引用 Core，并在自己的项目里编写 Godot Node 包装类来桥接 Core 逻辑与 Godot 运行时。**Client 和 Server 互不引用。**

> **Core 不允许引用 Godot。** 原因：客户端与服务端在节点树层面的需求截然不同——客户端需要 TileMapLayer、Camera2D、Sprite2D，服务端需要 PhysicsServer2D RID。把两类 Node 写进 Core 只会产生耦合。正确做法是：纯 C# 逻辑（状态机、数据操作、网络协议）放 Core，各端自己写对应的 Node 包装类。
>
> 内存布局一致的值类型对（如自建 `IntVector2` ↔ Godot `Vector2I`）在 Client/Server 的边界层通过 `Unsafe.As` 零开销 reinterpret，Core 本身不感知 Godot。

### 1.3 客户端设计决策

**客户端没有 ChunkManager、没有状态机、没有 ChunkData 字典。**
`TileMapLayer.SetCell` 和 `GetCellTileData` 是客户端唯一的 tile 数据读写入口。

服务端发什么 → 客户端画什么。TileMapLayer 自带的内部字典就是数据。

### 1.4 服务端设计决策

服务端是 **headless Godot**，保留 `GameManager → Save → World → Layer` 节点树。理由：

- 物理引擎（Jolt Physics via PhysicsServer2D）需要 Godot runtime
- Godot 节点树自带递归生命周期，Save/World/Layer 通过父节点驱动，不需要手写初始化顺序
- 物理体使用 `PhysicsServer2D` RID 创建，不挂 `CharacterBody2D` 节点到场景树

> ⚠️ **Headless 验证前置**：阶段 1 完成 MapCore 抽离后，立即做一个最小 headless 验证——启动 Godot `--headless`，加载 MapCore，跑一轮 tick 循环。不等到阶段 5 项目拆分后才确认 headless Godot 的稳定性与资源开销。

### 1.5 网络拓扑

**C/S。** 服务端独立进程。客户端本地启动时自动拉起服务端进程并连接 127.0.0.1。

两个通道：地图数据走可靠有序（TCP/ENet reliable），实体位置走不可靠（UDP/ENet unreliable）。

### 1.6 协议即契约

Core 不引用 Godot，意味着 Core 定义的数据结构（消息体、坐标、Tile 数据）与传输协议一起构成**唯一真相源**。任何人可以用任何语言实现自己的服务端——只要它遵循同一套网络协议。

- **官方服务端**：引用 Core，直接使用 Core 中的类
- **第三方服务端**（Rust、Go、Node.js 等）：按协议定义自行实现序列化/反序列化
- **官方客户端**：引用 Core + Godot，用 Node 包装类桥接 Core 逻辑与渲染

这与 Web 领域的前后端分离是同一模式：Core = 后端逻辑（纯 C#），Client = 前端（Godot 渲染 + Core），Server = 后端运行时（headless Godot + Core）。区别在于游戏是长连接实时通信，而 Web 是短连接请求-响应。独立开发者也能驾驭这种分层——它只是把"一个项目里的文件夹"变成了"多个项目"。

---

## 2. 地图系统

### 2.1 区块数据版本机制（✅ 已设计）

每个 Chunk 维护版本历史链。单次版本条目：

```csharp
public sealed class ChunkVersionEntry
{
    public long Version { get; init; }           // Counter 分配，per-Chunk 单调递增
    public ulong FullHash { get; init; }          // XXH64，完整 ChunkData 的 hash 指纹
    public ITileValueShape Delta { get; init; }   // 本次修改的 PreviousValues（回退用）
    public long EntityId { get; init; }           // 更改发起实体（0=世界本身）
    public DateTime Timestamp { get; init; }      // 创建时间
    public string Tag { get; init; }              // "player_dig" | "worldgen" | "explosion" | "rollback"
}
```

**同一套数据支撑八个场景：** P2P 分发验证、转发追踪、客户端缓存判断、磁盘完整性校验、精确回退、防熊查询、选择性撤销、审核日志。

### 2.2 TileValueShape 统一（✅ 已设计）

接口只暴露一个 Shape 基座和一个迭代器。调用方不关心值是统一的还是逐点的——它只管遍历。

```csharp
public interface IPixelValueShape<T>
{
    PixelShape Shape { get; }
    IEnumerator<(Vector2I coord, T value)> GetEnumerator();
}

public interface ITileValueShape : IPixelValueShape<int>
{
}
```

两种内部实现：

- **统一值**（如右键画深海）：不分配数组。迭代器每次 `MoveNext` 返回同一个 `runId`。`TileUniform` 退化为这种模式。
- **逐点值**（如从 ChunkData 读出的混合区域）：内部持有 `int[]`，迭代器按索引取出。

### 2.3 IAuthority（✅ 已设计）

```csharp
public interface IAuthority
{
    AuthorityDecision Decide(TileModification modification, ChunkData targetData);
}
```

三种实现：`StandaloneAuthority`（始终接受，等价于当前单机行为）、`ServerAuthority`（始终接受 + 通知 Replication 广播）、`ClientReplica`（仅接受来自服务器的修改，预测另路管理）。

### 2.4 IChunkDataProvider（✅ 已设计）

```csharp
public interface IChunkDataProvider
{
    DataRequestResult TryAcquire(ChunkPosition pos, out ChunkDataStorage data);
    void RequestAsync(ChunkPosition pos);
}
```

四种实现：`DiskChunkDataProvider`（Region 文件）、`NetworkChunkDataProvider`（客户端网络收包）、`ProcGenChunkDataProvider`（程序化生成）、`PeerMeshChunkDataProvider`（P2P 分发 + hash 验证）。

### 2.5 P2P 区块分发（🟡 可选优化）

> 此项为可选优化，不在初期施工范围内。C/S 直传先跑通，后续按需叠加。

服务器下发 `{version, hash, peers[]}`，客户端从其他客户端拉取区块数据，本地算 XXH64 hash 对账。对了直接用，不对换人，没人对找服务器。服务器永远兜底。

---

## 3. 四层架构

```
Layer 4: Presentation  │ 仅客户端    │ MapVisualLayer, TileMapLayer, PlayerCamera
Layer 3: Replication   │ 两端都有    │ ServerReplicator / ClientReplicator, PredictionTracker
Layer 2: Simulation    │ 两端共享    │ MapCore, ChunkManager, IAuthority, ChunkData, 状态机
Layer 1: Data Provision│ 两端都有    │ IChunkDataProvider（Disk / Network / PeerMesh / ProcGen）
```

核心约束：Layer 2 尽量少直接依赖 Godot 静态 API，功能通过接口抽象（如 `ILogger` 替 `GD.Print`）。`Vector2I` 等值类型与自用 `IntVector2` 布局兼容，`Unsafe.As` 零开销桥接。

---

## 4. 实体系统（ECS 变体："组件列"模型）

### 4.1 数据结构

```
实体 (Entity)
  ├─ EntityId: long
  ├─ Tag: string
  ├─ State: Active | Dead | PendingDestroy
  ├─ ComponentMask: ulong              ← 位标记
  └─ ComponentRefs: Dict<Type, int>    ← "PositionComponents[3] 是我的位置"

组件全局列表（每组件类型一个紧凑数组）
  PositionComponents[]:
    [0] { X, Y, OwnerEntityId }
    [1] { X, Y, OwnerEntityId }
    ...
```

### 4.2 操作规则

- **增**：数组末尾追加，更新实体的 ComponentRefs + ComponentMask
- **删**：swap-pop。将数组最后一位覆盖被删位置 → 被移动组件的 OwnerEntity 的 ComponentRefs 回写新索引 → Pop 末尾
- **查**：System 遍历目标组件列表，通过 OwnerEntityId 回查实体，再通过实体 ComponentRefs 获取关联组件
- **清理**：帧末统一回收 Dead 实体的所有组件（逐个 swap-pop），最后回收 EntityId

### 4.3 TileModifierComponent

实体修改地图的通道：

```csharp
public struct TileModifierComponent
{
    public long OwnerEntityId;
    public TileModification ActiveOperation;  // System 处理完清空
    public float OperationRadius;
    public int MaxOperationsPerTick;
}
```

`TileModifierSystem` 遍历所有 `TileModifierComponent`，存在 `ActiveOperation` 时调用 `MapCore.ApplyModification(operation, entityId)` 后清空。

---

## 5. 预测与回滚（🟡 可选优化）

> 客户端预测与回滚为可选优化，不在初期施工范围内。初期采用悲观更新（服务端确认后才渲染）。网络层稳定后再叠加。

### 5.1 PredictionTracker

```csharp
public sealed class PredictionTracker
{
    // seq → (ChunkPosition, ITileValueShape previousValues)
    private readonly Dictionary<long, (ChunkPosition, ITileValueShape)> _pending;

    public void Track(long seq, ChunkPosition pos, ITileValueShape previousValues);
    public ReconcileResult Reconcile(long seq, ITileValueShape serverResult);
    public void InvalidateChunk(ChunkPosition pos);  // 区块被卸载时清理
}
```

### 5.2 时序

```
客户端右键 → 预测渲染(0ms) → 发请求 → 等待确认
                                     ↓
                              服务器确认到达
                              → Matched: 无事发生
                              → Mismatched: 用 PreviousValues 回滚 TileMapLayer
                              → Rejected: 用 PreviousValues 回滚 TileMapLayer
```

### 5.3 乱序保护

`ClientReplicator` 维护 `_lastAppliedSeq[tileKey]`。收到的 seq 小于已应用的直接丢弃。

---

## 6. 网络消息

### 6.1 消息类型

| 方向 | 消息 | 通道 | 说明 |
|------|------|------|------|
| Client→Server | `TileModificationRequest` | reliable | seq, TileUniform, PlayerId |
| Server→All | `TileModificationConfirm` | reliable | seq, ITileValueShape result |
| Server→Client | `TileModificationReject` | reliable | seq, reason |
| Server→Client | `ChunkDataAnnounce` | reliable | version, hash, peers[] |
| Client→Client | `ChunkDataRequest` | reliable | chunkPos, targetVersion |
| Client→Client | `ChunkDataResponse` | reliable | compressed tile data |
| Server→Client | `ChunkUnload` | reliable | chunkPos |
| Client→Server | `ChunkAck` | reliable | "我已拥有 chunkPos v=N" |

### 6.2 序列化

优先 MessagePack（紧凑 + C# source generator），备选 Protobuf。

---

## 7. 施工路线

### 阶段 1：抽离模拟层 + Headless 验证 [可单机验证]

- [ ] 创建 `MapCore`（纯 C#，不引用 Godot），将 ChunkManager 持有权和 Update 循环从 `MapLayer : Node` 移到 `MapCore`
- [ ] `MapLayer` 退化为薄壳：持有 `MapCore`，_PhysicsProcess 调用 `MapCore.Tick()`
- [ ] Core 中功能抽象：`GD.Print/PushError` → `ILogger` 接口注入；自建 `IntVector2` 替代 `Vector2I`，Client/Server 边界层通过 `Unsafe.As` 零开销桥接
- [ ] **Headless 最小验证**：启动 Godot `--headless`，加载 MapCore，跑一轮 tick 循环，确认 headless Godot 的稳定性与资源开销

### 阶段 2：抽象数据源与权限 [可单机验证]

- [ ] `IChunkDataProvider` + `DiskChunkDataProvider` + `ProcGenChunkDataProvider`
- [ ] 改造 `ReadingInformationHandler`，用 `DataProvider.TryAcquire` 替换 `PersistenceCache.TryTakeOut`
- [ ] `IAuthority` + `StandaloneAuthority`
- [ ] `ChunkDataOperator` 重构为 `ModifyTiles(TileModification)`，写入前过 `IAuthority.Decide`

### 阶段 3：TileValueShape 统一 [可单机验证]

- [ ] `IPixelValueShape<T>` + `ITileValueShape`
- [ ] `UniformTileValueShape` + `PerPointTileValueShape`
- [ ] 现有 `TileUniform` 和 `TileValuesArrayShape` 迁移到新接口

### 阶段 4：区块版本机制 [可单机验证]

- [ ] `ChunkVersionEntry`（Version, FullHash, Delta, EntityId, Timestamp, Tag）
- [ ] `ChunkVersionHistory`：内存保留 N 个版本，磁盘保留最新版
- [ ] 版本淘汰策略：数量上限 + 时间冷淘汰
- [ ] ChunkData 修改时自动记录版本条目
- [ ] XXH64 增量 hash 计算

### 阶段 5：项目拆分

- [ ] 创建 `WorldWeaver.Core` class library
- [ ] 将 Core 层代码移入
- [ ] 创建 `WorldWeaver.Server` headless Godot 项目
- [ ] 创建 `WorldWeaver.Client` Godot 项目
- [ ] `ServerRunner.cs`：服务端入口 Node

### 阶段 6：网络 System

- [ ] `NetworkSystem : IGlobalSystem`，message 序列化/反序列化、通道管理
- [ ] `GameProfile` 枚举 + `GlobalSystemAttribute.Profiles`，按 Profile 选择性注册 System
- [ ] `ServerReplicationSystem`：订阅 ChunkManager 事件 → 广播
- [ ] `ClientReplicationSystem`：收包 → `ModifyTiles(IsPrediction=false)`
- [ ] `NetworkChunkDataProvider`：客户端用，TryAcquire 无数据时返回 RetryLater

### 阶段 7：预测与回滚 + P2P 分发（🟡 可选优化）

> 此阶段为可选优化。初期采用悲观更新（服务端确认后渲染），P2P 由 C/S 直传替代。

- [ ] `PredictionTracker`：Track / Reconcile / InvalidateChunk
- [ ] `ClientReplica : IAuthority`：区分预测写入和权威写入
- [ ] 序列号守卫处理网络乱序
- [ ] `PeerMeshChunkDataProvider`：hash 验证 + peer 兜底

### 阶段 8：实体系统

- [ ] Entity + ComponentMask + ComponentRefs
- [ ] 组件全局列表（增 → append，删 → swap-pop + 回写 OwnerEntity ref）
- [ ] 帧末 Dead 实体统一回收
- [ ] `TileModifierComponent` + `TileModifierSystem`

### 阶段 9：存档系统

- [ ] `SaveManager : IGlobalSystem`
- [ ] `SaveSystemGroup`：存档级 System 容器（Kahn 拓扑排序，复用现有容器管线）
- [ ] `Save : Node`：持有存档元数据 + World 列表
- [ ] `World : Node`：持有 Layer 列表
- [ ] 存档级事件：`SaveSystemRegistering` / `SaveSystemsInitialized` / `SaveReady` / `SaveUnloading`

### 阶段 10：Mod 系统完善

- [ ] `GlobalSystemAttribute.Profiles`：`[Flags]` 枚举 `Server` / `Client` / `All = Server | Client`（默认 `All`）
- [ ] Mod 三种分发形态：仅客户端（`Client`，如 UI 美化）、仅服务端（`Server`，如反作弊）、双端（`All`/默认，如新增方块类型）
- [ ] 单机模式（`Server | Client` 都在跑）自然覆盖了 `Client` 和 `Server` 的 System
- [ ] ModManager 扫描 mods/{mod}/ → 按依赖排序 → 加载 DLL → 反射扫描带 `[GlobalSystem]` 的类，按 `Profiles` + 当前进程角色过滤注册
- [ ] Mod 入口类分为两种：`IGlobalSystem`（游戏启动即入 `GlobalSystemManager`）和 `ISaveSystem`（存档加载后入 `SaveSystemGroup`）。同一 Mod 可同时包含两者
- [ ] 同一套 `[GlobalSystem]` 特性发现，同一个 Kahn 拓扑排序管线，入口时机 + Profile 两层过滤
- [ ] 香草 System 与模组 System 混合拓扑排序

---

## 8. 不变清单

以下现有模块在重构中保持不变或仅做接口适配：

| 模块 | 状态 |
|------|------|
| `ChunkStateMachine` — 状态图、路径预计算 | 不变 |
| `ChunkState` — 选路算法、阻塞表 | 不变 |
| `TileType` / `TileTypeManager` — JSON 配置驱动 | 不变 |
| `PixelShape` / `RectangleShape` / `PointsShape` / Shape 体系 | 不变 |
| `MapVisualLayer` — 事件驱动渲染 | 不变 |
| `Region 文件持久化` — 服务端继续用 | 不变 |
| `CounterCore` — 用于 Chunk 版本号和 EntityId 发放 | 不变 |
| `System 容器管线` — 声明表→拓扑排序→注册器 | 不变 |
| `ModManifest` — mod.json 反序列化 | 不变 |

---

## 9. 本方案生成基础

- `src/Systems/TODO.md` — 现有 System 架构与 TODO（2026-05-24）
- `src/Map/TODO.md` — 现有 Map 系统 TODO（2026-05-06）
- `src/PROJECT_STANDARD.md` — 项目编码规范 v1.3
- `src/CODE_STYLE.md` — 代码风格规定 v1.2
- `src/Map/README.md` — MapSystem 层级说明
- `src/Map/ChunkCore/README.md` — Chunk 状态机与驱动闭环
- 2026-05-29~30 联机架构讨论 — 四层架构、C/S、P2P 分发、ECS 变体、版本机制
- 2026-05-30 TileValueShape 统一、防熊回退讨论

---

> 一个伟大的进程。
> —— 既是项目推进的进程，也是服务端独立启动的进程。
