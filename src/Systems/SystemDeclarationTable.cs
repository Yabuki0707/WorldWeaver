using System;
using System.Collections.Generic;
using Godot;

namespace WorldWeaver.Systems
{
    /// <summary>
    /// 系统声明表实现。
    /// </summary>
    /// <typeparam name="TSystem">容器所管理的 System 类型。</typeparam>
    public sealed class SystemDeclarationTable<TSystem> : ISystemDeclarationTable<TSystem> where TSystem : IGameSystem
    {
        /// <summary>
        /// SystemName → System 实例映射，OrderedDictionary 语义，同名先到先得。
        /// </summary>
        private readonly Dictionary<string, TSystem> _table = new(StringComparer.Ordinal);

        /// <summary>
        /// 所有已声明的 SystemName。
        /// </summary>
        public IEnumerable<string> Keys => _table.Keys;

        /// <summary>
        /// 已声明的 System 数量。
        /// </summary>
        public int Count => _table.Count;

        /// <summary>
        /// 具体类型的 + 运算符，供同程序集内部代码直接使用。
        /// </summary>
        public static SystemDeclarationTable<TSystem> operator +(SystemDeclarationTable<TSystem> table, TSystem system)
        {
            table._table.TryAdd(system.SystemName, system);
            return table;
        }

        /// <summary>
        /// 显式接口实现，供外部通过 <see cref="ISystemDeclarationTable{TSystem}"/> 调用。
        /// </summary>
        static ISystemDeclarationTable<TSystem> ISystemDeclarationTable<TSystem>.operator +(ISystemDeclarationTable<TSystem> table, TSystem system)
        {
            if (table is not SystemDeclarationTable<TSystem> self)
            {
                throw new ArgumentException(
                    $"[SystemDeclarationTable] +: 不支持的声明表类型 {table.GetType()}。");
            }

            self._table.TryAdd(system.SystemName, system);
            return self;
        }

        /// <summary>
        /// 检查指定 SystemName 是否已在声明表中。
        /// </summary>
        public bool Contains(string systemName)
        {
            return _table.ContainsKey(systemName);
        }

        /// <summary>
        /// 向 SystemContainerBase 暴露已声明 System 实例。
        /// </summary>
        internal IEnumerable<TSystem> Values => _table.Values;

        /// <summary>
        /// 拷贝当前声明表并筛选：已生成过前置、生成失败或前置缺失的 System 被排除。
        /// <para>返回筛选后的新声明表，原表不变。</para>
        /// </summary>
        /// <param name="generatePrerequisites">容器注入的前置依赖生成钩子。</param>
        internal SystemDeclarationTable<TSystem> BuildFilteredTable(Func<TSystem, bool> generatePrerequisites)
        {
            // 先拷贝
            SystemDeclarationTable<TSystem> filtered = new();
            foreach (TSystem system in _table.Values)
            {
                string name = system.SystemName;

                // 已生成过前置依赖则报错并排除。
                if (system.IsPrerequisitesGenerated)
                {
                    GD.PushError(
                        $"[SystemDeclarationTable] 声明表筛选: System '{name}' 的前置依赖已生成过，排除。");
                    continue;
                }

                // 生成前置依赖失败则报错并排除。
                if (!generatePrerequisites(system))
                {
                    GD.PushError(
                        $"[SystemDeclarationTable] 声明表筛选: System '{name}' 的前置依赖生成失败，排除。");
                    continue;
                }

                // 校验所有前置是否存在于本声明表中。
                bool missingDep = false;
                foreach (string dep in system.Prerequisites.Span)
                {
                    if (!Contains(dep))
                    {
                        GD.PushError(
                            $"[SystemDeclarationTable] 声明表筛选: System '{name}' 的前置依赖 '{dep}' 不存在于声明表中，排除。");
                        missingDep = true;
                        break;
                    }
                }

                if (missingDep)
                {
                    continue;
                }

                filtered += system;
            }

            return filtered;
        }
    }
}
