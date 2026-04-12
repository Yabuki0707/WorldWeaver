using Godot;
using WorldWeaver.MapSystem.ChunkSystem.State;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块加载请求处理器。
    /// <para>该对象只负责接收请求表，并将请求项合并进更新表。</para>
    /// <para>若同一区块在一次或多次请求中重复出现，则保留枚举值更大的稳定状态。</para>
    /// </summary>
    public sealed class ChunkLoadRequestProcessor
    {
        // ================================================================================
        //                                  公开状态
        // ================================================================================

        /// <summary>
        /// 当前区块加载更新表。
        /// <para>请求到来时会直接合并进该表，随后由 ChunkManager.Update() 消费并逐项移除。</para>
        /// </summary>
        public ChunkLoadUpdateTable UpdateTable { get; } = new();


        // ================================================================================
        //                                  请求处理方法
        // ================================================================================

        /// <summary>
        /// 接收一份新的区块加载请求表。
        /// <para>该方法会将请求表中的内容直接合并进更新表，而不是保存为快照等待后续重建。</para>
        /// </summary>
        public void HandleRequestTable(ChunkLoadRequestTable requestTable)
        {
            if (requestTable == null)
            {
                throw new System.ArgumentNullException(nameof(requestTable));
            }

            foreach ((ChunkPosition chunkPosition, ChunkStateNode targetStableNode) in requestTable)
            {
                // 请求层只允许稳定状态，发现中间状态直接报错并跳过该条请求。
                if (!ChunkStateMachine.IsStable(targetStableNode))
                {
                    GD.PushError($"[ChunkSystem/ChunkLoadRequestProcessor]: 区块请求 {chunkPosition} 的目标节点 {targetStableNode} 不是稳定状态。");
                    continue;
                }

                // 同一区块多次出现时，按枚举值保留更大的稳定状态。
                UpdateTable.SetTargetStableNodeByMax(chunkPosition, targetStableNode);
            }
        }
    }
}
