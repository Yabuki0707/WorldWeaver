# ChunkSystem README（初稿）

本稿聚焦你关心的主线：`ChunkManager` 的自主驱动、`update` 如何推进 `Chunk + ChunkState`、以及 `Handler` 执行结果如何反馈回 `State` 形成闭环。

## 1. 系统定位

`ChunkSystem` 是地图分块运行时核心，主要职责有三类：

- 生命周期管理：创建/移除 Chunk。
- 状态机驱动：按帧推进 Chunk 状态。
- 数据与事件出口：统一对上层广播状态变化和 Tile 变化。

核心关系：

`MapLayer -> ChunkManager -> Chunk -> ChunkState / ChunkData`

其中当前架构下，`ChunkManager` 是唯一驱动者，`Chunk` 与 `ChunkState` 都不直接执行业务副作用。

## 2. 关键角色职责

### 2.1 ChunkManager（主驱动）

- 维护 `_chunks`（全量索引）与 `_updatingChunks`（待驱动集合）。
- 每帧调用 `Update()` 推进状态。
- 统一执行 `StateHandler`。
- 统一处理执行结果并触发事件：
  - `ChunkStateUpdated`
  - `ChunkStateStableReached`
  - `ChunkCreated`
  - `ChunkRemoved`
  - `TilesChanged`

### 2.2 Chunk（流程适配层）

- 持有身份与容器：`CPosition`、`Uid`、`Data`、`State`。
- 对外暴露三件事：
  - `GetStateUpdateHandler()`：给 Manager 本轮该执行哪个 Handler。
  - `StateUpdate()`：在“Handler 成功后”提交状态迁移。
  - `HandleStateExecutionFailure()`：在“Handler 失败后”执行状态收敛。

注意：`Chunk` 本身不执行 handler，只做流程桥接。

### 2.3 ChunkState（决策与收敛层）

- 维护状态字段：
  - `CurrentNode`、`PreviousNode`
  - `CurrentStableNode`
  - `FinalStableNode`（最终目标稳定节点）
  - `TargetStableNode`（中间稳定目标）
  - `TargetNode`（下一跳节点）
- 负责路径决策（宏观稳定路径 + 微观详细路径）。
- 负责失败收敛（阻塞/回退/重试策略）。

### 2.4 StateHandler（执行层）

- 只做状态副作用执行，返回 `StateExecutionResult`：
  - `Success`
  - `RetryLater`
  - `PermanentFailure`
- 不直接改 `ChunkState`，状态写回由 Manager 驱动回流。

## 3. 状态节点与路径机制

状态节点由 `ChunkStateNode` 定义，稳定节点为：

- `Enter`
- `NotInMemory`
- `LoadedInMemory`
- `LoadedInGame`
- `Exit`

路径分两级：

- 宏观稳定路径：`GetStablePathLookup(fromStable, toStable)`，用于选下一段稳定目标。
- 微观详细路径：`GetDetailedPathLookup(currentStable, targetStable)`，用于在当前节点选下一跳。

`ChunkState.SelectTargetNode()` 的核心是：

1. 若到达当前中间稳定目标，则先重算下一段稳定目标。
2. 在 `CurrentNode` 的可转移邻接中，筛选“在微观路径上且未被阻塞且未被全局禁用”的节点。
3. 按优先级选出下一跳，写入 `TargetNode`。

## 4. 主流程（Manager 驱动）

每帧 `ChunkManager.Update()` 对每个活跃 Chunk 做如下流程：

1. 询问需求：`chunk.GetStateUpdateHandler()`  
   - 若 `TargetNode` 为空，内部先触发 `State.SelectTargetNode()`。
   - 若仍无目标，说明本轮无需推进，直接跳过。
2. 执行副作用：`handler.Execute(manager, chunk)`。
3. 按执行结果分支：
   - `Success`：调用 `chunk.StateUpdate()`，提交状态迁移。
   - `RetryLater` / `PermanentFailure`：调用 `chunk.HandleStateExecutionFailure(...)`。
