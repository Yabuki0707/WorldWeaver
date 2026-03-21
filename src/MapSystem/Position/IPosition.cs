namespace rasu.Map.Position
{
    /// <summary>
    /// 位置接口，定义任意元素坐标位置的基本结构。
    /// </summary>
    /// <typeparam name="T">实现该接口的具体类型</typeparam>
    public interface IPosition<T> where T : IPosition<T>
    {
        /// <summary>
        /// 获取X轴坐标
        /// </summary>
        int X { get; }

        /// <summary>
        /// 获取Y轴坐标
        /// </summary>
        int Y { get; }

        /// <summary>
        /// 零点坐标
        /// </summary>
        static abstract T Zero { get; }

        /// <summary>
        /// 将坐标变为 long 类型的 key 作为字典的键
        /// </summary>
        /// <returns>long 类型的 key</returns>
        long ToKey();

        /// <summary>
        /// 返回坐标绝对值的新实例
        /// </summary>
        /// <returns>坐标绝对值的新实例</returns>
        T Abs();
    }
}
