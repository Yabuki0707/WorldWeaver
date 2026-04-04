using System;
using Godot;

namespace WorldWeaver.MapSystem
{
    /// <summary>
    /// 基于指数定义的二维尺寸类型。
    /// <para>内部以整数字段保存宽高、指数与掩码，并按需构造 `Vector2I` 视图。</para>
    /// </summary>
    public sealed class MapElementSize : IEquatable<MapElementSize>, IComparable<MapElementSize>
    {
        // ================================================================================
        //                                  常量
        // ================================================================================

        /// <summary>
        /// 默认宽度指数。
        /// </summary>
        public const int DEFAULT_WIDTH_EXP = 4;

        /// <summary>
        /// 默认高度指数。
        /// </summary>
        public const int DEFAULT_HEIGHT_EXP = 4;


        // ================================================================================
        //                                  构造
        // ================================================================================

        /// <summary>
        /// 通过宽高指数构造尺寸信息。
        /// <para>当指数不合法时，会自动回退到默认值 `(4, 4)`。</para>
        /// </summary>
        public MapElementSize(int widthExp, int heightExp)
        {
            if (!IsValidExp(widthExp, heightExp))
            {
                GD.PushWarning($"MapElementSize: 非法的尺寸指数 ({widthExp}, {heightExp})，已自动回退为默认值 ({DEFAULT_WIDTH_EXP}, {DEFAULT_HEIGHT_EXP})。");
                widthExp = DEFAULT_WIDTH_EXP;
                heightExp = DEFAULT_HEIGHT_EXP;
            }

            WidthExp = widthExp;
            HeightExp = heightExp;

            Width = 1 << WidthExp;
            Height = 1 << HeightExp;

            WidthMask = Width - 1;
            HeightMask = Height - 1;

            Area = Width * Height;
            AreaMask = Area - 1;
        }

        /// <summary>
        /// 通过 `Vector2I` 形式的指数构造尺寸信息。
        /// <para>当指数不合法时，会自动回退到默认值 `(4, 4)`。</para>
        /// </summary>
        public MapElementSize(Vector2I exp) : this(exp.X, exp.Y)
        {
        }


        // ================================================================================
        //                                  基础属性
        // ================================================================================

        /// <summary>
        /// 宽度指数。
        /// </summary>
        public int WidthExp { get; }

        /// <summary>
        /// 高度指数。
        /// </summary>
        public int HeightExp { get; }

        /// <summary>
        /// 宽度。
        /// </summary>
        public int Width { get; }

        /// <summary>
        /// 高度。
        /// </summary>
        public int Height { get; }

        /// <summary>
        /// 宽度掩码。
        /// </summary>
        public int WidthMask { get; }

        /// <summary>
        /// 高度掩码。
        /// </summary>
        public int HeightMask { get; }

        /// <summary>
        /// 指数的 `Vector2I` 视图。
        /// </summary>
        public Vector2I Exp => new(WidthExp, HeightExp);

        /// <summary>
        /// 实际尺寸的 `Vector2I` 视图。
        /// </summary>
        public Vector2I Size => new(Width, Height);

        /// <summary>
        /// 掩码的 `Vector2I` 视图。
        /// </summary>
        public Vector2I Mask => new(WidthMask, HeightMask);

        /// <summary>
        /// 面积。
        /// </summary>
        public int Area { get; }

        /// <summary>
        /// 面积掩码。
        /// <para>由于宽高均为 2 的幂，面积同样恒为 2 的幂，因此可直接用于位运算校验与归一化索引。</para>
        /// </summary>
        public int AreaMask { get; }


        // ================================================================================
        //                                  校验
        // ================================================================================

        /// <summary>
        /// 判断指数是否合法。
        /// <para>要求指数大于 0、小于 30，且派生出的面积必须小于 `int.MaxValue`。</para>
        /// </summary>
        public static bool IsValidExp(int widthExp, int heightExp)
        {
            //当指数小于等于 0时不合法
            if (widthExp <= 0 || heightExp <= 0) return false;
            //当指数大于等于 30时不合法
            if (widthExp >= 30 || heightExp >= 30) return false;
            //判断面积是否正确
            long computedArea = (1L << widthExp) * (1L << heightExp);
            return computedArea < int.MaxValue;
        }

