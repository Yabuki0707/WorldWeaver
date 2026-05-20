using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 系统注册序列表。接收筛选后的声明表，通过饱和式扫描迭代器逐次产出前置已全部在成员表中的 System。
    /// <para>迭代器产出的 System 已经过拓扑顺序保证——每次产出的 System 其所有前置均已注册。</para>
    /// <para>调用方初始化成功后通过 + 运算符将其加入成员表。</para>
    /// <para>终止条件为成员表不再增长（<c>_members.Count == prevCount</c>）。</para>
    /// <para>终止扫描后通过 <see cref="GetUnregisteredSystemNames"/> 获取未能注册的 System 名单。</para>
    /// </summary>
    /// <typeparam name="TSystem">容器所管理的 System 类型。</typeparam>
    public sealed class SystemRegistrationSequence<TSystem> : ISystemRegistrationSequence<TSystem>, IEnumerable<TSystem>
        where TSystem : IGameSystem
    {
        /// <summary>
        /// 筛选后的声明表，Key 为 SystemName。构造函数注入，仅用于扫描产出。
        /// </summary>
        private readonly Dictionary<string, TSystem> _declared;

        /// <summary>
        /// 已加入注册表的 System。Key 为 SystemName。
        /// <para>由 + 运算添加；其数量增长是饱和式扫描是否继续的唯一判断条件。</para>
        /// </summary>
        private readonly Dictionary<string, TSystem> _members;

        // ================================================================================
        //                              属性
        // ================================================================================

        /// <summary>
        /// 已注册的 System 数量。
        /// </summary>
        public int Count => _members.Count;

        /// <summary>
        /// 检查指定名称的 System 是否已注册。
        /// </summary>
        public bool ContainsKey(string systemName)
        {
            return _members.ContainsKey(systemName);
        }

        /// <summary>
        /// 尝试按名称获取已注册的 System。
        /// </summary>
        public bool TryGetValue(string systemName, out TSystem system)
        {
            return _members.TryGetValue(systemName, out system);
        }

        /// <summary>
        /// 按名称获取已注册的 System，不存在则抛出 KeyNotFoundException。
        /// </summary>
        public TSystem this[string systemName] => _members[systemName];

        // ================================================================================
        //                              构造
        // ================================================================================

        /// <summary>
        /// 以筛选后的声明表构建注册序列表。
        /// </summary>
        /// <param name="declared">筛选后的声明表，Key 为 SystemName。</param>
        public SystemRegistrationSequence(Dictionary<string, TSystem> declared)
        {
            _declared = declared ?? throw new ArgumentNullException(nameof(declared));
            _members = new Dictionary<string, TSystem>(declared.Count, StringComparer.Ordinal);
        }

        // ================================================================================
        //                              + 运算符
        // ================================================================================

        /// <summary>
        /// 将指定 System 加入 <see cref="_members"/>，返回当前序列表以支持链式调用。
        /// </summary>
        public static SystemRegistrationSequence<TSystem> operator +(
            SystemRegistrationSequence<TSystem> sequence, TSystem system)
        {
            if (system == null)
            {
                GD.PushError("[SystemRegistrationSequence] +: system 为 null，操作无效。");
                return sequence;
            }

            string name = system.SystemName;
            if (!sequence._members.TryAdd(name, system))
            {
                GD.PushError($"[SystemRegistrationSequence] +: System '{name}' 已在成员表中，跳过。");
            }

            return sequence;
        }

        // ================================================================================
        //                              迭代器（饱和式扫描）
        // ================================================================================

        /// <summary>
        /// 饱和式扫描迭代器：反复扫描声明表，产出前置已全在 <see cref="_members"/> 中且自身不在 <see cref="_members"/> 中的 System。
        /// <para>当扫描完毕而成员表未增长时终止（<c>_members.Count == prevCount</c>）。</para>
        /// <para>产出的顺序即拓扑顺序——前置依赖总是先于依赖方被产出。</para>
        /// </summary>
        public IEnumerator<TSystem> GetEnumerator()
        {
            int prevCount = -1;

            // 局部函数: 检查 system 的所有前置是否都已加入成员表。
            bool AllPrereqsMet(TSystem system)
            {
                ReadOnlySpan<string> prerequisites = system.Prerequisites.Span;
                foreach (string dep in prerequisites)
                {
                    if (!_members.ContainsKey(dep))
                    {
                        return false;
                    }
                }

                return true;
            }

            while (_members.Count > prevCount)
            {
                prevCount = _members.Count;

                foreach (TSystem system in _declared.Values)
                {
                    string name = system.SystemName;

                    // 自身已在成员表中的跳过。
                    if (_members.ContainsKey(name))
                    {
                        continue;
                    }

                    // 前置未全部就位的跳过。
                    if (!AllPrereqsMet(system))
                    {
                        continue;
                    }

                    yield return system;
                }
            }
        }

        /// <summary>
        /// 非泛型迭代器委托至泛型版本。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // ================================================================================
        //                              未注册名单
        // ================================================================================

        /// <summary>
        /// 获取扫描终止后仍未进入成员表的 System 名称列表。
        /// <para>这些 System 可能因前置依赖形成环或前置声明了不存在的外部依赖而无法注册。</para>
        /// </summary>
        public IReadOnlyList<string> GetUnregisteredSystemNames()
        {
            List<string> unregistered = new();
            foreach (string name in _declared.Keys)
            {
                if (!_members.ContainsKey(name))
                {
                    unregistered.Add(name);
                }
            }

            return unregistered;
        }

        // ================================================================================
        //                              转换为系统表
        // ================================================================================

        /// <summary>
        /// 将当前成员表冻结为 <see cref="FrozenDictionary{TKey, TValue}"/>。
        /// </summary>
        public FrozenDictionary<string, TSystem> ToSystemTable()
        {
            return _members.ToFrozenDictionary(_members.Comparer);
        }

    }
}
