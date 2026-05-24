# WorldWeaver 架构文档

**版本：3.0**
**日期：2026-05-24**

---

## 1. 架构总览

```
GameManager (场景根 Node : IGameManager)
│   static Instance — 全局单例入口
│
├── ModManager : IModManager
│   └── 扫描 mods/{mod}/ 目录，加载 DLL、按依赖排序、反射发现 [GlobalSystem]
│
├── Systems: GlobalSystemManager : IGlobalSystemManager
│   ├── SaveManager      ← 全局 System，管理存档生命周期（非 Node，入系统表）
│   └── ... (模组可注入)
│
└── 事件流
    ├── GlobalSystemRegistering   ← 容器广播，参数为 IGlobalSystemManager 自身
    └── GlobalSystemsInitialized  ← 容器广播，全部就绪
```

Godot 节点树结构：

```
GameManager              ← 场景根 Node，静态单例
  └── Save[]             ← 每个存档一个 Node 实例，由 SaveManager 动态创建/销毁
        ├── Systems: SaveSystemGroup    ← 存档级 System 容器（不入节点树）
        ├── World[]                     ← 存档内可切换多个世界
        │     └── Layer[]               ← 每个世界由多个图层组成
        └── 存档事件 (SaveReady / SaveUnloading)
```

> SaveManager 是全局 System，存在于 GlobalSystemManager 的系统表中，不是 Godot Node。
> Save 才是 GameManager 的直接子节点。System 容器不入节点树。

---

## 2. System 层级

### 2.1 两层 System

|      | 全局 System                    | 存档级 System                 |
|------|------------------------------|----------------------------|
| 接口   | `IGlobalSystem`              | `ISaveSystem`              |
| 基接口  | `IGameSystem`                | `IGameSystem`              |
| 容器类  | `GlobalSystemManager`        | `SaveSystemGroup` (待实现)    |
| 容器接口 | `IGlobalSystemManager`       | `ISaveSystemGroup`         |
| 属性   | `GameManager.Systems`        | `ISave.Systems`            |
| 生命周期 | 游戏进程绑定                       | 存档加载/卸载                    |
| 注册方式 | `GlobalSystemRegistering` 事件 | `SaveSystemRegistering` 事件 |

### 2.2 IGameSystem

```csharp
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

### 2.3 IGlobalSystem

```csharp
public interface IGlobalSystem : IGameSystem
{
    bool GenerateGlobalSystemPrerequisites(ISystemDeclarationTable<IGlobalSystem> declaredSystems);
    bool Initialize(ISystemRegistrar<IGlobalSystem> registry);
}
```

### 2.4 初始化流程

```
GameManager._Ready()
  → new GlobalSystemManager → new ModManager
  → DiscoverVanillaGlobalSystems(): 反射主程序集，实例化带 [GlobalSystem] 的 IGlobalSystem
  → RegisterVanillaGlobalSystems(): 订阅 GlobalSystemRegistering，注入声明表
  → ModManager 加载模组 DLL → 再次反射发现模组 System 并注册
  → systemsManager.Initialize()
      → 广播 GlobalSystemRegistering → 声明表 += system
      → 声明表.BuildFilteredTable(): GenerateGlobalSystemPrerequisites → 校验前置 → 排除失败者
      → new SystemRegistrar(filteredDeclared) → Kahn 拓扑排序 → 逐个 Initialize → 成功则 + 入表
      → 异常报告 (按入度分类)
      → IsInitialized = true → 广播 GlobalSystemsInitialized
```

---

## 3. Mod 加载

### 3.1 目录结构

```
mods/
├── vanilla/           ← 香草（代码硬编码于主 DLL，mod.json 仅含 name/version）
│   └── mod.json
└── some_mod/
    ├── mod.json
    ├── some_mod.dll
    └── some_mod.pck
```

### 3.2 mod.json

```json
{ "name": "some_mod", "version": "1.0.0", "dependencies": ["vanilla"] }
```

> `entry_class` 和 `soft_dependencies` 已退役。入口：`[GlobalSystem]` 标记类。依赖：`GenerateXxxPrerequisites`。

### 3.3 加载步骤

```
ModManager 扫描 mods/ → 按依赖排序 → 加载 DLL 到 AppDomain
  → 反射扫描带 [GlobalSystem] 的 IGlobalSystem/ISaveSystem → 实例化 → 注册到声明表
  → 容器 Initialize() → Kahn 拓扑排序
```

---

## 4. 核心接口

### 4.1 IGameManager

```csharp
public interface IGameManager
{
    static IGameManager Instance { get; set; }
    IGlobalSystemManager Systems { get; }
    IModManager ModManager { get; }
    event Action GameShuttingDown;
}
```

### 4.2 IGlobalSystemManager

```csharp
public interface IGlobalSystemManager : ISystemContainer<IGlobalSystem>, IGameSystem
{
    event Action<IGlobalSystemManager> GlobalSystemRegistering;
    event Action GlobalSystemsInitialized;
}
```

### 4.3 ISystemContainer<TSystem>

```csharp
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

---

## 5. 关键设计决策

1. **统一反射发现** — 香草与模组的 System 均通过 `[GlobalSystem]` + 反射发现。不再使用 `IMod` 接口与 `entry_class`。

2. **GameManager → Save → World → Layer 节点树** — System 容器仅全局和存档两级，不入节点树。

3. **Kahn 拓扑排序** — SystemRegistrar 以反向依赖图 + 入度表 + 就绪队列实现 O(V+E) 拓扑排序，替代原饱和式扫描。

4. **声明表内聚** — 注册走声明表 `+` 运算符，筛选由声明表 `BuildFilteredTable` 完成。

5. **依赖统一表达** — 所有依赖通过 `GenerateXxxPrerequisites` 表达，不设 `soft_dependencies`。

6. **入口收窄** — Mod 通过 `IGameManager` 获取一切能力。

7. **`WorldWeaver.Systems` 命名空间** — 与 .NET `System.*` 区分。
