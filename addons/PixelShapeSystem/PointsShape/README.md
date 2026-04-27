# PointsShape

## 设计理念

`PointsShape` 系统用于表达一组离散的全局像素坐标。它不关心这些坐标最终会写入哪个地图区块、哪个数组索引或哪个渲染层，只负责稳定、明确、高效地描述“这个形状覆盖了哪些点”。

该系统的核心使用理念是：先明确点数据的语义，再选择对应的数据结构。

- 已在外部保证无重复点，或业务上不在意重复点，并希望跳过内部去重成本时，使用 `PointSequenceShape` 或 `MutablePointSequenceShape`。
- 需要表达唯一覆盖范围、杜绝重复点时，使用 `PointSetShape` 或 `MutablePointSetShape`。
- 数据构造完成后不再变化时，使用静态形状。
- 数据需要持续收集、追加或增量生成时，使用动态形状。

点序列与点集合不尽是性能上的差异，更是责任边界上的差异。
序列形状相信调用方已经处理好重复点问题，或明确接受重复点带来的结果，因此内部不再花成本判重；
集合形状把“同一个坐标只代表一个有效覆盖点”作为自身责任。调用方应根据重复点责任归属选择类型，而不是在外部临时补去重或补排序。

序列形状会保留输入顺序，也天然适合与外部值数组一一对齐，但这通常是“无内部去重”带来的良性副作用。只有当业务确实需要这些副作用时，它们才应成为选择序列形状的使用理由。

空形状使用错误边界表达“无有效点”，而不是使用一个看似合法的零大小零坐标边界。这样可以让空语义在边界传播时更加清晰，避免把“没有点”误读成“原点上存在一个零尺寸形状”。

## 核心技术点

### 坐标输出契约

所有点形状都继承自 `PixelShape`，并提供以下输出方式：

- `GetGlobalCoordinateIterator()`：流式遍历坐标，适合普通消费路径。
- `GetGlobalCoordinateList()`：返回坐标列表副本，适合需要可变列表的调用方。
- `GetGlobalCoordinateArray()`：返回坐标数组副本，适合需要稳定数组快照的调用方。
- `GetGlobalCoordinateSpan()`：返回只读坐标切片，适合高性能读取路径。

`GetGlobalCoordinateSpan()` 是 `PointsShape` 额外提供的零复制读取接口。静态形状直接暴露内部数组的只读视图，动态形状基于内部 `List<Vector2I>` 创建只读 span。调用方应把它视为短生命周期读取视图，不应跨越后续写入操作长期保存。

### 边界缓存语义

点形状的边界使用四个缓存值描述：

- `minX`
- `maxX`
- `minY`
- `maxY`

最小边界的哨兵值为 `int.MaxValue`，最大边界的哨兵值为 `int.MinValue`。空形状的公共边界为：

```csharp
new Rect2I(
    new Vector2I(int.MaxValue, int.MaxValue),
    new Vector2I(int.MinValue, int.MinValue))
```

该边界是故意构造出的错误边界，用来表达空形状语义。

`ExpandCoordinateBounds` 服务于高速写入路径。它在每个坐标轴上只执行一次 `if / else if` 分支更新，因此允许出现半边界状态。例如 `minX` 已经被更新为正常坐标，而 `maxX` 仍保留 `int.MinValue` 哨兵值。这种状态是设计范围内的中间状态。

`CreateCoordinateBounds` 服务于最终边界创建路径。它会在创建 `Rect2I` 前检查每个轴的 min/max 哨兵状态：

- 若 min 正常、max 仍为哨兵，则 max 兜底为 min。
- 若 min 仍为哨兵、max 正常，则 min 兜底为 max。
- 若 min 与 max 都仍为哨兵，则保留错误边界语义。

因此，写入阶段可以保持轻量，读取边界时再统一兜底。

### 边界大小语义

`CoordinateBounds.Size` 表示 `Max - Min`，不是离散点宽高数量。

例如：

- 单点 `(4, 7)` 的边界为 `Position=(4, 7)`，`Size=(0, 0)`。
- 覆盖从 `(1, 1)` 到 `(3, 3)` 的边界为 `Position=(1, 1)`，`Size=(2, 2)`。

