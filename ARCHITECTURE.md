# WorldWeaver 架构文档

**版本：2.5**
**日期：2026-05-10**

---

## 1. 架构总览

```
GameManager (场景根 Node : IGameManager)
│   static Instance — 全局单例入口
│
├── ModManager : IModManager
│   └── 扫描 mods/{mod}/ 目录，加载 vanilla 与社区模组
│
├── Systems: GlobalSystemsManager : IGlobalSystemsManager
│   ├── SaveManager      ← 香草注册，管理存档生命周期
│   └── ... (模组可注入)
│
└── 事件流
    ├── GlobalSystemRegistering   ← 容器广播，参数为 IGlobalSystemsManager 自身
    └── GlobalSystemsInitialized  ← 容器广播，全部就绪
```

节点树结构：

```
GameManager              ← 场景根 Node，静态单例
  └── SaveManager        ← 全局 System，管理存档生命周期
        └── Save[]       ← 每个存档一个实例
              ├── Systems: SaveSystemsManager  ← 存档级 System 容器
              ├── World[]                     ← 存档内可切换多个世界
              │     └── Layer[]               ← 每个世界由多个图层组成
              └── 存档事件 (SaveReady / SaveUnloading)
```

> System 容器仅存在于全局和存档两级。World/Layer 是游戏对象，不设 System 容器。

---

## 2. System 层级

### 2.1 两层 System

|      | 全局 System                    | 存档级 System                 |
|------|------------------------------|----------------------------|
| 接口   | `IGlobalSystem`              | `ISaveSystem`              |
| 基接口  | `IGameSystem`                | `IGameSystem`              |
| 基类   | `GlobalSystem`               | `SaveSystem` (待实现)         |
| 容器类  | `GlobalSystemsManager`       | `SaveSystemsManager` (待实现) |
| 容器接口 | `IGlobalSystemsManager`      | `ISaveSystemsManager`      |
| 属性   | `GameManager.Systems`        | `ISave.Systems`            |
| 生命周期 | 游戏进程绑定                       | 存档加载/卸载                    |
| 注册方式 | `GlobalSystemRegistering` 事件 | `SaveSystemRegistering` 事件 |

### 2.2 IGameSystem

```csharp
public interface IGameSystem
{
    string SystemName { get; }
    IGameSystem GetGlobalSystem(string systemName);
    void Uninstall();
}
```

- `SystemName` — 全局唯一标识，用于前置依赖引用与调试。
- `GetGlobalSystem(string)` — 按名称查询同级 System。通过 `GameManager.Instance.Systems.GetGlobalSystem(this, name)` 执行，visitor 为调用方自身。
- `Uninstall` — 容器卸载时调用，按初始化逆序执行。

> `GetPrerequisites` 和 `Initialize` 不在基接口上——它们携带的类型与各自 System 层级绑定，分别定义在 `IGlobalSystem` 和 `ISaveSystem` 上。

### 2.3 IGlobalSystem

```csharp
public interface IGlobalSystem : IGameSystem
{
    string[] GetGlobalSystemPrerequisites(Dictionary<string, IGlobalSystem> declaredSystems);
    void Initialize(Dictionary<string, IGlobalSystem> registry);
}
```

- `GetGlobalSystemPrerequisites` — 在声明表→注册表转换阶段一次性调用，传入声明表（Key 为 SystemName），返回前置依赖名称数组。检测到环依赖或缺失前置则直接报错。
- `Initialize` — 按拓扑顺序逐个调用，传入的注册表完整包含所有前置依赖。

### 2.4 初始化流程

```
GameManager._Ready()
  → new GlobalSystemsManager → new ModManager
  → 订阅 systemsManager.GlobalSystemRegistering += 香草注册
  → systemsManager.Initialize()
      → 广播 GlobalSystemRegistering(systemsManager 自身)
          → 香草硬编码注册
          → 模组注册自定义 IGlobalSystem
      → BuildRegistrationTable(): 声明表 → 各 System 调用 GetGlobalSystemPrerequisites → 注册表
      → TopologicalSort(registrationTable): 检测环/缺失前置
      → 逐个 entry.Initialize(_systemTable)，同时填充系统表
      → IsInitialized = true，清空声明表
      → 广播 GlobalSystemsInitialized
```

存档流程（由 SaveManager 管理，留待后续阶段实现）：

