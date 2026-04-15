using System;
using System.Collections;
using System.Collections.Generic;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.State;
using WorldWeaver.PixelShapeSystem.PointsShape;
using WorldWeaver.PixelShapeSystem.ValueShape;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块加载请求表。
    /// <para>该对象保存“区块坐标点序 + 目标稳定节点值序”的完整请求快照。</para>
    /// </summary>
    public sealed class ChunkLoadRequestTable : IEnumerable<(ChunkPosition ChunkPosition, ChunkStateNode TargetStableNode)>
    {
        // ================================================================================
        //                                  核心字段
        // ================================================================================

        /// <summary>
        /// 内部承载请求点序与目标稳定状态值序的形状。
        /// </summary>
        private readonly IPixelValuesShape<ChunkStateNode> _requestShape;


        // ================================================================================
        //                                  静态对象
        // ================================================================================

        /// <summary>
        /// 空请求表。
        /// </summary>
        public static readonly ChunkLoadRequestTable EMPTY = new(
            new PixelValuesArrayShape<ChunkStateNode>(new PointListShape(), Array.Empty<ChunkStateNode>()));


        // ================================================================================
        //                                  构造方法
        // ================================================================================

        /// <summary>
        /// 创建区块加载请求表。
        /// <para>构造函数只接受已经通过公开工厂校验与清洗的值形状。</para>
        /// </summary>
        private ChunkLoadRequestTable(IPixelValuesShape<ChunkStateNode> requestShape)
        {
            _requestShape = requestShape;
        }


        // ================================================================================
        //                                  工厂方法
        // ================================================================================

        /// <summary>
        /// 使用数组值形状创建请求表。
        /// <para>输入必须携带值数组，且点数量和值数量必须对齐。</para>
        /// </summary>
        public static ChunkLoadRequestTable Create(PixelValuesArrayShape<ChunkStateNode> requestShape)
        {
            if (requestShape == null)
            {
                GD.PushError("[ChunkSystem/ChunkLoadRequestTable]: Create(PixelValuesArrayShape<ChunkStateNode>) 调用失败，requestShape 不能为空。");
                return null;
            }

            if (!requestShape.HasValues())
            {
                GD.PushError("[ChunkSystem/ChunkLoadRequestTable]: Create(PixelValuesArrayShape<ChunkStateNode>) 调用失败，requestShape 必须携带目标稳定节点值数组。");
                return null;
            }

            if (!requestShape.IsAligned())
            {
                GD.PushError($"[ChunkSystem/ChunkLoadRequestTable]: Create(PixelValuesArrayShape<ChunkStateNode>) 调用失败，requestShape.ValueCount={requestShape.ValueCount} 与 requestShape.Shape.PointCount={requestShape.Shape.PointCount} 不一致。");
                return null;
            }

            // 请求目标节点值数组，合法时直接复用，不复制。
            ChunkStateNode[] values = requestShape.Values;

            // 当前检查的值索引。
            for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
            {
                // 当前检查的目标稳定节点。
                ChunkStateNode targetStableNode = values[valueIndex];
                if (ChunkStateMachine.IsStable(targetStableNode))
                {
                    continue;
                }

                (ChunkStateNode[] sanitizedValues, string invalidNodeNames) = Sanitize(values, valueIndex);
                GD.PushError($"[ChunkSystem/ChunkLoadRequestTable]: Create(PixelValuesArrayShape<ChunkStateNode>) 检测到非稳定目标节点，已将其替换为 Exit。非法节点: {invalidNodeNames}");
                return new ChunkLoadRequestTable(
                    new PixelValuesArrayShape<ChunkStateNode>(requestShape.Shape, sanitizedValues));
            }

            return new ChunkLoadRequestTable(requestShape);
        }

        /// <summary>
        /// 使用列表值形状创建请求表。
        /// <para>输入必须携带值列表，且点数量和值数量必须对齐。</para>
        /// </summary>
        public static ChunkLoadRequestTable Create(PixelValuesListShape<ChunkStateNode> requestShape)
        {
            if (requestShape == null)
            {
                GD.PushError("[ChunkSystem/ChunkLoadRequestTable]: Create(PixelValuesListShape<ChunkStateNode>) 调用失败，requestShape 不能为空。");
                return null;
            }

            if (!requestShape.HasValues())
            {
                GD.PushError("[ChunkSystem/ChunkLoadRequestTable]: Create(PixelValuesListShape<ChunkStateNode>) 调用失败，requestShape 必须携带目标稳定节点值列表。");
                return null;
            }

            if (!requestShape.IsAligned())
            {
                GD.PushError($"[ChunkSystem/ChunkLoadRequestTable]: Create(PixelValuesListShape<ChunkStateNode>) 调用失败，requestShape.ValueCount={requestShape.ValueCount} 与 requestShape.Shape.PointCount={requestShape.Shape.PointCount} 不一致。");
                return null;
            }

            // 请求目标节点值列表，合法时直接复用，不复制。
            List<ChunkStateNode> values = requestShape.Values;

            // 当前检查的值索引。
            for (int valueIndex = 0; valueIndex < values.Count; valueIndex++)
            {
                // 当前检查的目标稳定节点。
                ChunkStateNode targetStableNode = values[valueIndex];
                if (ChunkStateMachine.IsStable(targetStableNode))
                {
                    continue;
                }

                // 列表形态只有在需要清洗时才转换为数组。
                ChunkStateNode[] valueArray = values.ToArray();
                (ChunkStateNode[] sanitizedValues, string invalidNodeNames) = Sanitize(valueArray, valueIndex);
                GD.PushError($"[ChunkSystem/ChunkLoadRequestTable]: Create(PixelValuesListShape<ChunkStateNode>) 检测到非稳定目标节点，已将其替换为 Exit。非法节点: {invalidNodeNames}");
                return new ChunkLoadRequestTable(
                    new PixelValuesArrayShape<ChunkStateNode>(requestShape.Shape, sanitizedValues));
            }

            return new ChunkLoadRequestTable(requestShape);
        }


        // ================================================================================
        //                                  值数组处理
        // ================================================================================

        /// <summary>
        /// 从指定非法索引开始清洗目标节点数组。
        /// <para>该方法会先复制整份数组，再从 <paramref name="firstInvalidIndex"/> 开始把所有非稳定节点修正为 <see cref="ChunkStateNode.Exit"/>。</para>
        /// </summary>
        private static (ChunkStateNode[] SanitizedValues, string InvalidNodeNames) Sanitize(
            ChunkStateNode[] values,
            int firstInvalidIndex)
        {
            ChunkStateNode[] sanitizedValues = new ChunkStateNode[values.Length];
            Array.Copy(values, sanitizedValues, values.Length);

            // 存放本次检测到的所有非稳定节点，最后统一输出错误。
            string invalidNodeNames = string.Empty;

            // 当前清洗的值索引。
            for (int valueIndex = firstInvalidIndex; valueIndex < sanitizedValues.Length; valueIndex++)
            {
                // 当前清洗检查的目标稳定节点。
                ChunkStateNode targetStableNode = sanitizedValues[valueIndex];
                if (ChunkStateMachine.IsStable(targetStableNode))
                {
                    continue;
                }

                if (invalidNodeNames.Length > 0)
                {
                    invalidNodeNames += ",";
                }

                invalidNodeNames += $"{targetStableNode}[{valueIndex}]";
                sanitizedValues[valueIndex] = ChunkStateNode.Exit;
            }

            return (sanitizedValues, invalidNodeNames);
        }


        // ================================================================================
        //                                  迭代方法
        // ================================================================================

        /// <summary>
        /// 获取区块请求迭代器。
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