这与 `PixelShape` 的基础契约保持一致：最大边界坐标满足 `Max = Position + CoordinateBounds.Size`。

### 去重键设计

点集合形状使用 `HashSet<long>` 执行去重。`Vector2I.ToKey()` 会将二维整数坐标压缩为 64 位键：

```csharp
((long)point.X << 32) | (uint)point.Y
```

高 32 位保存 X，低 32 位保存 Y。这样可以避免为 `Vector2I` 构造额外包装对象，并让集合判重使用简单的整数键。

### 输出顺序稳定性

所有点形状都保证公开输出顺序稳定：

- 序列形状按输入顺序输出。
- 集合形状按首次接受该点的顺序输出。
- 静态形状构造完成后输出顺序固定。
- 动态形状按写入顺序输出已接受点。

这条规则对值形状非常重要，因为 `PixelValuesArrayShape<T>` 这类结构需要坐标顺序与值数组顺序保持一一对应。

## 性能优化点

### 静态形状只保留最终数组

`PointSequenceShape` 与 `PointSetShape` 在构造完成后只持有 `Vector2I[]`。静态点集合只在构造阶段临时使用 `HashSet<long>` 判重，构造完成后不再保留判重缓存。

这让静态形状适合“构造一次、读取多次”的场景，常驻内存只包含最终坐标数据与边界缓存。

### 动态形状增量维护边界

`MutablePointSequenceShape` 与 `MutablePointSetShape` 在追加点时同步更新边界缓存。读取 `CoordinateBounds` 时不需要重新扫描全部坐标，只需要调用 `CreateCoordinateBounds` 对缓存值做最终归一化。

这对大量点持续写入、频繁读取边界的地图系统很重要。

### 批量追加避免重复单点调用

动态形状的 `AddPoints` 不通过反复调用 `AddPoint` 实现，而是按输入类型选择专用路径：

- `Vector2I[]`：数组索引读取。
- `List<Vector2I>`：通过 `CollectionsMarshal.AsSpan` 读取。
- `ICollection<Vector2I>`：预估容量并按集合规模写入。
- 普通 `IEnumerable<Vector2I>`：退化为流式逐点追加。

这样可以减少批量写入时的虚调用、重复容量检查和中间分配。

### Span 零复制读取

静态形状和动态形状都提供 `GetGlobalCoordinateSpan()`。当调用方只需要短时间顺序读取坐标时，可以避免创建列表或数组副本。

如果调用方需要长期保存结果，应使用 `GetGlobalCoordinateArray()` 或 `GetGlobalCoordinateList()` 获取副本。

### 重复点警告合并

集合形状遇到重复点时不会为每个重复点立即输出一条警告，而是先将重复点追加到同一个 `StringBuilder`，遍历结束后统一输出。

这样既保留了调试信息，又避免批量输入重复点时大量刷屏。

## 系统细致介绍

### `PointsShape`

`PointsShape` 是所有点形状的抽象基类，继承自 `PixelShape`。

它负责定义点形状共同拥有的能力：

- 提供 `GetGlobalCoordinateSpan()` 零复制读取接口。
- 提供 `EmptyCoordinateBounds` 空形状错误边界。
- 提供 `CreateCoordinateBounds()` 边界创建与半边界兜底逻辑。
- 提供 `ExpandCoordinateBounds()` 高速写入边界扩展逻辑。

`PointsShape` 不保存具体点数据，具体存储策略由静态基类与动态基类负责。

### `StaticPointsShapeBase`

`StaticPointsShapeBase` 是静态点形状的公共基类。

它持有：

- `Vector2I[] points`
- `Rect2I coordinateBounds`

静态形状构造完成后，点数组与边界缓存不再变化。它统一实现了迭代器、列表副本、数组副本与 span 输出，具体子类只需要在构造阶段完成点数组与边界的初始化。

适合场景：

- 从已有坐标列表创建一次性结果。
- 与值数组绑定后传递给地图写入或事件系统。
- 高频读取、低频构造的结果对象。

### `DynamicPointsShapeBase`

`DynamicPointsShapeBase` 是动态点形状的公共基类。

它持有：

- `List<Vector2I> points`
- `minX`
- `maxX`
- `minY`
- `maxY`

动态形状允许构造后继续追加点。它统一实现读取接口，并要求子类实现以下写入接口：

