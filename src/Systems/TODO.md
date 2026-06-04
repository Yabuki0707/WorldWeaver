# WorldWeaver Systems — 架构与 TODO

**日期：2026-05-24**

---

## 1. 架构总览

```
GameManager (场景根 Node : IGameManager)
│   static Instance — 全局单例入口
│
├── ModManager : IModManager 🔲
│   └── 扫描 mods/{mod}/ 目录，加载 DLL、按依赖排序、反射发现 [GlobalSystem]
│
├── Systems: GlobalSystemManager : IGlobalSystemManager ✅
│   ├── SaveManager 🔲  ← 全局 System，管理存档生命周期（非 Node，入系统表）
│   └── ... (模组可注入)
│
└── 事件流
    ├── GlobalSystemRegistering   ← 容器广播，参数为 IGlobalSystemManager 自身
    └── GlobalSystemsInitialized  ← 容器广播，全部就绪
```

Godot 节点树结构：

```
GameManager              ← 场景根 Node，静态单例
  └── Save[] 🔲          ← 每个存档一个 Node 实例，由 SaveManager 动态创建/销毁
        ├── Systems: SaveSystemGroup 🔲 ← 存档级 System 容器（不入节点树）
        ├── World[] 🔲                 ← 存档内可切换多个世界
        │     └── Layer[] 🔲           ← 每个世界由多个图层组成
        └── 存档事件 (SaveReady / SaveUnloading)
```

---

## 2. System 层级

### 2.1 两层 System

|      | 全局 System                    | 存档级 System                 |
|------|------------------------------|----------------------------|
| 接口   | `IGlobalSystem` ✅            | `ISaveSystem` ✅            |
| 基接口  | `IGameSystem` ✅              | `IGameSystem` ✅            |
| 容器类  | `GlobalSystemManager` ✅      | `SaveSystemGroup` 🔲       |
| 容器接口 | `IGlobalSystemManager` ✅     | `ISaveSystemGroup` 🔲      |
| 属性   | `GameManager.Systems`        | `ISave.Systems`            |
| 生命周期 | 游戏进程绑定                       | 存档加载/卸载                    |
| 注册方式 | `GlobalSystemRegistering` 事件 | `SaveSystemRegistering` 事件 |

---

### 2.2 IGameSystem ✅

```csharp
// config/Systems/IGameSystem.cs
public interface IGameSystem
{
    string SystemName { get; }
    bool IsPrerequisitesGenerated { get; }
    ReadOnlyMemory<string> Prerequisites { get; }
    FrozenSet<string> PrerequisiteSet { get; }
    IGameSystem VisitSystem(string systemName);
    void Uninstall();
}
```

- `SystemName` — 全局唯一标识，用于前置依赖引用与调试。
- `IsPrerequisitesGenerated` — 是否已通过 `GenerateXxxPrerequisites` 生成前置依赖缓存。
- `Prerequisites` / `PrerequisiteSet` — 稳定的前置依赖名称列表与集合，由 `GenerateXxxPrerequisites` 一次性填入。
- `VisitSystem(string)` — 按名称查询全局 System，默认委托至 `IGameManager.Instance.Systems.ResolveFor`。
- `Uninstall` — 容器卸载时调用，按初始化逆序执行。

---

### 2.3 IGlobalSystem ✅

```csharp
// config/Systems/IGlobalSystem.cs
public interface IGlobalSystem : IGameSystem
{
    bool GenerateGlobalSystemPrerequisites(ISystemDeclarationTable<IGlobalSystem> declaredSystems);
    bool Initialize(ISystemRegistrar<IGlobalSystem> registry);
}
```

- `GenerateGlobalSystemPrerequisites` — 一次性生成前置依赖并填入 Prerequisites / PrerequisiteSet、置位 IsPrerequisitesGenerated。传入声明表供校验前置是否存在。若已生成则返回 false。
- `Initialize` — 按拓扑顺序逐个调用，传入的注册器完整包含所有已就位的前置依赖。返回是否初始化成功。

---

### 2.4 初始化流程

