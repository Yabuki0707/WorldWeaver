using System;
using WorldWeaver.MapSystem.ChunkSystem.Handler;

namespace WorldWeaver.MapSystem.ChunkSystem.State
{
    /// <summary>
    /// 区块状态节点信息
    /// </summary>
    public class ChunkStateNodeInfo
    {
        /// <summary>所属的节点枚举</summary>
        public ChunkStateNode Node { get; set; }

        /// <summary>导向的状态列表</summary>
        public ChunkStateNode[] ValidTransitions { get; set; }

        /// <summary>状态优先级</summary>
        public int Priority { get; set; }

        /// <summary>是否为稳定状态</summary>
        public bool IsStable { get; set; }

        /// <summary>状态描述</summary>
        public string Description { get; set; }

        /// <summary>状态处理器（无回调操作则为null）</summary>
        public StateHandler Handler { get; set; }
    }
}
