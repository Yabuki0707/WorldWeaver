using System;
using System.Collections.Frozen;
using System.Collections.Generic;

namespace WorldWeaver.Systems.Tests
{
    /// <summary>
    /// 测试用可配置前置的 IGlobalSystem。
    /// </summary>
    internal sealed class StubSystem : IGlobalSystem
    {
        private readonly string[] _prerequisites;

        public string SystemName { get; }

        public bool IsPrerequisitesGenerated { get; private set; }

        public ReadOnlyMemory<string> Prerequisites => _prerequisites;

        public FrozenSet<string> PrerequisiteSet => _prerequisites.ToFrozenSet(StringComparer.Ordinal);

        public StubSystem(string name, params string[] prerequisites)
        {
            SystemName = name;
            _prerequisites = prerequisites;
        }

        public bool GenerateGlobalSystemPrerequisites(IReadOnlyDictionary<string, IGlobalSystem> declaredSystems)
        {
            if (IsPrerequisitesGenerated)
            {
                return false;
            }

            IsPrerequisitesGenerated = true;
            return true;
        }

        public bool Initialize(ISystemRegistrationSequence<IGlobalSystem> registry)
        {
            return true;
        }

        public void Uninstall()
        {
        }
    }

    /// <summary>
    /// 初始化始终失败的 IGlobalSystem。
    /// </summary>
    internal sealed class FailingInitSystem : IGlobalSystem
    {
        private readonly string[] _prerequisites;

        public string SystemName { get; }

        public bool IsPrerequisitesGenerated { get; private set; }

        public ReadOnlyMemory<string> Prerequisites => _prerequisites;

        public FrozenSet<string> PrerequisiteSet => _prerequisites.ToFrozenSet(StringComparer.Ordinal);

        public FailingInitSystem(string name, params string[] prerequisites)
        {
            SystemName = name;
            _prerequisites = prerequisites;
        }

        public bool GenerateGlobalSystemPrerequisites(IReadOnlyDictionary<string, IGlobalSystem> declaredSystems)
        {
            if (IsPrerequisitesGenerated)
            {
                return false;
            }

            IsPrerequisitesGenerated = true;
            return true;
        }

        public bool Initialize(ISystemRegistrationSequence<IGlobalSystem> registry)
        {
            return false;
        }

        public void Uninstall()
        {
        }
    }
}