```
GameManager._Ready()
  → new GlobalSystemManager ✅ → new ModManager 🔲
  → DiscoverVanillaGlobalSystems() ✅: 反射主程序集，实例化带 [GlobalSystem] 的 IGlobalSystem
  → RegisterVanillaGlobalSystems() ✅: 订阅 GlobalSystemRegistering，注入声明表
  → ModManager 加载模组 DLL 🔲 → 再次反射发现模组 System 并注册
  → systemsManager.Initialize() ✅
      → 广播 GlobalSystemRegistering → 声明表 += system
      → 声明表.BuildFilteredTable() ✅: GenerateGlobalSystemPrerequisites → 校验前置 → 排除失败者
      → new SystemRegistrar(filteredDeclared) ✅: Kahn 拓扑排序 → 逐个 Initialize → 成功则 + 入表
      → 异常报告 (按入度分类) ✅
      → IsInitialized = true → 广播 GlobalSystemsInitialized
```

存档流程（由 SaveManager 管理，留待后续阶段实现）：

```
SaveManager.CreateSave() / LoadSave() 🔲
  → new SaveSystemGroup 🔲 → 反射发现带 [GlobalSystem] 的 ISaveSystem
  → 广播 SaveSystemRegistering → 声明表 → GenerateSaveSystemPrerequisites → 注册器 → Kahn 拓扑 → Initialize
  → 广播 SaveSystemsInitialized
  → new Save(SaveSystemGroup) 🔲 → 广播 SaveReady
```

---

## 3. 事件清单

### 3.1 GlobalSystemManager 事件 ✅

| 事件                         | 触发时机              | 参数                     | 说明                                 |
|----------------------------|-------------------|------------------------|------------------------------------|
| `GlobalSystemRegistering`  | 容器 Initialize 开头  | `IGlobalSystemManager` | 订阅方直接通过 `Declared += system` 注入声明表 |
| `GlobalSystemsInitialized` | 全部全局 System 初始化完毕 | —                      | 此时可安全查询 GameManager.Systems        |

### 3.2 GameManager 事件

| 事件                   | 触发时机 | 参数 | 说明    |
|----------------------|------|----|-------|
| `GameShuttingDown` ✅ | 游戏关闭 | —  | 清理前通知 |

### 3.3 SaveSystemGroup 事件

| 事件                       | 触发时机            | 参数                 | 状态 |
|--------------------------|-----------------|--------------------|----|
| `SaveSystemRegistering`  | 存档创建/加载时        | `ISaveSystemGroup` | 🔲 |
| `SaveSystemsInitialized` | 全部存档级 System 就绪 | —                  | 🔲 |

### 3.4 ISave 事件

| 事件              | 触发时机     | 参数 | 说明                | 状态 |
|-----------------|----------|----|-------------------|----|
| `SaveReady`     | 创建/加载完成后 | —  | 此时 Systems 已初始化完毕 | 🔲 |
| `SaveUnloading` | 卸载存档前    | —  | 清理前通知             | 🔲 |

---

## 4. Mod 加载流程

### 4.1 目录结构

```
mods/
├── vanilla/           ← 香草（代码硬编码于主 DLL，mod.json 仅含 name/version）
│   └── mod.json
└── some_mod/
    ├── mod.json
    ├── some_mod.dll
    └── some_mod.pck
```

### 4.2 mod.json

```json
{ "name": "some_mod", "version": "1.0.0", "dependencies": ["vanilla"] }
```

> `entry_class` 和 `soft_dependencies` 已退役。入口：`[GlobalSystem]` 标记类。依赖：`GenerateXxxPrerequisites`。

### 4.3 加载步骤 🔲

```
ModManager 扫描 mods/ → 按依赖排序 → 加载 DLL 到 AppDomain
  → 反射扫描带 [GlobalSystem] 的 IGlobalSystem/ISaveSystem → 实例化 → 注册到声明表
  → 容器 Initialize() → Kahn 拓扑排序
```

---

## 5. 核心接口

### 5.1 IGameManager ✅

```csharp
// config/IGameManager.cs
public interface IGameManager
{
    static IGameManager Instance { get; set; }
    IGlobalSystemManager Systems { get; }
    IModManager ModManager { get; }
    event Action GameShuttingDown;
}
```

### 5.2 IGlobalSystemManager ✅

```csharp
// config/Systems/IGlobalSystemManager.cs
public interface IGlobalSystemManager : ISystemContainer<IGlobalSystem>, IGameSystem
{
    event Action<IGlobalSystemManager> GlobalSystemRegistering;
    event Action GlobalSystemsInitialized;
}
```

### 5.3 ISystemContainer<TSystem> ✅

```csharp
// config/Systems/ISystemContainer.cs
public interface ISystemContainer<TSystem> where TSystem : IGameSystem
{
    bool IsInitialized { get; }
    int Count { get; }
    bool IsRegistered(string systemName);
    ISystemDeclarationTable<TSystem> Declared { get; }
    TSystem ResolveFor(IGameSystem visitor, string target);
    TSystem this[IGameSystem visitor, string target] { get; }
}
```

