using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.PixelShapeSystem.ValueShape
{
    /// <summary>
    /// 挂载值列表的像素形状。
    /// <para><see cref="Shape"/> 描述点的空间分布与顺序，<see cref="Values"/> 描述对应顺序上的业务值。</para>
    /// <para>约定：当 <see cref="Values"/> 不为 <see langword="null"/> 时，<c>Values[i]</c> 始终对应 <see cref="Shape"/> 输出序列中的第 <c>i</c> 个点。</para>
    /// </summary>
    /// <typeparam name="T">值列表中元素的类型。</typeparam>
    public class PixelValuesListShape<T> : IPixelValuesShape<T>
    {
        /// <summary>
        /// 底层像素形状。
        /// </summary>
        public PixelShape Shape { get; }

        /// <summary>
        /// 与点序对齐的值列表。
        /// <para>为 <see langword="null"/> 时表示该对象仅承载点形状，不承载值数据。</para>
        /// </summary>
        public List<T> Values { get; }

        /// <summary>
        /// 当前值列表中的值数量。
        /// </summary>
        public int ValueCount => Values?.Count ?? 0;

        /// <summary>
        /// 底层形状的坐标边界范围。
        /// </summary>
        public Rect2I CoordinateBounds => Shape.CoordinateBounds;

        /// <summary>
        /// 创建一个挂载值列表的像素形状。
        /// </summary>
        public PixelValuesListShape(PixelShape shape, List<T> values)
        {
            Shape = shape ?? throw new ArgumentNullException(nameof(shape));
            Values = values;

            if (!IsAligned())
            {
                GD.PushError(
                    $"[PixelShapeSystem/PixelValuesListShape]: 构造失败，Values.Count={Values.Count} 与 Shape.PointCount={Shape.PointCount} 不一致。");
            }
        }

        /// <summary>
        /// 当前对象是否携带值列表。
        /// </summary>
        public bool HasValues()
        {
            return Values != null;
        }

        /// <summary>
        /// 点数量与值数量是否对齐。
        /// </summary>
        public bool IsAligned()
        {
            return Values == null || Shape.PointCount == Values.Count;
        }

        /// <summary>
        /// 转换为数组值形状。
        /// <para>该方法会复制值容器，避免转换后的对象与原对象共享同一个可变值集合。</para>
        /// </summary>
        public PixelValuesArrayShape<T> ToArrayShape()
        {
            return new PixelValuesArrayShape<T>(
                Shape,
                Values == null ? null : Values.ToArray());
        }

        /// <summary>
        /// 获取全局坐标与对应值的配对迭代器。
        /// </summary>
        public IEnumerable<(Vector2I GlobalPosition, T Value)> GetGlobalValueIterator()
        {
            if (!HasValues())
            {
                GD.PushError("[PixelShapeSystem/PixelValuesListShape]: GetGlobalValueIterator 调用失败，当前对象未携带 Values。");
                yield break;
            }

            if (!IsAligned())
            {
                GD.PushError(
                    $"[PixelShapeSystem/PixelValuesListShape]: GetGlobalValueIterator 调用失败，Values.Count={Values.Count} 与 Shape.PointCount={Shape.PointCount} 不一致。");
                yield break;
            }

            int pointIndex = 0;
            foreach (Vector2I globalPosition in Shape.GetGlobalCoordinateIterator())
            {
                yield return (globalPosition, Values[pointIndex]);
                pointIndex++;
            }
        }

        /// <summary>
        /// 获取全局坐标与对应值索引的配对迭代器。
        /// </summary>
        public IEnumerable<(Vector2I GPosition, int ValueIndex)> GetGlobalValueIndexIterator()
        {
            if (!HasValues())
            {
                GD.PushError("[PixelShapeSystem/PixelValuesListShape]: GetGlobalValueIndexIterator 调用失败，当前对象未携带 Values。");
                yield break;
            }

            if (!IsAligned())
            {
                GD.PushError(
                    $"[PixelShapeSystem/PixelValuesListShape]: GetGlobalValueIndexIterator 调用失败，Values.Count={Values.Count} 与 Shape.PointCount={Shape.PointCount} 不一致。");
                yield break;
            }

            int valueIndex = 0;
            foreach (Vector2I globalPosition in Shape.GetGlobalCoordinateIterator())
            {
                yield return (globalPosition, valueIndex);
                valueIndex++;
            }
        }
    }
}
