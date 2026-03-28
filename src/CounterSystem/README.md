# 计数器系统

## 系统概述

计数器系统是一个使用C#语言实现的线程安全自增计数与持久化解决方案，专为游戏开发中的实体ID分配、资源计数等场景设计。该系统提供了可靠的唯一标识符生成机制，支持数据持久化和多存档隔离，确保在复杂的游戏开发环境中稳定运行。

## 核心功能

### 线程安全的自增计数

- 所有读写操作都使用锁保护，确保在多线程环境下的安全使用
- 支持自定义自增步长，满足不同场景的需求

### 双标识符设计

- **CounterName**：恒定不变，用于持久化，跨存档保持一致
- **UID**：运行时生成，用于实例查询，每次启动都不同

### 数据持久化

- 将计数数据以JSON格式保存到文件
- 支持自动加载历史数据，实现跨会话的状态保持

### 存档隔离

- 每个存档使用独立的管理器实例
- 不同存档指向不同文件路径，计数器数据互不干扰

## 系统架构

### 核心组件

- **Counter类**：核心计数组件，提供自增功能和唯一ID生成
- **CounterHistoryManager类**：管理持久化数据，处理存档和恢复

### 数据流

1. 创建管理器实例 → 2. 创建计数器实例 → 3. 使用计数器生成ID → 4. 保存数据 → 5. 释放资源

## 快速开始

### 基础使用（无持久化）

```csharp
// 创建一个简单的计数器，不关联管理器
Counter counter = new Counter("PlayerID");

// 获取唯一ID
long playerId = counter.GetAndIncrement();
Console.WriteLine($"生成的玩家ID: {playerId}");

// 释放资源
counter.Dispose();
```

### 带持久化的实体ID分配

```csharp
// 创建管理器实例，指定文件路径
CounterHistoryManager manager = new CounterHistoryManager("save1/counters.json");

// 创建计数器并关联管理器
Counter enemyCounter = new Counter("EnemyID", manager);

// 为敌人分配ID
for (int i = 0; i < 10; i++)
{
    long enemyId = enemyCounter.GetAndIncrement();
    Console.WriteLine($"生成的敌人ID: {enemyId}");
}

// 保存数据
manager.Save();

// 释放资源
enemyCounter.Dispose();
```

### 多存档场景

```csharp
// 为不同存档创建独立的管理器
CounterHistoryManager save1Manager = new CounterHistoryManager("save1/counters.json");
CounterHistoryManager save2Manager = new CounterHistoryManager("save2/counters.json");

// 在存档1中使用计数器
Counter save1Counter = new Counter("ItemID", save1Manager);
long itemId1 = save1Counter.GetAndIncrement();
save1Manager.Save();
save1Counter.Dispose();

// 在存档2中使用计数器
Counter save2Counter = new Counter("ItemID", save2Manager);
long itemId2 = save2Counter.GetAndIncrement();
save2Manager.Save();
save2Counter.Dispose();

// 两个存档的计数器独立运行
Console.WriteLine($"存档1的物品ID: {itemId1}");
Console.WriteLine($"存档2的物品ID: {itemId2}");
```

## API参考

### Counter类

#### 属性

- **CounterName**：计数器名称，恒定不变的唯一标识符，用于持久化
- **CounterUID**：运行时唯一标识符，用于实例查询，每次创建都不同
- **Manager**：关联的历史数据管理器实例，可为null
- **IncrementStep**：自增幅度，默认为1，负值取绝对值，超过最大值则设为1
- **CountValue**：当前计数值（下一个待使用的序号），不推荐直接读取

#### 方法

- **GetAndIncrement()**：返回当前值并按步长自增，推荐使用此方法获取唯一标识符
- **GetAndIncrementOne()**：返回当前值并自增1
- **Increment()**：仅自增，不返回值
- **IncrementOne()**：自增1，不返回值
- **GetValue()**：获取当前计数值，不推荐使用
- **SetValue(value)**：设置计数值，会破坏自增语义，可能导致ID重复，谨慎使用
- **Dispose()**：释放资源，从管理器和实例映射表中注销
- **GetCounterByUID(uid)**：静态方法，通过UID查询计数器实例