```
SaveManager.CreateSave() 或 SaveManager.LoadSave()
  → new SaveSystemsManager
  → 广播 SaveSystemRegistering
  → 收集至声明表 → GetSaveSystemPrerequisites → 注册表 → 拓扑排序 → Initialize
  → 广播 SaveSystemsInitialized
  → new Save(SaveSystemsManager) → 广播 SaveReady
```

---

## 3. 事件清单

### 3.1 GlobalSystemsManager 事件

| 事件                         | 触发时机              | 参数                      | 说明                          |
|----------------------------|-------------------|-------------------------|-----------------------------|
| `GlobalSystemRegistering`  | 容器 Initialize 开头  | `IGlobalSystemsManager` | 订阅方直接调用容器 Register 注入声明表    |
| `GlobalSystemsInitialized` | 全部全局 System 初始化完毕 | —                       | 此时可安全查询 GameManager.Systems |

### 3.2 GameManager 事件

| 事件                 | 触发时机 | 参数 | 说明    |
|--------------------|------|----|-------|
| `GameShuttingDown` | 游戏关闭 | —  | 清理前通知 |

### 3.3 SaveSystemsManager 事件（设计阶段）

| 事件                       | 触发时机            | 参数                    |
|--------------------------|-----------------|-----------------------|
| `SaveSystemRegistering`  | 存档创建/加载时        | `ISaveSystemsManager` |
| `SaveSystemsInitialized` | 全部存档级 System 就绪 | —                     |

### 3.4 ISave 事件

| 事件              | 触发时机     | 参数 | 说明                |
|-----------------|----------|----|-------------------|
| `SaveReady`     | 创建/加载完成后 | —  | 此时 Systems 已初始化完毕 |
| `SaveUnloading` | 卸载存档前    | —  | 清理前通知             |

---

## 4. Mod 加载流程

```
mods/                          ← 模组父目录
├── vanilla/                   ← 香草（官方内容，代码已硬编码于主 DLL，不实现 IMod）
│   └── mod.json               ← 仅含 name/version，无 entry_class
└── some_community_mod/
    ├── mod.json
    ├── icon.png
    ├── some_community_mod.dll
    └── some_community_mod.pck
```

### 4.1 mod.json 结构

```json
{
    "name": "some_community_mod",
    "version": "1.0.0",
    "entry_class": "SomeMod.Main",
    "dependencies": ["vanilla"],
    "soft_dependencies": ["another_mod"]
}
```
> 香草的 mod.json 仅含 name 与 version，无 entry_class。

### 4.2 加载步骤

```
GameManager._Ready()
  → ModManager 扫描 mods/ 目录
  → 按依赖排序模组（vanilla 固定最先）
  → 遇到 vanilla 时：跳过 DLL 加载与 IMod 实例化，直接走硬编码初始化
  → 社区模组：加载 DLL 程序集 → 加载 PCK 资源包 → 实例化入口类（IMod）
    → 调用 OnLoad(IGameManager)，IMod 订阅 GlobalSystemRegistering 注册自己的 IGlobalSystem
  → 拓扑排序 + Initialize(registry)
```

---

## 5. 核心接口

### 5.1 IGameManager

```csharp
public interface IGameManager
{
    IGlobalSystemsManager Systems { get; }
    IModManager ModManager { get; }
    event Action GameShuttingDown;
}
```

> 模组的唯一入口。`GlobalSystemRegistering` 与 `GlobalSystemsInitialized` 归属 `IGlobalSystemsManager`，不在此处。

### 5.2 IGlobalSystemsManager

```csharp
public interface IGlobalSystemsManager
{
    bool IsInitialized { get; }
    int Count { get; }
    bool ContainsKey(string systemName);
    IGlobalSystem GetGlobalSystem(IGameSystem visitor, string target);
    IGlobalSystem this[IGameSystem visitor, string target] { get; }
    IGlobalSystem ResolveSystem(string systemName);
    event Action<IGlobalSystemsManager> GlobalSystemRegistering;
    event Action GlobalSystemsInitialized;
}
```

- `GetGlobalSystem(visitor, target)` — 一切查询的唯一枢纽。visitor 为请求方自身。
- `this[visitor, target]` — 索引器语法糖，直接委托至 `GetGlobalSystem`。
- `ResolveSystem(string)` — 容器以自身为 visitor 查询。非 System 上下文访问全局 System 的唯一合法入口。