        /// <summary>
        /// 判断指数是否合法。
        /// </summary>
        public static bool IsValidExp(Vector2I exp)
        {
            return IsValidExp(exp.X, exp.Y);
        }


        // ================================================================================
        //                                  局部坐标与索引的检验与处理
        // ================================================================================

        /// <summary>
        /// 判断局部坐标是否在当前尺寸范围内。
        /// </summary>
        public bool IsValidLocalPosition(int localX, int localY)
        {
            return ((localX & WidthMask) == localX) && ((localY & HeightMask) == localY);
        }

        /// <summary>
        /// 判断局部坐标是否在当前尺寸范围内。
        /// </summary>
        public bool IsValidLocalPosition(Vector2I localPosition)
        {
            return IsValidLocalPosition(localPosition.X, localPosition.Y);
        }

        /// <summary>
        /// 判断局部索引是否在当前尺寸范围内。
        /// <para>由于面积恒为 2 的幂，因此可通过面积掩码直接完成范围判断。</para>
        /// </summary>
        public bool IsValidLocalIndex(int localIndex)
        {
            return (localIndex & AreaMask) == localIndex;
        }

        /// <summary>
        /// 将局部坐标规范化到当前尺寸范围内。
        /// </summary>
        public Vector2I ToValidLocalPosition(int localX, int localY)
        {
            return new(localX & WidthMask, localY & HeightMask);
        }

        /// <summary>
        /// 将局部坐标规范化到当前尺寸范围内。
        /// </summary>
        public Vector2I ToValidLocalPosition(Vector2I localPosition)
        {
            return ToValidLocalPosition(localPosition.X, localPosition.Y);
        }

        /// <summary>
        /// 将局部索引规范化到当前尺寸范围内。
        /// <para>等价于对面积取模，但在 2 的幂尺寸下可直接使用位与提升性能。</para>
        /// </summary>
        public int ToValidLocalIndex(int localIndex)
        {
            return localIndex & AreaMask;
        }


        // ================================================================================
        //                                  隐式转换
        // ================================================================================

        /// <summary>
        /// 隐式转换为实际尺寸对应的 `Vector2I`。
        /// </summary>
        public static implicit operator Vector2I(MapElementSize size) => size?.Size ?? Vector2I.Zero;


        // ================================================================================
        //                                  比较与相等
        // ================================================================================

        public bool Equals(MapElementSize other)
        {
            return other is not null &&
                   WidthExp == other.WidthExp &&
                   HeightExp == other.HeightExp;
        }

        /// <summary>
        /// 按指数进行比较。
        /// <para>先比较 X，若 X 相等再比较 Y。</para>
        /// </summary>
        public int CompareTo(MapElementSize other)
        {
            if (ReferenceEquals(other, null))
            {
                return 1;
            }

            int xCompare = WidthExp.CompareTo(other.WidthExp);
            return xCompare != 0 ? xCompare : HeightExp.CompareTo(other.HeightExp);
        }

        public override bool Equals(object obj)
        {
            return obj is MapElementSize other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(WidthExp, HeightExp);
        }

        public override string ToString()
        {
            return $"MapElementSize(Exp: {Exp}, Size: {Size}, Mask: {Mask})";
        }

        public static bool operator ==(MapElementSize left, MapElementSize right)
        {
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (ReferenceEquals(left, null) || ReferenceEquals(right, null))
            {
                return false;
            }

            return left.Equals(right);
        }

        public static bool operator !=(MapElementSize left, MapElementSize right)
        {
            return !(left == right);
        }

        public static bool operator <(MapElementSize left, MapElementSize right)
        {
            if (ReferenceEquals(left, null))
            {
                return !ReferenceEquals(right, null);
            }

            return left.CompareTo(right) < 0;
        }

        public static bool operator <=(MapElementSize left, MapElementSize right)
        {
            if (ReferenceEquals(left, null))
            {
                return true;
            }

            return left.CompareTo(right) <= 0;
        }

        public static bool operator >(MapElementSize left, MapElementSize right)
        {
            if (ReferenceEquals(left, null))
            {
                return false;
            }

            return left.CompareTo(right) > 0;
        }

        public static bool operator >=(MapElementSize left, MapElementSize right)
        {
            if (ReferenceEquals(right, null))
            {
                return true;
            }

            if (ReferenceEquals(left, null))
            {
                return false;
            }

            return left.CompareTo(right) >= 0;
        }
    }
}
