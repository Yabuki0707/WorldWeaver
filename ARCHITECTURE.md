# WorldWeaver 架构文档

**版本：2.0**
**日期：2026-05-07**

---

## 1. 架构总览

```
GameManager (场景根 Node)
│
├── ModManager (硬编码，不可替换)
│   └── 扫描 mods/{mod}/ 目录，加载 vanilla 与社区模组
│
├── Systems: GlobalSystemsManager (全局 System 容器，术语称 GlobalSystems)
│   ├── SaveManager      ← 香草注册，管理存档生命周期
│   ├── ModManager 本身   ← 作为 System 之一注册
│   └── ... (模组可注入)
│
└── 事件流
    ├── GlobalSystemRegistering   ← 全局 System 收集阶段
    └── GlobalSystemsInitialized  ← 全局 System 全部就绪
```

节点树结构：

```
GameManager              ← 全局级，场景根 Node
  └── SaveManager        ← 全局 System，管理存档生命周期
        └── Save[]       ← 每个存档一个 Node 实例
              ├── Systems: SaveSystemsGroup  ← 存档级 System 容器（术语称 SaveSystems）
              ├── World[]                    ← 存档内可切换多个世界
              │     └── Layer[]              ← 每个世界由多个图层组成
              └── 存档事件 (创建/加载/卸载)
```

> System 仅存在于全局和存档两级。World/Layer 不再有 System 容器，其详细设计留到后续阶段展开。

---

## 2. System 层级

### 2.1 两层 System

|      | 全局 System                    | 存档级 System                        |
|------|------------------------------|-----------------------------------|
| 接口   | `IGlobalSystem`              | `ISaveSystem`                     |
| 基接口  | `IGameSystem`                | `IGameSystem`                     |
| 容器类  | `GlobalSystemsManager`       | `SaveSystemsGroup`                |
| 属性   | `GameManager.Systems`        | `Save.Systems`                    |
| 术语   | GlobalSystems                | SaveSystems                       |
| 生命周期 | 游戏进程绑定                       | 存档加载/卸载                           |
| 注册方式 | `GlobalSystemRegistering` 事件 | Save 加载事件（由 SaveManager 广播）       |
| 示例   | `SaveManager`, `ModManager`  | `TileTypeManager`, `EntitySystem` |

### 2.2 IGameSystem

```csharp
public interface IGameSystem
{
    string SystemName { get; }

    // 一次性声明前置依赖：传入声明表，返回所依赖的 System 名称
    string[] GetPrerequisites(IReadOnlyDictionary<string, IGameSystem> declaredSystems);

    // 初始化：按拓扑顺序依次调用，传入已初始化的 System 注册表（含全部前置）
    void Initialize(IReadOnlyDictionary<string, IGameSystem> registry);

    // 卸载：清理所有运行时状态
    void Uninstall();
}
```

- `GetPrerequisites` 在声明表→注册表转换阶段一次性调用，注入前置后转为注册表，再拓扑排序，检测到环或缺失前置则直接报错。
- `Initialize` 按拓扑顺序逐个调用，传入的注册表完整包含该 System 的所有前置依赖。
- `Uninstall` 在容器卸载时调用，按初始化逆序执行。

### 2.3 初始化流程

```
GameManager._Ready()
  → 广播 GlobalSystemRegistering (IGlobalSystemRegistrar)
    → 香草硬编码注册 SaveManager, ModManager 等
    → 模组注册自定义 IGlobalSystem
  → 收集至声明表 → 各 System 调用 GetPrerequisites(declaredSystems) → 注入前置 → 转为注册表
  → 拓扑排序（按注入后的前置依赖）
  → 逐个调用 Initialize(registry)
  → 广播 GlobalSystemsInitialized
```

存档流程（由 SaveManager 管理）：

```
SaveManager.CreateSave() 或 SaveManager.LoadSave()
  → 广播存档级注册事件 (ISaveSystemRegistrar)
    → 香草注册 TileTypeManager 等
    → 模组注册自定义 ISaveSystem
  → 收集至声明表 → 各 System 调用 GetPrerequisites(declaredSystems) → 注入前置 → 转为注册表
  → 拓扑排序
  → 逐个调用 Initialize(registry)
```

---

## 3. 事件清单

### 3.1 GameManager 事件

| 事件                         | 触发时机              | 参数                       | 说明                             |
|----------------------------|-------------------|--------------------------|--------------------------------|
| `GlobalSystemRegistering`  | 游戏启动              | `IGlobalSystemRegistrar` | 香草与模组注册全局 System               |
| `GlobalSystemsInitialized` | 全部全局 System 初始化完毕 | —                        | 订阅方此时可安全查询 GameManager.Systems |
| `GameShuttingDown`         | 游戏关闭              | —                        | 清理前通知                          |

### 3.2 SaveManager 事件

