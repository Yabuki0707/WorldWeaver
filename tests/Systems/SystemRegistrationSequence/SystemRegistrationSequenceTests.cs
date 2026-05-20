using System.Collections.Generic;
using System.Collections.Frozen;
using Xunit;

namespace WorldWeaver.Systems.Tests
{
    public sealed class SystemRegistrationSequenceTests
    {
        // ================================================================================
        //                              正常拓扑排序
        // ================================================================================

        [Fact]
        public void Iterate_NoDependencies_YieldsAllInDeclarationOrder()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            StubSystem a = new("A");
            StubSystem b = new("B");
            StubSystem c = new("C");
            declared[a.SystemName] = a;
            declared[b.SystemName] = b;
            declared[c.SystemName] = c;

            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);
            List<string> yielded = new();
            foreach (IGlobalSystem entry in seq)
            {
                seq += entry;
                yielded.Add(entry.SystemName);
            }

            Assert.Equal(3, yielded.Count);
            Assert.Equal(3, seq.Count);
            Assert.Empty(seq.GetUnregisteredSystemNames());
        }

        [Fact]
        public void Iterate_WithDependencies_YieldsPrerequisitesFirst()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            StubSystem logger = new("Logger", "Config");
            StubSystem config = new("Config");
            StubSystem engine = new("Engine", "Logger", "Config");
            declared[logger.SystemName] = logger;
            declared[config.SystemName] = config;
            declared[engine.SystemName] = engine;

            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);
            List<string> yielded = new();
            foreach (IGlobalSystem entry in seq)
            {
                seq += entry;
                yielded.Add(entry.SystemName);
            }

            // Config 和 Logger（无前置）必须排在 Engine（依赖两者）前面
            int configIdx = yielded.IndexOf("Config");
            int loggerIdx = yielded.IndexOf("Logger");
            int engineIdx = yielded.IndexOf("Engine");
            Assert.True(configIdx < engineIdx, "Config 应在 Engine 之前");
            Assert.True(loggerIdx < engineIdx, "Logger 应在 Engine 之前");
            Assert.Equal(3, seq.Count);
        }

        // ================================================================================
        //                              环依赖检测
        // ================================================================================

        [Fact]
        public void Iterate_CircularDependency_LeavesUnregistered()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            StubSystem a = new("A", "B");
            StubSystem b = new("B", "A");
            declared[a.SystemName] = a;
            declared[b.SystemName] = b;

            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);
            foreach (IGlobalSystem entry in seq)
            {
                seq += entry;
            }

            // 环依赖中两者都无法注册
            Assert.Equal(0, seq.Count);
            IReadOnlyList<string> unregistered = seq.GetUnregisteredSystemNames();
            Assert.Contains("A", unregistered);
            Assert.Contains("B", unregistered);
        }

        // ================================================================================
        //                              部分注册
        // ================================================================================

        [Fact]
        public void Iterator_OneFailsToInitialize_OthersCanStillRegister()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            StubSystem config = new("Config");
            FailingInitSystem failer = new("Failer", "Config");
            StubSystem logger = new("Logger");
            declared[config.SystemName] = config;
            declared[failer.SystemName] = failer;
            declared[logger.SystemName] = logger;

            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);
            foreach (IGlobalSystem entry in seq)
            {
                // 模拟: Failer 初始化失败, 不加入 seq
                if (entry.SystemName != "Failer")
                {
                    seq += entry;
                }
            }

            Assert.Equal(2, seq.Count);
            Assert.Contains("Failer", seq.GetUnregisteredSystemNames());
        }

        // ================================================================================
        //                              Contains / Indexer / TryGet
        // ================================================================================

        [Fact]
        public void ContainsKey_RegisteredSystem_ReturnsTrue()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            StubSystem a = new("A");
            declared[a.SystemName] = a;

            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);
            seq += a;

            Assert.True(seq.ContainsKey("A"));
            Assert.False(seq.ContainsKey("B"));
        }

        [Fact]
        public void Indexer_RegisteredSystem_ReturnsInstance()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            StubSystem a = new("A");
            declared[a.SystemName] = a;

            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);
            seq += a;

            Assert.Same(a, seq["A"]);
        }

        [Fact]
        public void TryGetValue_ReturnsCorrectly()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            StubSystem a = new("A");
            declared[a.SystemName] = a;

            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);
            seq += a;

            Assert.True(seq.TryGetValue("A", out IGlobalSystem found));
            Assert.Same(a, found);
            Assert.False(seq.TryGetValue("B", out _));
        }

        // ================================================================================
        //                              ToSystemTable
        // ================================================================================

        [Fact]
        public void ToSystemTable_ReturnsFrozenCopy()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            StubSystem a = new("A");
            declared[a.SystemName] = a;

            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);
            seq += a;

            FrozenDictionary<string, IGlobalSystem> table = seq.ToSystemTable();
            Assert.Equal(1, table.Count);
            Assert.True(table.ContainsKey("A"));
        }

        // ================================================================================
        //                              空声明表
        // ================================================================================

        [Fact]
        public void EmptyDeclared_YieldsNothing()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);

            int count = 0;
            foreach (IGlobalSystem _ in seq)
            {
                count++;
            }

            Assert.Equal(0, count);
            Assert.Equal(0, seq.Count);
        }

        // ================================================================================
        //                              缺失前置
        // ================================================================================

        [Fact]
        public void MissingPrerequisite_StaysUnregistered()
        {
            Dictionary<string, IGlobalSystem> declared = new();
            StubSystem a = new("A", "NonExistent");
            declared[a.SystemName] = a;

            SystemRegistrationSequence<IGlobalSystem> seq = new(declared);
            foreach (IGlobalSystem entry in seq)
            {
                seq += entry;
            }

            Assert.Equal(0, seq.Count);
            Assert.Contains("A", seq.GetUnregisteredSystemNames());
        }
    }
}