4. 成功推进后统一发事件；若新节点是 `Exit`，延迟到循环后移除 Chunk。

## 5. 你强调的“结果回流到 State”闭环

闭环不是“handler 直接改 state”，而是四段式回流：

1. `State` 决策下一跳（`TargetNode`）。
2. `Manager` 执行该跳对应 `Handler`。
3. `Manager` 把执行结果回灌给 `ChunkState`：
   - 成功：`UpdateToTargetNode()`
   - 失败：`HandleExecutionFailure()`
4. `ChunkState` 产出 `ChunkStateUpdateResult`（成功分支）回传给 `Manager`，由 Manager 广播事件，外部据此再调整目标状态，进入下一帧循环。

这就是完整反馈回路：

`State决策 -> Handler执行 -> State收敛/迁移 -> Manager事件输出 -> 外部调整目标 -> 下一轮State决策`

## 6. 失败与重试策略（反馈环关键）

### 6.1 Success

- `ChunkState.UpdateToTargetNode()` 提交 `CurrentNode = TargetNode`。
- 清空 `TargetNode` 与阻塞表。
- 若落入稳定节点，更新 `CurrentStableNode` 并返回稳定节点变化信息。

### 6.2 RetryLater

- `ChunkState` 保持当前状态不变。
- 目标可在后续帧继续尝试（适合异步未就绪场景）。

### 6.3 PermanentFailure

- 若有 `PreviousNode` 且不同于当前节点：回退到 `PreviousNode`。
- 阻塞失败节点，避免立刻再次选中。
- 清空 `TargetNode`，下帧重新选路。

若由于局部阻塞导致“无路可走”，状态机会清空阻塞表并重试，避免死锁。

## 7. 当前实现状态（非常重要）

现阶段多个具体 `Handler` 仍是占位实现（直接返回 `Success`），例如：

- `LoadingInMemoryHandler`
- `ReadingInformationHandler`
- `SavingInformationHandler`
- `LoadingInGameHandler`
- `DeletingFromMemoryHandler`
- `DeletingFromGameHandler`

这意味着主流程和反馈闭环已经搭好，但具体 IO/资源加载副作用仍待接入。

## 8. 外部使用建议

1. 创建 Chunk：`ChunkManager.CreateChunk(...)`。
2. 设置目标稳定状态：`chunk.FinalStateNode = xxx`。
3. 在游戏主循环持续调用：`ChunkManager.Update()`。
4. 订阅状态事件以驱动上层逻辑（渲染、实体激活、调度策略等）。

## 9. 扩展实现建议

### 9.1 新增状态节点

1. 在 `ChunkStateNode` 增加节点。
2. 在 `ChunkStateMachine` 的 `_stateNodesInfo` 配置：
   - `ValidTransitions`
   - `Priority`
   - `IsStable`
   - `Handler`
3. 验证路径表初始化是否符合预期。

### 9.2 新增或替换 Handler

1. 继承 `StateHandler` 并实现 `Execute(ChunkManager, Chunk)`。
2. 返回值必须准确区分：
   - 可推进：`Success`
   - 暂不可推进：`RetryLater`
   - 路径应回避：`PermanentFailure`
3. 尽量保证副作用幂等，避免重试造成重复写入。

## 10. 参考文件

- `ChunkManager`：`src/MapSystem/ChunkSystem/ChunkManager.cs`
- `Chunk`：`src/MapSystem/ChunkSystem/Chunk.cs`
- `ChunkState`：`src/MapSystem/ChunkSystem/State/ChunkState.cs`
- `ChunkStateMachine`：`src/MapSystem/ChunkSystem/State/ChunkStateMachine.cs`
- `StateHandler`：`src/MapSystem/ChunkSystem/Handler/StateHandler.cs`
- Handler 说明：`src/MapSystem/ChunkSystem/Handler/README.md`
