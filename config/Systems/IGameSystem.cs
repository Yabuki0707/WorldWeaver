using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// System 基接口。定义所有 System 共有的身份标识、缓存前置、查询入口与卸载行为。
    /// </summary>
    public interface IGameSystem
    {
        /// <summary>
        /// System 名称，全局唯一，用于前置依赖引用与调试。
        /// </summary>
        string SystemName { get; }

        /// <summary>
        /// 是否已通过 GenerateXxxPrerequisites 生成前置依赖缓存。
        /// </summary>
        bool IsPrerequisitesGenerated { get; }

        /// <summary>
        /// 稳定的前置依赖名称列表，由 GenerateXxxPrerequisites 一次性填入，之后不再变更。
        /// <para>以 <see cref="ReadOnlyMemory{T}"/> 存底，支持零分配切片。</para>
        /// </summary>
        ReadOnlyMemory<string> Prerequisites { get; }

        /// <summary>
        /// 稳定的前置依赖名称集合，与 <see cref="Prerequisites"/> 同源，供 O(1) 查重。
        /// <para>以 <see cref="FrozenSet{T}"/> 存底，创建后不可变且哈希探针最优。</para>
        /// </summary>
        FrozenSet<string> PrerequisiteSet { get; }

        /// <summary>
        /// 按名称查询全局 System。默认实现委托至 IGameManager.Instance.Systems.ResolveFor。
        /// </summary>
        IGameSystem VisitSystem(string systemName)
        {
            return IGameManager.Instance.Systems.ResolveFor(this, systemName);
        }

        /// <summary>
        /// 卸载本 System，清理所有运行时状态。
        /// </summary>
        void Uninstall();
    }
}
