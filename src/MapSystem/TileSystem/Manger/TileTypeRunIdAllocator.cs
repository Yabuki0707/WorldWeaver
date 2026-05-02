using Godot;

namespace WorldWeaver.MapSystem.TileSystem.Manger
{
    /// <summary>
    /// TileType 运行 ID 分配器。
    /// <para>分配器只持有当前最小可用运行 ID，以及构造时传入的初始最小可用运行 ID。</para>
    /// <para>运行 ID 通过重载的 <c>++</c> 运算符推进；转换为 <see cref="int"/> 时表示当前 <see cref="MinAvailableRunId"/>。</para>
    /// </summary>
    internal sealed class TileTypeRunIdAllocator
    {
        /// <summary>
        /// 创建 TileType 运行 ID 分配器。
        /// </summary>
        /// <param name="initialMinAvailableRunId">初始最小可用运行 ID。</param>
        public TileTypeRunIdAllocator(int initialMinAvailableRunId = 1)
        {
            if (initialMinAvailableRunId < 1)
            {
                GD.PushWarning($"[TileTypeRunIdAllocator] 初始最小可用运行 ID 被设定为 {initialMinAvailableRunId}，小于 1；如果不是刻意设计，请注意修正。");
            }

            InitialMinAvailableRunId = initialMinAvailableRunId;
            MinAvailableRunId = initialMinAvailableRunId;
        }

        /// <summary>
        /// 获取初始最小可用运行 ID。
        /// </summary>
        public int InitialMinAvailableRunId { get; private set; }

        /// <summary>
        /// 获取当前最小可用运行 ID。
        /// </summary>
        public int MinAvailableRunId { get; private set; }

        /// <summary>
        /// 获取已经分配出去的 TileType 数量。
        /// <para>数量由当前最小可用运行 ID 与初始最小可用运行 ID 的差值计算得到。</para>
        /// </summary>
        public int TypeCount => MinAvailableRunId - InitialMinAvailableRunId;

        /// <summary>
        /// 将分配器隐式转换为当前最小可用运行 ID。
        /// </summary>
        /// <param name="allocator">TileType 运行 ID 分配器。</param>
        public static implicit operator int(TileTypeRunIdAllocator allocator)
        {
            if (allocator == null)
            {
                GD.PushWarning("[TileTypeRunIdAllocator] 尝试将为null的空分配器转换为运行 ID，已返回 int.MinValue。");
                return int.MinValue;
            }

            return allocator.MinAvailableRunId;
        }

        /// <summary>
        /// 通过 <c>++</c> 运算符推进最小可用运行 ID。
        /// <para>运算符返回推进后的新分配器；配合后置 <c>++</c> 使用时，可以通过隐式 <see cref="int"/> 转换取得递增前的运行 ID。</para>
        /// </summary>
        /// <param name="allocator">需要推进的运行 ID 分配器。</param>
        /// <returns>推进后的运行 ID 分配器。</returns>
        public static TileTypeRunIdAllocator operator ++(TileTypeRunIdAllocator allocator)
        {
            if (allocator == null)
            {
                return null;
            }

            // 返回一个新实例，让后置 ++ 的表达式结果保留递增前的 MinAvailableRunId。
            return new TileTypeRunIdAllocator(allocator.InitialMinAvailableRunId)
            {
                MinAvailableRunId = allocator.MinAvailableRunId + 1
            };
        }
    }
}