- `ResolveFor(visitor, target)` — 一切 System 查询的唯一枢纽。visitor 为请求方自身。

### 5.4 ISave 🔲

```csharp
// config/Map/ISave.cs
public interface ISave
{
    string SaveId { get; }
    string StorageRootPath { get; }
    ISaveSystemGroup Systems { get; }
    IReadOnlyList<IMapLayer> Layers { get; }
    event Action SaveReady;
    event Action SaveUnloading;
}
```

### 5.5 ISaveSystemGroup 🔲

```csharp
// config/Systems/ISaveSystemGroup.cs
public interface ISaveSystemGroup : ISystemContainer<ISaveSystem>
{
    event Action<ISaveSystemGroup> SaveSystemRegistering;
    event Action SaveSystemsInitialized;
}
```

---

## 6. 关键设计决策

1. **统一反射发现** ✅ — 香草与模组的 System 均通过 `[GlobalSystem]` + 反射发现。`IMod` 退化为纯身份标识（`Name` + `Assembly`），不再承载 System 注册入口。`entry_class` 已移除。

2. **GameManager → Save → World → Layer 节点树** — System 容器仅全局和存档两级，不入节点树。

3. **Kahn 拓扑排序** ✅ — SystemRegistrar 以反向依赖图 + 入度表 + 就绪队列实现 O(V+E)，替代原饱和式扫描。

4. **声明表内聚** ✅ — 注册走声明表 `+` 运算符，筛选由 `BuildFilteredTable` 完成。

5. **依赖统一表达** ✅ — 所有依赖通过 `GenerateXxxPrerequisites` 表达，不设 `soft_dependencies`。

6. **入口收窄** — Mod 通过 `IGameManager` 获取一切能力。

7. **注册走声明表** ✅ — 容器无 `Register` 方法，Handler 直接 `Declared += system`。

8. **`WorldWeaver.Systems` 命名空间** — 与 .NET `System.*` 区分。

---

## 7. 相关文件

### 接口与基类 (config/)

| 文件                                          | 说明                 | 状态 |
|---------------------------------------------|--------------------|----|
| `config/IGameManager.cs`                    | GameManager 对外接口   | ✅  |
| `config/ModCore/IModManager.cs`             | Mod 管理器接口          | ✅  |
| `config/Systems/GlobalSystemAttribute.cs`   | System 标记特性，反射发现入口 | ✅  |
| `config/Systems/IGameSystem.cs`             | System 基接口         | ✅  |
| `config/Systems/IGlobalSystem.cs`           | 全局 System 接口       | ✅  |
| `config/Systems/IGlobalSystemManager.cs`    | 全局 System 容器接口     | ✅  |
| `config/Systems/ISystemContainer.cs`        | System 容器泛型基接口     | ✅  |
| `config/Systems/ISystemDeclarationTable.cs` | 系统声明表接口            | ✅  |
| `config/Systems/ISystemRegistrar.cs`        | 系统注册器只读接口          | ✅  |
| `config/Systems/ISaveSystem.cs`             | 存档级 System 接口      | ✅  |
| `config/Systems/ISaveSystemGroup.cs`        | 存档级 System 容器接口    | ✅  |
| `config/Map/ISave.cs`                       | 存档接口               | ✅  |
| `config/Map/IMapLayer.cs`                   | 地图图层接口             | ✅  |

### 实现 (src/)

| 文件                                      | 说明                      | 状态 |
|-----------------------------------------|-------------------------|----|
| `src/GameManager.cs`                    | 游戏根节点，反射发现、持有容器         | ✅  |
| `src/Systems/GlobalSystemManager.cs`    | 全局 System 容器实现          | ✅  |
| `src/Systems/SystemContainerBase.cs`    | System 容器基类，声明表→注册器→初始化 | ✅  |
| `src/Systems/SystemDeclarationTable.cs` | 系统声明表实现                 | ✅  |
| `src/Systems/SystemRegistrar.cs`        | 系统注册器，Kahn 拓扑排序         | ✅  |
| `src/Systems/SaveSystemGroup.cs`        | 存档级 System 容器实现         | 🔲 |
| `src/Map/Save.cs`                       | 存档 Node 实现              | 🔲 |
| `src/Systems/SaveManager.cs`            | 存档生命周期管理                | 🔲 |
| `src/ModCore/ModManager.cs`             | Mod 管理器                 | 🔲 |
