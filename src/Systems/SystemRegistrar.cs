using System;
using System.Collections;
using System.Collections.Frozen;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 系统注册器。以筛选后的声明表构建反向依赖图与入度计数，
    /// 迭代器按拓扑顺序逐个产出就绪 System。调用方初始化成功后通过 + 将其加入成员表并注销入度。
    /// <para>遍历终止后仍留在入度表中的 System 即为初始化异常者。</para>
    /// </summary>
    /// <typeparam name="TSystem">容器所管理的 System 类型。</typeparam>
    public sealed class SystemRegistrar<TSystem> : ISystemRegistrar<TSystem>, IEnumerable<TSystem>
        where TSystem : IGameSystem
    {
        /// <summary>
        /// 已加入注册表的 System。Key 为 SystemName。
        /// </summary>
        private readonly Dictionary<string, TSystem> _members;

        /// <summary>
        /// 反向依赖图——Key 为前置名，Value 为依赖它的 System 列表。
        /// <para>用于 + 调用时快速找到依赖方从而扣减入度。</para>
        /// </summary>
        private readonly Dictionary<string, List<TSystem>> _reverseGraph;

        /// <summary>
        /// 尚未初始化的 System 的入度表——Key 为 SystemName，Value 为未就位的前置数。
        /// <para>建图时写入所有声明 System。迭代过程中，成功注册的 System 由 + 运算符从本表移除，
        /// 因此遍历终止后表中剩余项即为初始化异常者。</para>
        /// </summary>
        private readonly Dictionary<string, int> _inDegree;

        /// <summary>
        /// 就绪队列——入度已归零但尚未被迭代器产出的 System。
        /// </summary>
        private readonly Queue<TSystem> _readyQueue;

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
        public bool IsRegistered(string systemName)
        {
            return _members.ContainsKey(systemName);
        }

        /// <summary>
        /// 尝试按名称获取已注册的 System。
        /// </summary>
        public bool TryGetRegistration(string systemName, out TSystem system)
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
        /// 以筛选后的声明表构建反向依赖图与入度计数，无前置的 System 直接入就绪队列。
        /// </summary>
        /// <param name="declared">筛选后的声明表。</param>
        public SystemRegistrar(SystemDeclarationTable<TSystem> declared)
        {
            if (declared == null)
            {
                throw new ArgumentNullException(nameof(declared));
            }

            _members = new Dictionary<string, TSystem>(declared.Count, StringComparer.Ordinal);
            _reverseGraph = new Dictionary<string, List<TSystem>>(declared.Count, StringComparer.Ordinal);
            _inDegree = new Dictionary<string, int>(declared.Count, StringComparer.Ordinal);
            _readyQueue = new Queue<TSystem>();

            BuildGraph(declared);
        }

        // ================================================================================
        //                              + 运算符
        // ================================================================================

        /// <summary>
        /// 将指定 System 加入成员表，同时从入度表注销自身，并将所有依赖方的入度扣减。
        /// 依赖方入度归零时即刻进入就绪队列。
        /// </summary>
        public static SystemRegistrar<TSystem> operator +(
            SystemRegistrar<TSystem> registrar, TSystem system)
        {
            if (system == null)
            {
                GD.PushError("[SystemRegistrar] +=: system 为 null。");
                return registrar;
            }

            string name = system.SystemName;
            if (!registrar._members.TryAdd(name, system))
            {
                GD.PushError($"[SystemRegistrar] +=: System '{name}' 已在成员表中，跳过。");
                return registrar;
            }

            // 注销自身——成功注册的 System 从入度表移除
            registrar._inDegree.Remove(name);

            // 所有依赖此 system 的，入度 -1
            if (registrar._reverseGraph.TryGetValue(name, out List<TSystem> dependents))
            {
                foreach (TSystem dependent in dependents)
                {
                    string depName = dependent.SystemName;
                    if (!registrar._inDegree.TryGetValue(depName, out int degree))
                    {
                        continue;
                    }

                    degree--;
                    registrar._inDegree[depName] = degree;
                    if (degree == 0)
                    {
                        registrar._readyQueue.Enqueue(dependent);
                    }
                }
            }

            return registrar;
        }

        // ================================================================================
        //                              迭代器
        // ================================================================================

        /// <summary>
        /// 从就绪队列逐个产出 System。队列耗尽即终止。
        /// <para>产出的顺序即拓扑顺序——前置依赖总是先于依赖方被产出。</para>
        /// </summary>
        public IEnumerator<TSystem> GetEnumerator()
        {
            while (_readyQueue.Count > 0)
            {
                yield return _readyQueue.Dequeue();
            }
        }

        /// <summary>
        /// 非泛型迭代器，委托至 <see cref="GetEnumerator()"/>。
        /// </summary>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        // ================================================================================
        //                              初始化异常名单
        // ================================================================================

        /// <summary>
        /// 获取迭代终止后仍在入度表中的 System，按入度分组返回。
        /// <para>入度为 0：出队后 Initialize 返回 false（初始化异常）。</para>
        /// <para>入度大于 0：前置依赖从未被满足（前置条件异常）。</para>
        /// </summary>
        public (IReadOnlyList<string> InitFailed, IReadOnlyList<string> PrereqFailed) GetFailedSystemNames()
        {
            List<string> initFailed = new();
            List<string> prereqFailed = new();
            foreach (KeyValuePair<string, int> pair in _inDegree)
            {
                if (pair.Value == 0)
                {
                    initFailed.Add(pair.Key);
                }
                else
                {
                    prereqFailed.Add(pair.Key);
                }
            }

            return (initFailed, prereqFailed);
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

        // ================================================================================
        //                              建图
        // ================================================================================

        /// <summary>
        /// 遍历声明表，建立反向依赖图与入度计数，入度为零的 System 直接入就绪队列。
        /// <para>缺失前置（声明了不在声明表中的依赖名）会被记录但不阻塞建图。</para>
        /// </summary>
        private void BuildGraph(SystemDeclarationTable<TSystem> declared)
        {
            foreach (TSystem system in declared.Values)
            {
                //获取system信息
                string name = system.SystemName;
                ReadOnlySpan<string> prereqs = system.Prerequisites.Span;
                
                // 建立入度条目
                _inDegree[name] = prereqs.Length;

                // 无前置的 System 直接入就绪队列（保留在 _inDegree 中，等 + 移除）
                if (prereqs.Length == 0)
                {
                    _readyQueue.Enqueue(system);
                }

                // 收集前置到反向依赖图
                foreach (string dep in prereqs)
                {
                    if (!declared.Contains(dep))
                    {
                        GD.PushError(
                            $"[SystemRegistrar] BuildGraph: System '{name}' 的前置依赖 '{dep}' 在筛选后的声明表中不存在，这表明前置数据在筛选与建图之间被篡改。");
                        continue;
                    }

                    if (!_reverseGraph.TryGetValue(dep, out List<TSystem> dependents))
                    {
                        dependents = [];
                        _reverseGraph[dep] = dependents;
                    }

                    dependents.Add(system);
                }
            }
        }
    }
}
