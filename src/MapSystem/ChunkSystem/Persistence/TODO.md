# Chunk Persistence TODO

## 目标需求

1. 不再保留 `ChunkPersistence` 统一入口，改为围绕“缓存器”重组持久化体系。
2. `ChunkManager` 持有一个持久化缓存器，由缓存器统一负责读取查询、保存缓存、任务分发、定期清理与定期落盘。
3. 阻塞型与线程型持久器不是顶层主体，而是缓存器内部调用的两个工具持久器。
4. 缓存器内部拆分为两个独立状态组件：
   - 结果缓存表
   - 待办任务表
5. 每个 chunk 在结果缓存表中只能有一条总结果，不区分读写类型，只要存在 `ChunkDataStorage` 就可直接读取。
6. 读取流程：
   - 先查结果缓存表
   - 若无结果，再查待办任务表
   - 若仍无任务，则创建对应读取任务
7. 保存流程：
   - `SavingInformation` 将当前 `ChunkDataStorage` 写入结果缓存表
   - 写入缓存成功即返回 `Success`
   - 实际落盘在缓存器清理周期中完成
8. 状态机中不再保留 `SavingInformationInThread`
9. `ReadingInformation` 与 `SavingInformation` 需要放弃原有“同步阻塞”语义，改为缓存驱动语义。
10. `ChunkManager.Update()` 每调用一次视为 1 tick。
11. 缓存器更新频率为每 `3 * 30` tick 一次。
12. 结果缓存只有超过 `17 * 30` tick 后才允许清理。
13. 待办任务每 `7` tick 分发一次：
    - 单个任务中的全部 chunk 必须属于同一 region
    - 单个任务中的全部 chunk 必须属于同一种操作类型
    - 同一 region 的 chunk 可以被拆分到多个任务
    - 单个任务最多 4 个 chunk
14. 若当前可用线程数连续 2 个清理周期都达到上限，则缓存器在清理周期中退化为主线程保存。
15. `ChunkRegionCreater` 需要增加总锁与路径小锁机制，防止多线程同时创建同一个 region 文件。
16. 阻塞型与线程型工具持久器都必须遵守两类锁：
    - creator 锁
    - free-partition 锁


## 结构阐释

### 顶层关系

1. `ChunkManager`
   - 持有 `ChunkPersistenceCache`
   - 在 `Update()` 中驱动缓存器的周期性逻辑

2. `ChunkPersistenceCache`
   - 作为唯一入口和包装器存在
   - 自身负责：
     - 读缓存
     - 写缓存
     - 查询任务是否存在
     - 周期性分发任务
     - 周期性清理结果
     - 周期性落盘
     - 线程爆满时主线程兜底保存
   - 内部组合：
     - `ChunkPersistenceResultCacheTable`
     - `ChunkPersistencePendingTaskSet`
     - `ChunkPersistenceBlockingWorker`
     - `ChunkPersistenceThreadedWorker`

### 内部组件职责

1. `ChunkPersistenceResultCacheTable`
   - 使用原子字典
   - 维护“每个 chunk 一条总结果”
   - 结果对象至少包含：
     - `OperationType`
     - `IsUpdating`
     - `ChunkDataStorage`
     - 时间戳 / 最近更新时间
   - 提供：
     - 查询结果
     - 写入结果
     - 标记更新中
     - 更新结果时间
     - 取出可清理项

2. `ChunkPersistencePendingTaskSet`
   - 使用 `HashSet`
   - 待办项为“区块坐标 + 操作类型”的结构体
   - 提供：
     - 查询任务是否存在
     - 添加任务
     - 删除任务
     - 按 region 与操作类型分组批量取任务
     - 保证单个任务中的全部 chunk 属于同一 region
     - 保证单个任务中的全部 chunk 属于同一种操作类型
     - 限制单个任务最多 4 个 chunk

3. `ChunkPersistenceBlockingWorker`
   - 仅负责阻塞式 region 读写
   - 不持有缓存状态
   - 不持有待办状态

4. `ChunkPersistenceThreadedWorker`
   - 仅负责线程任务中的 region 读写
   - 不持有缓存状态
   - 不持有待办状态

### 锁分工

1. creator 锁
   - 负责“锁内判断文件是否存在 + 不存在则创建”
   - 采用“总锁保护锁表 + 按路径小锁”的结构
   - 与 free-partition 锁分离

2. free-partition 锁
   - 负责 region 数据写入与空闲分区链修改

3. creator 锁与 free-partition 锁都由阻塞型 / 线程型工具持久器共同遵守


## 施工计划

### 阶段 1：补 `ChunkRegionCreater` 锁

1. 为 `ChunkRegionCreater` 新增独立锁表。
2. 锁表采用“总锁保护锁表 + 按路径小锁”。
3. `Create(string regionFilePath)` 改为：
   - 先进入路径锁
   - 锁内判断文件是否存在
   - 不存在才创建
   - 创建后释放锁
4. 明确 creator 锁与 free-partition 锁互不混用。

### 阶段 2：拆分 `ChunkPersistence`

1. 移除 `ChunkPersistence` 的统一职责。
2. 新建：
   - `ChunkPersistenceCache`
   - `ChunkPersistenceBlockingWorker`
   - `ChunkPersistenceThreadedWorker`
3. 将原有阻塞式读写逻辑迁入 `BlockingWorker`
4. 将原有线程读写逻辑迁入 `ThreadedWorker`

### 阶段 3：拆出结果缓存表

1. 新建 `ChunkPersistenceResultCacheTable`
2. 将结果对象结构独立出来
3. 结果表改为“每个 chunk 一条总结果”
4. 保存操作写缓存结果
5. 读取操作先读缓存结果

### 阶段 4：拆出待办任务表

1. 新建 `ChunkPersistencePendingTaskSet`
2. 定义待办项结构：`ChunkPosition + OperationType`
3. 支持去重、查询、删除、批量取任务
4. 实现“单任务仅允许同一 region 且同一操作类型、单任务最多 4 chunk”的批量分发规则

### 阶段 5：把缓存器接入 `ChunkManager`

1. `ChunkManager` 持有 `ChunkPersistenceCache`
2. `Update()` 中增加 tick 计数
3. 每 `7` tick 调用缓存器分发任务
4. 每 `3 * 30` tick 调用缓存器更新、清理与落盘

### 阶段 6：调整状态机与 handler

1. 删除 `SavingInformationInThread`
2. `ReadingInformation` 改为：
   - 查缓存
   - 查待办
   - 无任务则注册读取任务
   - 未命中结果时返回 `RetryLater`
3. `SavingInformation` 改为：
   - 将当前 `ChunkDataStorage` 写入缓存
   - 注册保存待办
   - 立即返回 `Success`
4. 同步更新状态描述文案，去掉原有“同步阻塞”表述

### 阶段 7：实现缓存器清理与延迟落盘

1. 缓存器在清理周期中处理待落盘项
2. 落盘任务进入待办任务表
3. 任务完成后才允许从结果缓存中删除
4. 结果项超过 `17 * 30` tick 才允许清理
5. 若可用线程数连续 2 个清理周期都达到上限，则主线程执行保存兜底