### 5.3 ISave（设计阶段）

```csharp
public interface ISave
{
    string SaveId { get; }
    string StorageRootPath { get; }
    ISaveSystemsManager Systems { get; }
    IReadOnlyList<IMapLayer> Layers { get; }
    event Action SaveReady;
    event Action SaveUnloading;
}
```

### 5.4 ISaveSystemsManager（设计阶段）

```csharp
public interface ISaveSystemsManager
{
    bool IsInitialized { get; }
    int Count { get; }
    bool ContainsKey(string systemName);
    ISaveSystem GetSaveSystem(IGameSystem visitor, string target);
    ISaveSystem this[IGameSystem visitor, string target] { get; }
    ISaveSystem ResolveSystem(string systemName);
    event Action<ISaveSystemsManager> SaveSystemRegistering;
    event Action SaveSystemsInitialized;
}
```

---

## 6. 关键设计决策

1. **香草硬编码**——香草不实现 IMod，其逻辑直接编译进主 DLL。ModManager 扫描到 vanilla 时跳过 DLL 加载与 IMod 实例化，直接走内置初始化流程。

2. **GameManager → Save → World → Layer 节点树**——System 容器仅存在于全局和存档两级，World/Layer 是游戏对象。

3. **香草为骨、模组为肉**——香草提供核心 System 与事件广播点，模组通过订阅事件、注册自定义 System 进行扩展。

4. **声明→注册→拓扑排序三阶段**——System 先入声明表（`Dictionary<string, T>`，TryAdd 去重），调用 `Get{Scope}SystemPrerequisites(declaredSystems)` 注入前置，构建注册表，再拓扑排序。获取 System 通过 `GetGlobalSystem(visitor, target)` 完成，visitor 为请求方自身——"只有 System 才能访问 System"。

5. **入口收窄**——Mod 只拿到 `IGameManager`，通过事件与容器逐步获取下游对象。事件不另设 Registrar 接口，直接传容器自身。

6. **配置分离编译**——`config/` 下为纯接口，编译为独立的 API DLL，供模组引用。`src/` 下为实现，引用 API DLL。

7. **`GetPrerequisites` 与 `Initialize` 按层级携带类型**——不在 `IGameSystem` 上定义，而在 `IGlobalSystem` / `ISaveSystem` 上分别以 `Dictionary<string, IGlobalSystem>` / `Dictionary<string, ISaveSystem>` 入参，避免包装转换。

8. **`WorldWeaver.Systems` 命名空间**——因 `WorldWeaver.System` 与 .NET 的 `System.*` 全局命名空间冲突，改用复数形式。

---

## 7. 相关文件

### 接口 (config/)

| 文件                                        | 说明               |
|-------------------------------------------|------------------|
| `config/IGameManager.cs`                  | GameManager 对外接口 |
| `config/ModSystem/IMod.cs`                | Mod 入口接口         |
| `config/ModSystem/IModManager.cs`         | Mod 管理器接口        |
| `config/Systems/IGameSystem.cs`           | System 基接口       |
| `config/Systems/IGlobalSystem.cs`         | 全局 System 接口     |
| `config/Systems/IGlobalSystemsManager.cs` | 全局 System 容器接口   |
| `config/Systems/ISaveSystem.cs`           | 存档级 System 接口    |
| `config/Systems/ISaveSystemsManager.cs`   | 存档级 System 容器接口  |
| `config/Systems/ISave.cs`                 | 存档接口             |
| `config/Systems/IMapLayer.cs`             | 地图图层接口           |

### 实现 (src/)

| 文件                                    | 说明                                                   |
|---------------------------------------|------------------------------------------------------|
| `src/GameManager.cs`                  | 游戏根节点，持有容器与 Mod 管理器                                  |
| `src/Systems/GlobalSystemsManager.cs` | 全局 System 容器实现                                       |
| `src/Systems/GameSystem.cs`           | System 基类，GetGlobalSystem 通过 GameManager.Instance 执行 |
| `src/Systems/GlobalSystem.cs`         | 全局 System 基类                                         |
| `src/Systems/ModManager.cs`           | Mod 管理器（桩）                                           |

### 规范

| 文件                        | 说明     |
|---------------------------|--------|
| `src/CODE_STYLE.md`       | 代码风格规范 |
| `src/PROJECT_STANDARD.md` | 项目标准规范 |
