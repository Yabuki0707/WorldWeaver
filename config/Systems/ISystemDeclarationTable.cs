using System.Collections.Generic;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 系统声明表。可查看与检测已声明的 SystemName，也是注册 System 的入口。
    /// <para>通过 + 运算符添加 System，同名先到先得。</para>
    /// </summary>
    /// <typeparam name="TSystem">容器所管理的 System 类型。</typeparam>
    public interface ISystemDeclarationTable<TSystem> where TSystem : IGameSystem
    {
        /// <summary>
        /// 添加一个 System 到声明表。同名 System 先到先得。
        /// </summary>
        static abstract ISystemDeclarationTable<TSystem> operator +(ISystemDeclarationTable<TSystem> table, TSystem system);

        /// <summary>
        /// 所有已声明的 SystemName。
        /// </summary>
        IEnumerable<string> Keys { get; }

        /// <summary>
        /// 已声明的 System 数量。
        /// </summary>
        int Count { get; }

        /// <summary>
        /// 检查指定 SystemName 是否已在声明表中。
        /// </summary>
        bool Contains(string systemName);
    }
}
