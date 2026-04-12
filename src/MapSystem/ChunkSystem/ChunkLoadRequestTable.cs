using System;
using System.Collections;
using System.Collections.Generic;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.State;
using WorldWeaver.PixelShapeSystem;
using WorldWeaver.PixelShapeSystem.PointsShape;
using WorldWeaver.PixelShapeSystem.ValueShape;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块加载请求表。
    /// <para>该对象是“完整快照”语义：它描述当前时刻希望哪些区块到达哪个目标稳定状态。</para>
    /// <para>凡是不在请求表中的区块，都应被视为“不再被当前请求需要”，后续会在更新表中被转译为目标状态 <see cref="ChunkStateNode.Exit"/>。</para>
    /// <para>内部使用 <see cref="IPixelValuesShape{T}"/> 持有“区块坐标点序 + 目标稳定状态数组”，但对外暴露为语义化请求表对象。</para>
    /// </summary>
    public sealed class ChunkLoadRequestTable : IEnumerable<(ChunkPosition ChunkPosition, ChunkStateNode TargetStableNode)>
    {
        // ================================================================================
        //                                  核心字段
        // ================================================================================

        /// <summary>
        /// 内部承载请求点序与目标稳定状态数组的值形状。
        /// </summary>
        private readonly IPixelValuesShape<ChunkStateNode> _requestShape;


        // ================================================================================
        //                                  静态空对象
        // ================================================================================

        /// <summary>
        /// 空请求表。
        /// </summary>
        public static readonly ChunkLoadRequestTable EMPTY = new(new PointListShape(), Array.Empty<ChunkStateNode>());


        // ================================================================================
        //                                  语义化属性
        // ================================================================================

        /// <summary>
        /// 当前请求表是否携带目标稳定状态数组。
        /// </summary>
        public bool HasTargetStableNodes => _requestShape.HasValues();

        /// <summary>
        /// 当前请求表是否处于有效状态。
        /// </summary>
        public bool IsValid()
        {
            return _requestShape.IsAligned();
        }


        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建区块加载请求表。
        /// <para>该构造函数为私有入口，外部应统一走静态工厂完成校验与错误处理。</para>
        /// </summary>
        private ChunkLoadRequestTable(PixelShape shape, ChunkStateNode[] targetStableNodes)
        {
            _requestShape = new PixelValuesArrayShape<ChunkStateNode>(shape, targetStableNodes);
        }

        /// <summary>
        /// 使用现有像素值形状创建区块加载请求表。
        /// <para>该构造函数会直接复用传入的值形状对象，不再额外创建新的像素值形状实例。</para>
        /// </summary>
        private ChunkLoadRequestTable(IPixelValuesShape<ChunkStateNode> valueShape)
        {
            _requestShape = valueShape;
        }

        /// <summary>
        /// 使用底层像素形状与目标稳定状态数组创建请求表。
        /// <para>若输入无效，则输出错误并返回 <see langword="null"/>。</para>
        /// </summary>
        public static ChunkLoadRequestTable Create(PixelShape shape, ChunkStateNode[] targetStableNodes)
        {
            // 工厂方法统一承担输入校验职责；无效时不抛异常，而是返回 null。
            if (shape == null)
            {
                GD.PushError("[ChunkSystem/ChunkLoadRequestTable]: Create(PixelShape, ChunkStateNode[]) 调用失败，shape 不能为空。");
                return null;
            }

            if (targetStableNodes == null)
            {
                GD.PushError("[ChunkSystem/ChunkLoadRequestTable]: Create(PixelShape, ChunkStateNode[]) 调用失败，targetStableNodes 不能为空。");
                return null;
            }

            if (shape.PointCount != targetStableNodes.Length)
            {
                GD.PushError($"[ChunkSystem/ChunkLoadRequestTable]: Create(PixelShape, ChunkStateNode[]) 调用失败，shape.PointCount={shape.PointCount} 与 targetStableNodes.Length={targetStableNodes.Length} 不一致。");
                return null;
            }

            return new ChunkLoadRequestTable(shape, targetStableNodes);
        }

        /// <summary>
        /// 使用底层像素形状与目标稳定状态列表创建请求表。
        /// <para>若输入无效，则输出错误并返回 <see langword="null"/>。</para>
        /// </summary>
        public static ChunkLoadRequestTable Create(PixelShape shape, List<ChunkStateNode> targetStableNodes)
        {
            if (shape == null)
            {
                GD.PushError("[ChunkSystem/ChunkLoadRequestTable]: Create(PixelShape, List<ChunkStateNode>) 调用失败，shape 不能为空。");
                return null;
            }

            if (targetStableNodes == null)
            {
                GD.PushError("[ChunkSystem/ChunkLoadRequestTable]: Create(PixelShape, List<ChunkStateNode>) 调用失败，targetStableNodes 不能为空。");
                return null;
            }

            if (shape.PointCount != targetStableNodes.Count)
            {
                GD.PushError($"[ChunkSystem/ChunkLoadRequestTable]: Create(PixelShape, List<ChunkStateNode>) 调用失败，shape.PointCount={shape.PointCount} 与 targetStableNodes.Count={targetStableNodes.Count} 不一致。");
                return null;
            }

            return new ChunkLoadRequestTable(shape, targetStableNodes.ToArray());
        }

        /// <summary>
        /// 使用像素值形状创建请求表。
        /// <para>若输入无效，则输出错误并返回 <see langword="null"/>。</para>
        /// </summary>
        public static ChunkLoadRequestTable Create(IPixelValuesShape<ChunkStateNode> valueShape)
        {
            if (valueShape == null)
            {
                GD.PushError("[ChunkSystem/ChunkLoadRequestTable]: Create(IPixelValuesShape<ChunkStateNode>) 调用失败，valueShape 不能为空。");
                return null;
            }

            if (!valueShape.IsAligned())
            {
                GD.PushError($"[ChunkSystem/ChunkLoadRequestTable]: Create(IPixelValuesShape<ChunkStateNode>) 调用失败，valueShape.ValueCount={valueShape.ValueCount} 与 valueShape.Shape.PointCount={valueShape.Shape.PointCount} 不一致。");
                return null;
            }

            return new ChunkLoadRequestTable(valueShape);
        }


        // ================================================================================
        //                                  迭代方法
        // ================================================================================

        /// <summary>
        /// 获取区块请求迭代器。
        /// <para>调用方可以直接对 <see cref="ChunkLoadRequestTable"/> 使用 <c>foreach</c>。</para>
        /// </summary>
        public IEnumerator<(ChunkPosition ChunkPosition, ChunkStateNode TargetStableNode)> GetEnumerator()
        {
            foreach ((Vector2I globalChunkPosition, ChunkStateNode targetStableNode) in _requestShape.GetGlobalValueIterator())
            {
                yield return (new ChunkPosition(globalChunkPosition), targetStableNode);
            }
        }

        /// <summary>
        /// 获取非泛型迭代器。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