### CounterHistoryManager类

#### 属性

- **RegisteredCount**：已注册的计数器数量
- **HistoryDataCount**：历史数据条目数量

#### 方法

- **Save()**：保存所有已注册计数器的当前值到文件，返回bool
- **Register(counter)**：注册计数器，通常由Counter构造函数自动调用，返回bool
- **Unregister(counterName)**：注销计数器，通常由Dispose自动调用，返回bool
- **HasHistoryData(counterName)**：检查是否存在历史数据，返回bool
- **GetHistoryValue(counterName)**：获取历史值，无数据则返回long.MinValue
- **Clear()**：清空所有数据

## 最佳实践

1. **推荐使用GetAndIncrement()**：而非直接读取CountValue，确保ID的唯一性和连续性
2. **每个存档使用独立的管理器实例**：确保不同存档的计数器数据互不干扰
3. **在适当时机调用Save()**：如游戏存档或退出时，确保数据持久化
4. **不再使用的计数器应调用Dispose()**：释放资源，避免内存泄漏
5. **Manager参数可为null**：此时计数器不进行持久化，适用于临时计数场景

## 注意事项

### long.MinValue的特殊语义

- 不作为有效序号
- GetHistoryValue()返回此值表示「无历史数据」
- 计数器初始化时检测到此值则使用long.MinValue + 1

### Manager = null

- 管理器参数可为null，此时计数器不进行持久化
- 适用于临时计数场景，如单次会话内的临时ID分配

### CountValue

- CountValue中的值是"下一个待使用"的序号，直接读取通常无意义
- 推荐使用GetAndIncrement()获取当前值并自增

### SetValue()

- SetValue()会破坏自增语义，可能导致ID重复
- 仅在特殊场景使用，如系统重置或数据迁移

### Save()

- 记得在适当的时机调用Save()保存数据
- 频繁调用可能影响性能，建议在关键节点调用

### Dispose()

- 计数器不再使用时应调用Dispose()释放资源
- 从管理器注册表和全局实例映射表中移除

## 技术特点分析

### 线程安全实现

- 使用锁保护所有读写操作，确保在多线程环境下的安全使用
- 适用于游戏开发中的并行场景，如异步加载和多线程处理

### 持久化机制

- 使用JSON格式存储数据，易于阅读和调试
- 自动处理文件不存在的情况，创建空表

### 存档隔离方案

- 通过文件路径隔离不同存档的数据
- 每个存档使用独立的管理器实例，避免数据混淆

### 性能优化考虑

- 最小化锁的范围，减少线程阻塞
- 延迟加载历史数据，提高初始化速度

## 应用场景

### 游戏开发中的实体ID分配

- 玩家角色ID
- 敌人和NPC ID
- 物品和装备ID

### 资源计数和管理

- 资源采集计数
- 任务完成计数
- 成就解锁计数

### 其他场景

- 任何需要唯一标识符生成的系统
- 需要持久化计数状态的应用

## 总结与评价

### 系统优势

- **可靠性**：线程安全设计确保在多线程环境下的稳定运行
- **灵活性**：双标识符设计和自定义步长满足不同场景需求
- **可扩展性**：存档隔离方案支持复杂的多存档系统
- **易用性**：简洁的API设计，易于集成到现有项目

### 潜在改进空间

- **性能优化**：对于高频率ID生成场景，可考虑无锁设计
- **序列化选项**：支持更多序列化格式，如二进制格式提高性能
- **分布式支持**：扩展到分布式环境的ID生成

### 适用范围评估

- **最适用于**：游戏开发中的ID分配和计数管理
- **适用于**：任何需要唯一标识符生成的C#项目
- **注意**：对于极高并发场景，可能需要进一步优化

## 版本信息

### 核心功能版本

- 线程安全自增计数
- 数据持久化
- 存档隔离

### 技术栈

- C#语言
- JSON序列化
- 线程安全设计

## 结语

计数器系统提供了一种简单而强大的方法来生成和管理唯一标识符，特别适合游戏开发中的各种计数需求。通过合理使用其API和遵循最佳实践，您可以构建更加可靠和可维护的游戏系统。