| 事件              | 触发时机   | 参数                                     | 说明               |
|-----------------|--------|----------------------------------------|------------------|
| `SaveCreating`  | 新建存档   | `ISaveSystemRegistrar`                 | 注册存档级 System     |
| `SaveLoading`   | 加载已有存档 | `ISaveContext`, `ISaveSystemRegistrar` | 注册存档级 System     |
| `SaveLoaded`    | 存档加载完毕 | `ISaveContext`                         | 所有存档级 System 已就绪 |
| `SaveUnloading` | 卸载存档   | `ISaveContext`                         | 清理前通知            |

---

## 4. Mod 加载流程

```
mods/                          ← 模组父目录
├── vanilla/                   ← 香草（官方内容，代码已硬编码于主 DLL，不实现 IMod，无 pck）
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
    "optional_dependencies": ["another_mod"]
}
```
> 香草的 mod.json 仅含 name 与 version，无 entry_class 字段。

### 4.2 加载步骤

```
GameManager._Ready()
  → ModManager 扫描 mods/ 目录
  → 按依赖排序模组（vanilla 固定最先）
  → 遇到 vanilla 时：跳过 DLL 加载与 IMod 实例化，直接走硬编码初始化（注册 SaveManager 等全局 System）
  → 社区模组：加载 DLL 程序集 → 加载 PCK 资源包 → 实例化入口类（IMod）→ 调用 OnLoad(IGameManager)
  → GlobalSystemRegistering 事件中，各 Mod 注册自己的 IGlobalSystem
  → 拓扑排序 + Initialize(registry)
```

---

## 5. GameManager 对外接口（IGameManager）

```csharp
public interface IGameManager
{
    // 硬编码
    ModManager ModManager { get; }

    // 全局 System 容器
    GlobalSystemsManager Systems { get; }

    // 事件
    event Action<IGlobalSystemRegistrar> GlobalSystemRegistering;
    event Action GlobalSystemsInitialized;
    event Action GameShuttingDown;
}

// GlobalSystemsManager 内部访问器：
// IGlobalSystem this[IGlobalSystem requester, string systemName] { get; }
// 要求传入请求方主体，容器据此校验访问合法性。
// 封装后各 System 通过自身 this.GetSystem("name") 即可查询同级 System。
```

---

## 6. Save 接口（ISave）

```csharp
public interface ISave : ISaveContext
{
    // 存档级 System 容器
    SaveSystemsGroup Systems { get; }

    // 事件
    event Action<ISaveSystemRegistrar> SaveCreating;
    event Action<ISaveContext, ISaveSystemRegistrar> SaveLoading;
    event Action<ISaveContext> SaveLoaded;
    event Action<ISaveContext> SaveUnloading;
}

// SaveSystemsGroup 内部访问器：
// ISaveSystem this[ISaveSystem requester, string systemName] { get; }
// 要求传入请求方主体，容器据此校验访问合法性。
// 封装后各 System 通过自身 this.GetSystem("name") 即可查询同级 System。
```

**当前阶段 ISave 仅定义接口、记入文档，不实现。** 实现留到存档系统构建阶段。

---

## 7. 关键设计决策

1. **香草硬编码**——香草不实现 IMod，其逻辑直接编译进主 DLL。ModManager 扫描到 vanilla 时跳过 DLL 加载与 IMod 实例化，直接走内置初始化流程注册 SaveManager 等全局 System
2. **GameManager → Save → World → Layer 节点树**——骨架已定，System 仅存在于全局（GlobalSystems）和存档（SaveSystems）两级，World/Layer 不再设 System 容器
3. **香草为骨、模组为肉**——香草提供核心 System 与事件广播点，模组通过订阅事件、注册自定义 System 进行扩展
4. **声明→注册两阶段 + 拓扑排序**——System 先入声明表，`GetPrerequisites(declaredSystems)` 一次性返回前置，注入后转为注册表，再拓扑排序。获取 System 通过 `this.GetSystem("name")` 完成，是 System 自身行为而非直接操作容器——容器访问器 `[requester, name]` 同样要求传入请求方主体
5. **入口收窄**——Mod 只拿到 `IGameManager`，通过事件与容器逐步获取下游对象（SaveManager、Save 等）
6. **事件归属游戏对象自身**——事件不强制集中于某一处，各游戏对象按职责暴露自身事件

---

## 8. 相关文件

| 文件                                        | 说明                |
|-------------------------------------------|-------------------|
| `config/System/IGameSystem.cs`            | System 基接口        |
| `config/System/IGlobalSystem.cs`          | 全局 System 接口      |
| `config/System/IGlobalSystemRegistrar.cs` | 全局 System 注册器     |
| `config/System/ISaveSystem.cs`            | 存档级 System 接口     |
| `config/System/ISaveSystemRegistrar.cs`   | 存档级 System 注册器    |
| `config/System/ISaveContext.cs`           | 存档上下文接口           |
| `config/IMod.cs`                          | Mod 入口接口          |
| `src/CODE_STYLE.md`                       | 代码风格规范            |
| `src/PROJECT_STANDARD.md`                 | 项目标准规范            |
| `src/MapSystem/README.md`                 | MapSystem 简介      |
| `src/MapSystem/TODO.md`                   | MapSystem TODO 列表 |