- `AddPoint(Vector2I point)`
- `AddPoints(IEnumerable<Vector2I> inputPoints)`
- `AddPoints(Vector2I[] inputPoints)`
- `AddPoints(List<Vector2I> inputPoints)`

适合场景：

- 运行时逐步收集变化点。
- 批量构造尚未确定最终数量的点数据。
- 需要边写入边维护边界的中间结果。

### `PointSequenceShape`

`PointSequenceShape` 是静态点序列形状。

它不执行内部去重，适用于调用方已经在外部保证无重复点，或业务上不在意重复点的静态点数据。构造完成后，它只保存最终 `Vector2I[]` 和边界。

由于不去重，它会原样保留输入顺序，也可以自然保持坐标与外部值数组的写入顺序对齐。这两个特性是可利用的良性副作用，但不是该类型存在的第一设计目的。

适合场景：

- 外部流程已经保证坐标不会重复。
- 重复点即使存在也不会破坏业务语义。
- 需要跳过集合判重成本，构造一个轻量静态结果。
- 需要利用输入顺序或值数组对齐这一副作用。

常见用法：

```csharp
PointSequenceShape shape = new(globalPositions);
```

### `MutablePointSequenceShape`

`MutablePointSequenceShape` 是动态点序列形状。

它支持构造后继续追加点，并且不会执行内部重复性检查。它适用于写入来源已经保证无重复点，或重复点不会影响业务结果的动态点数据。

由于不去重，它会完整保留写入顺序，也能自然与后续外部值数组保持追加顺序一致。这两个特性同样是可利用的良性副作用。

适合场景：

- 写入来源已经保证坐标唯一。
- 重复点即使进入集合也不会破坏业务语义。
- 需要以较低写入成本临时收集大量点。
- 需要利用写入顺序或值数组对齐这一副作用。

常见用法：

```csharp
MutablePointSequenceShape shape = new();
shape.AddPoint(new Vector2I(10, 20));
shape.AddPoints(points);
```

### `PointSetShape`

`PointSetShape` 是静态点集合形状。

它在构造阶段跳过重复点，并保留每个点首次出现的顺序。构造完成后不再持有 `HashSet<long>`，只保留最终唯一点数组。

适合场景：

- 只关心某些坐标是否被覆盖。
- 重复点没有业务意义。
- 构造完成后希望对象尽量轻量。

重复点会被跳过，并通过合并警告输出。

### `MutablePointSetShape`

`MutablePointSetShape` 是动态点集合形状。

它在对象生命周期内常驻一个 `HashSet<long>`，用于保证后续追加点仍然满足集合语义。无论通过构造函数、`AddPoint` 还是 `AddPoints` 写入，重复点都会被跳过。

适合场景：

- 持续收集唯一变化点。
- 多个来源可能写入同一坐标。
- 需要长期维护“已接受点集合”的动态对象。

相比 `MutablePointSequenceShape`，它多维护一个判重集合，写入成本更高，但可以保证输出中不会出现重复坐标。

### 类型选择建议

| 需求 | 推荐类型 |
| --- | --- |
| 外部已保证无重复或不在意重复，构造后不再变化 | `PointSequenceShape` |
| 外部已保证无重复或不在意重复，需要持续追加 | `MutablePointSequenceShape` |
| 自动去重，构造后不再变化 | `PointSetShape` |
| 自动去重，需要持续追加 | `MutablePointSetShape` |

### 与值形状的关系

点形状只描述坐标，不描述坐标对应的值。

当坐标需要绑定值时，应交给 `PixelValuesArrayShape<T>` 等值形状包装。此时必须确保坐标输出顺序与值数组顺序一致。序列形状因为不执行内部去重，所以天然保留输入顺序，适合利用这一副作用完成坐标和值数组对齐；若业务上只允许一个坐标对应一个值，则应在进入值形状前明确使用集合形状去重，或在外部完成去重后再使用序列形状。

### 空形状处理

空点形状的 `PointCount` 为 `0`，坐标输出为空，`CoordinateBounds` 返回错误边界。

调用方应优先通过 `PointCount == 0` 判断空形状，而不是把空边界当作普通矩形参与坐标转换。需要处理边界前，应先确认形状确实包含有效点。
