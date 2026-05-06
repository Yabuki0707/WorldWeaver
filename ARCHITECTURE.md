# WorldWeaver 架构文档

**版本：1.0**
**日期：2026-05-06**

---

## 1. 架构总览

```
GameManager (场景根 Node)
│
├── ModManager (硬编码，不可替换)
│   └── 扫描 mods/{mod}/ 目录，加载 vanilla 与社区模组
│
├── FrozenDictionary<string, IGlobalSystem> (全局 System 表)
│   ├── SaveSystem        ← 香草注册，提供存档事件
│   ├── ModManager 本身   ← 作为 System 之一注册
│   └── ... (模组可注入)
│
└── 事件流
    ├── GlobalSystemRegistering   ← 全局 System 收集阶段
    └── GlobalSystemsInitialized  ← 全局 System 全部就绪
```

节点树结构：

```
GameManager           ← 全局级，场景根 Node
  └── SaveSystem      ← 全局 System 之一，管理存档生命周期
        └── Save[]    ← 每个存档一个 Node 实例
              ├── FrozenDictionary<string, ISaveSystem>
              ├── World[]         ← 存档内可切换多个世界
              │     └── Layer[]  ← 每个世界由多个图层组成
              └── 存档事件 (创建/加载/卸载)
```

> 本节仅标记节点树骨架，World/Layer 系统的详细设计留到后续阶段展开。

---

## 2. System 层级

### 2.1 两层 System

|      | 全局 System                    | 存档级 System                        |
|------|------------------------------|-----------------------------------|
| 接口   | `IGlobalSystem`              | `ISaveSystem`                     |
| 基接口  | `IGameSystem`                | `IGameSystem`                     |
| 持有者  | `GameManager`                | `Save`                            |
| 生命周期 | 游戏进程绑定                       | 存档加载/卸载                           |
| 注册方式 | `GlobalSystemRegistering` 事件 | Save 加载事件（通过 SaveSystem 广播）       |
| 示例   | `SaveSystem`, `ModManager`   | `TileTypeManager`, `EntitySystem` |

### 2.2 IGameSystem

```csharp
public interface IGameSystem
{
    string SystemName { get; }       // 全局唯一标识
    string[] Prerequisites { get; }  // 前置依赖 System 名称
}
```

所有 System 均声明前置依赖，初始化时拓扑排序，检测环与缺失前置则直接报错。

### 2.3 初始化流程

```
GameManager._Ready()
  → 广播 GlobalSystemRegistering (IGlobalSystemRegistrar)
    → 香草注册 SaveSystem, ModManager 等
    → 模组注册自定义 IGlobalSystem
  → 收集所有 IGlobalSystem
  → 拓扑排序（按 Prerequisites）
  → 逐个调用 OnGameStart()
  → 广播 GlobalSystemsInitialized
```

存档流程（由 SaveSystem 管理）：

```
SaveSystem.CreateSave() 或 SaveSystem.LoadSave()
  → 广播存档级注册事件 (ISaveSystemRegistrar)
    → 香草注册 TileTypeManager 等
    → 模组注册自定义 ISaveSystem
  → 收集所有 ISaveSystem
  → 拓扑排序
  → 逐个调用 OnSaveLoad(ISaveContext)
```

---

## 3. 事件清单

### 3.1 GameManager 事件

| 事件                         | 触发时机              | 参数                       | 说明                         |
|----------------------------|-------------------|--------------------------|----------------------------|
| `GlobalSystemRegistering`  | 游戏启动              | `IGlobalSystemRegistrar` | 香草与模组注册全局 System           |
| `GlobalSystemsInitialized` | 全部全局 System 初始化完毕 | —                        | 订阅方此时可安全查询 `GlobalSystems` |

**GameManager 不暴露存档相关事件。** 存档事件由 `SaveSystem` 独立管理。

### 3.2 SaveSystem 事件

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
├── vanilla/                   ← 香草（官方内容，代码已硬编码于主 DLL，不实现 IMod）
│   ├── mod.json               ← 仅含 name/version，无 entry_class
│   └── vanilla.pck
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
  → 遇到 vanilla 时：跳过 DLL 加载与 IMod 实例化，直接走硬编码初始化（注册 SaveSystem、TileTypeManager 等全局 System）
  → 社区模组：加载 DLL 程序集 → 加载 PCK 资源包 → 实例化入口类（IMod）→ 调用 OnLoad(IGameManager)
  → GlobalSystemRegistering 事件中，各 Mod 注册自己的 IGlobalSystem
  → 拓扑排序 + OnGameStart()
```

---

## 5. GameManager 对外接口（IGameManager）

```csharp
public interface IGameManager
{
    // 硬编码
    ModManager ModManager { get; }

    // 全局 System 表（只读）
    FrozenDictionary<string, IGlobalSystem> GlobalSystems { get; }

    // 按名称获取全局 System
    IGlobalSystem GetGlobalSystem(string systemName);

    // 事件
    event Action<IGlobalSystemRegistrar> GlobalSystemRegistering;
    event Action GlobalSystemsInitialized;
    event Action GameShuttingDown;
}
```

---

## 6. Save 接口（ISave）

```csharp
public interface ISave : ISaveContext
{
    // 存档级 System 表
    FrozenDictionary<string, ISaveSystem> SaveSystems { get; }

    // 按名称获取存档级 System
    ISaveSystem GetSaveSystem(string systemName);

    // 事件
    event Action<ISaveSystemRegistrar> SaveCreating;
    event Action<ISaveContext, ISaveSystemRegistrar> SaveLoading;
    event Action<ISaveContext> SaveLoaded;
    event Action<ISaveContext> SaveUnloading;
}
```

**当前阶段 ISave 仅定义接口、记入文档，不实现。** 实现留到存档系统构建阶段。

---

## 7. 关键设计决策

1. **GameManager 只管全局**——存档相关事件归 `SaveSystem`，GameManager 不触碰存档逻辑
2. **SaveSystem 是全局 System**——`ModManager` 加载香草时自动注册，模组可自由订阅其事件
3. **事件广播 + 拓扑排序**——全局 System 与存档级 System 均走同一套注册+排序机制
4. **入口收窄**——Mod 只拿到 `IGameManager`，通过事件注册 System，通过 `GetSystem<T>()` 在运行时获取已就绪的 System
5. **Save 在结构上位于 Layer/World 之上一层**——当前架构只深入到存档层，更高层（World 间切换、多存档管理等）暂不涉及
6. **香草硬编码**——香草不实现 IMod，其逻辑直接编译进主 DLL。ModManager 扫描到 vanilla 时跳过 DLL 加载与 IMod 实例化，直接走内置初始化流程注册 SaveSystem 等全局 System

---

## 8. 相关文件

| 文件                                 | 说明                |
|------------------------------------|-------------------|
| `config/IGameSystem.cs`            | System 基接口        |
| `config/IGlobalSystem.cs`          | 全局 System 接口      |
| `config/IGlobalSystemRegistrar.cs` | 全局 System 注册器     |
| `config/ISaveSystem.cs`            | 存档级 System 接口     |
| `config/ISaveSystemRegistrar.cs`   | 存档级 System 注册器    |
| `config/ISaveContext.cs`           | 存档上下文接口           |
| `src/CODE_STYLE.md`                | 代码风格规范            |
| `src/PROJECT_STANDARD.md`          | 项目标准规范            |
| `src/MapSystem/README.md`          | MapSystem 简介      |
| `src/MapSystem/TODO.md`            | MapSystem TODO 列表 |
