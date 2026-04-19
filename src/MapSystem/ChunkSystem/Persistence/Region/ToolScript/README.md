# Region 检测脚本设计

## 目标

这组脚本用于扫描指定路径下的 `ChunkRegion` 文件，并输出：

- 已加载区块数量
- 总占用大小
- 实际数据大小
- 分区内无效数据大小
- 写入分区内未写入区域大小
- 前缀无效数据大小
- 空闲分区大小
- 无效数据占总占用比例
- 实际数据占总占用比例
- 分区无效数据比重
- 分区有效数据比重
- 空闲分区数量
- 空闲分区链条可视化
- 空闲分区占分区无效数据比重
- 写入分区内未写入区域占分区无效数据比重

实现必须严格镜像 `src/MapSystem/ChunkSystem/Persistence/Region` 下的 C# 语义，而不是“按感觉猜布局”。

## Region 文件布局

文件固定分成四段：

1. `Format Area`：`2048` 字节
2. `Introduction Area`：`2048` 字节
3. `Header Area`：`24576` 字节
4. `Partition Area`：`N * 4096` 字节

因此，分区区之前的固定前缀总大小为：

- `2048 + 2048 + 24576 = 28672` 字节

这部分始终占磁盘空间，但不是 chunk 负载数据，所以脚本里记为：

- `前缀无效数据大小`

## Header 结构

一个 region 固定覆盖 `32 * 32 = 1024` 个 chunk 槽位。

每个 chunk header 固定 `18` 字节：

- `uint32 firstPartitionIndex`
- `uint16 lastPartitionDataLength`
- `uint32 partitionCount`
- `int64 timestamp`

空 header 的判定条件和 C# 完全一致：

- `firstPartitionIndex = uint.MaxValue`
- `lastPartitionDataLength = 0`
- `partitionCount = 0`

Header 区末尾还有两个全局字段：

- `headFreePartitionIndex`
- `freePartitionCount`

## Partition 结构

每个 partition 固定 `4096` 字节：

- 前 `4` 字节：`next`
- 后 `4092` 字节：payload

哨兵值 `0xFFFFFFFF` 表示“没有下一跳”。

## 新链 / 旧链 / 空闲链 的真实逻辑

### 新链写入流程

`ChunkRegionWriter.SaveChunkStorage` 的核心顺序是：

1. 先读取当前 chunk header，把它视为旧链
2. 序列化并压缩新的 chunk 数据
3. 计算新链需要多少 partition
4. 为新链分配 partition
5. 把整条新链完整写入
6. `Flush`
7. 覆盖 chunk header，让 header 指向新链
8. `Flush`
9. 回收旧链，把旧链挂入空闲链
10. `Flush`

这意味着：

- 读流程永远只认 header
- 在第 7 步之前，读者仍然只能看到旧链
- 第 7 步之后，读者才切到新链

### 这套顺序的意义

如果进程中途崩溃：

- 崩在第 7 步之前：
  旧链仍然是正式提交的数据，新数据还没提交，只会浪费刚写进去的新链空间
- 崩在第 7 步之后、第 9 步之前：
  新数据已经提交成功，但旧链可能还没被回收到空闲链，会形成泄漏分区

也就是说，这套设计优先保证“已提交数据可读”，然后才考虑空间回收。

### 新链分配策略

分配逻辑刻意不混用“部分空闲链 + 部分尾部新分区”。

规则只有两条：

- 空闲链数量足够：整条新链全部从空闲链取
- 空闲链数量不够：整条新链全部从文件尾部追加

这样做的好处是：

- 状态机更简单
- 崩溃面更小
- 回滚推理更清楚

### 旧链回收策略

旧链回收时不是尾插，而是逐个头插到空闲链。

所以空闲链表现为 LIFO 风格，并且旧链回收后会反序挂入空闲链。

示例：

- 旧链：`1 -> 2 -> 3`
- 原空闲链头：`10`
- 回收后空闲链：`3 -> 2 -> 1 -> 10`

## 指标口径

### 总占用大小

- `总占用大小 = 文件大小`

### 实际数据大小

对每条有效 chunk 链：

- `实际数据大小 = (partitionCount - 1) * 4092 + lastPartitionDataLength`

所有活动 chunk 链累加后，就是 region 的：

- `实际数据大小`

### 分区内无效数据大小

定义为：

- `分区内无效数据大小 = 分区区总大小 - 实际数据大小`

它包含：

- 活动分区的 `next` 指针开销
- 每条活动链最后一个 partition 中未写满的 slack
- 空闲分区
- 如果文件损坏时可能出现的孤儿/泄漏/重叠分区

### 细分指标

- `前缀无效数据大小 = 28672`
- `空闲分区大小 = freePartitionCount * 4096`
- `写入分区内未写入区域大小 = activePartitionCount * 4092 - 实际数据大小`
- `活动分区 next 指针大小 = activePartitionCount * 4`

对健康文件，通常满足：

- `分区内无效数据大小`
  =
  `空闲分区大小 + 写入分区内未写入区域大小 + 活动分区 next 指针大小`

如果不相等，差值一般意味着：

- 孤儿分区
- 泄漏分区
- 重叠引用
- 结构损坏

### 比例指标

- `无效数据占总占用比例 = 总无效数据 / 总占用大小`
- `实际数据占总占用比例 = 实际数据 / 总占用大小`
- `分区无效数据比重 = 分区内无效数据 / 分区区总大小`
- `分区有效数据比重 = 实际数据 / 分区区总大小`
- `空闲分区占分区无效数据比重 = 空闲分区大小 / 分区内无效数据大小`
- `写入分区内未写入区域占分区无效数据比重 = 写入分区内未写入区域大小 / 分区内无效数据大小`

## 脚本架构

### 核心模块

- `region_layout.ts`
  镜像 C# 常量、固定尺寸、偏移和哨兵值
- `region_types.ts`
  定义解析结果、指标结果和 CLI 参数类型
- `region_parser.ts`
  负责二进制解析、格式校验、空闲链遍历、chunk 链遍历
- `region_metrics.ts`
  负责把解析结果汇总成用户需要的统计指标
- `region_i18n.ts`
  负责多语言文案
- `region_render.ts`
  负责人类可读输出、emoji 可视化和链条展示

### CLI 脚本

- `region_inspector.ts`
  主入口，输出完整可视化报告
- `region_metrics_json.ts`
  输出 JSON，方便后续接别的工具
- `region_free_chain.ts`
  聚焦空闲链结构和可视化

## 编码约定

region 文件本身是二进制文件，不能整体按 UTF-8 文本读取。

正确做法是：

- 整个文件按二进制 `Buffer` 读取
- 其中的文本区段，例如：
  - format JSON
  - introduction signature
  - create time prefix
  再显式按 UTF-8 解码

脚本实现会显式使用 UTF-8 解码这些文本区域，不依赖默认编码。

## 当前严格性

脚本校验策略会尽量贴近 C# 运行时假设：

- format JSON 必须匹配标准格式语义
- introduction signature 必须匹配
- 分区区长度必须按 `4096` 对齐
- 空闲链必须在已分配分区范围内
- chunk 链必须严格匹配 header 声明的 partitionCount
- 最后一个 partition 必须以哨兵结束
- 重复引用、越界引用、提前结束都会明确报出

如果发现损坏，脚本仍会尽力给出统计结果，但会把异常单独列出来，不会静默吞掉。
