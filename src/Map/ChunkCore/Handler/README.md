# 状态处理器 (State Handlers)

## 地位与职责

状态处理器是区块状态机架构中的**执行单元**，负责实现状态转换过程中的具体业务逻辑。

### 架构定位

```
┌─────────────────────────────────────────────────────────────┐
│                    ChunkStateNodeInfo                        │
│  ┌─────────────────────────────────────────────────────┐    │
│  │  Node            →  状态节点枚举                      │    │
│  │  ValidTransitions →  可转换目标列表                   │    │
│  │  Priority        →  优先级                           │    │
│  │  Callback        →  回调枚举                         │    │
│  │  IsStable        →  是否稳定态                       │    │
│  │  Description     →  状态描述                         │    │
│  │  Handler         →  状态处理器（执行逻辑）            │    │
│  └─────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────┘
```

Handler 作为 `ChunkStateNodeInfo` 的一个属性存在，与状态元数据分离：

- **元数据**（Node、ValidTransitions 等）：描述状态的特征和转换规则
- **Handler**：执行状态转换时的具体操作

### 设计原则

1. **单一职责**：Handler 只包含执行逻辑，不包含状态元数据
2. **策略模式**：通过 `StateHandler` 抽象类实现可替换的执行策略
3. **开闭原则**：新增状态只需添加新的 Handler 类，无需修改现有代码

## 接口定义

```csharp
public abstract class StateHandler
{
    /// <summary>
    /// 执行状态处理逻辑
    /// </summary>
    /// <param name="chunk">目标区块实例</param>
    public abstract StateExecutionResult Execute(Chunk chunk);
}
```

## 处理器列表

| 处理器                                 | 状态类型 | 职责说明           |
|-------------------------------------|------|----------------|
| `ExitHandler`                       | 终态   | 处理区块从状态机中移除    |
| `EnterHandler`                      | 起始态  | 处理区块进入状态机的初始化  |
| `NotInMemoryHandler`                | 稳定态  | 区块未加载到内存时的占位处理 |
| `LoadingInMemoryHandler`            | 过渡态  | 执行区块数据加载到内存    |
| `ReadingInformationHandler`         | 过渡态  | 同步读取区块元数据      |
| `ReadingInformationInThreadHandler` | 过渡态  | 异步读取区块元数据      |
| `LoadedInMemoryHandler`             | 稳定态  | 区块已加载到内存后的处理   |
| `SavingInformationHandler`          | 过渡态  | 同步保存区块元数据      |
| `SavingInformationInThreadHandler`  | 过渡态  | 异步保存区块元数据      |
| `DeletingFromMemoryHandler`         | 过渡态  | 从内存中卸载区块数据     |
| `LoadingInGameHandler`              | 过渡态  | 加载区块到游戏场景      |
| `LoadedInGameHandler`               | 稳定态  | 区块已加载到游戏场景后的处理 |
| `DeletingFromGameHandler`           | 过渡态  | 从游戏场景中卸载区块     |

<br />

## 状态转换流程

![ChunkState Flow](../ChunkStateFlow.png)

## 扩展指南

1. 创建新的 Handler 类实现 `IStateHandler` 接口
2. 在 `ChunkStateNodeInfo` 数组中将 Handler 实例赋值给对应状态的 `Handler` 属性
3. 实现 `Execute` 方法中的业务逻辑
