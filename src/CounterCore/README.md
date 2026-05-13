# 计数器系统

## 概述

计数器系统由两个类型组成：

- `Counter`：提供线程安全的自增计数。
- `CounterHistoryManager`：负责历史数据的读取、注册表维护与持久化保存。

该系统适合用于需要连续编号的场景，例如物品 ID、任务流水号等。


## 核心概念

- `CounterName`：计数器的持久化标识。相同名称会映射到同一份历史数据。
- `CounterUid`：计数器实例的运行时标识。仅在当前进程内用于实例查询。
- `CountValue`：当前保存的是“下一个待使用的值”，不是上一次已经发出的值。
- `long.MinValue`：在 `CounterHistoryManager` 中表示“无历史数据”。
- `manager = null`：计数器不接入持久化，仅在当前运行期内使用。


## 基础用法

### 不使用持久化

```csharp
Counter counter = new Counter("PlayerId");

long playerId = counter.GetAndIncrement();
GD.Print($"playerId={playerId}");

counter.Dispose();
```

### 使用持久化

```csharp
CounterHistoryManager manager = new CounterHistoryManager("user://save_01/counters.json");
Counter enemyCounter = new Counter("EnemyId", manager);

long enemyId = enemyCounter.GetAndIncrement();
GD.Print($"enemyId={enemyId}");

manager.Save();
enemyCounter.Dispose();
```


## API 参考

### Counter

#### 构造函数

`Counter(string counterName, CounterHistoryManager manager = null, long incrementStep = 1)`

- 创建一个计数器实例。
- `manager` 为 `null` 时不启用持久化。
- `incrementStep` 非法时会被修正为有效值。

#### 属性

| 名称 | 说明 |
| --- | --- |
| `Manager` | 当前关联的 `CounterHistoryManager` 实例。未启用持久化时为 `null`。 |
| `CounterUid` | 当前计数器实例的运行时唯一标识符。 |
| `CounterName` | 当前计数器的持久化名称。 |
| `IncrementStep` | 当前自增步长。设置非法值时会自动修正。 |
| `CountValue` | 当前保存的计数值，即下一个待使用的值。 |
| `IsRegistered` | 当前计数器是否已注册到历史数据管理器。 |

#### 方法

| 名称 | 说明 |
| --- | --- |
| `Increment()` | 按当前步长自增，不返回旧值。 |
| `GetAndIncrement()` | 返回当前值，再按当前步长自增。通常这是最常用的方法。 |
| `IncrementOne()` | 固定按 `1` 自增，不返回旧值。 |
| `GetAndIncrementOne()` | 返回当前值，再固定按 `1` 自增。 |
| `GetValue()` | 读取当前计数值。 |
| `SetValue(long value)` | 直接设置计数值。会破坏正常自增语义，应谨慎使用。 |
| `Dispose()` | 注销当前计数器并移除运行时实例映射。 |
| `GetCounterByUid(Guid uid)` | 通过运行时 `Guid` 查询计数器实例。 |
| `GetCounterByUid(string uid)` | 通过字符串形式的 `Guid` 查询计数器实例。 |


### CounterHistoryManager

#### 构造函数

`CounterHistoryManager(string filePath)`

- 创建一个历史数据管理器。
- 初始化时会尝试从 `filePath` 读取历史数据。

#### 属性

| 名称 | 说明 |
| --- | --- |
| `RegisteredCount` | 当前已注册的计数器数量。 |
| `HistoryDataCount` | 当前历史数据表中的条目数量。 |

#### 方法

| 名称 | 说明 |
| --- | --- |
| `Save()` | 将当前已注册计数器的值写入文件。成功返回 `true`。 |
| `Register(Counter counter)` | 注册一个计数器到管理器。 |
| `Unregister(string counterName)` | 从管理器中移除指定名称的计数器。 |
| `HasHistoryData(string counterName)` | 判断指定名称是否存在历史数据。 |
| `GetHistoryValue(string counterName)` | 读取指定名称的历史值；若不存在则返回 `long.MinValue`。 |
| `Clear()` | 清空历史数据表与注册表。 |


## 使用注意事项

- 该系统包含锁与持久化管理语义，不适合作为高频调用任务中的热路径计数器，更适合存档 ID、实体持久化编号等低频分配场景。
- 需要发号时，优先使用 `GetAndIncrement()`，不要依赖 `CountValue`。
- `CountValue` 表示下一个待使用值，因此直接读取通常没有业务意义。
- `SetValue()` 会改变序列连续性，只适合迁移、纠偏或测试场景。
- 持久化不会自动触发，需要显式调用 `Save()`。
- 不同存档应使用不同的 `CounterHistoryManager` 文件路径。
- 计数器不再使用时，应调用 `Dispose()` 完成注销。
- `CounterHistoryManager` 初始化加载失败时会输出 warning；具体失败原因由加载过程日志给出。
