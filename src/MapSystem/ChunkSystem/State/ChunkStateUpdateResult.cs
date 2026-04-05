using WorldWeaver.MapSystem.ChunkSystem.State;

namespace WorldWeaver.MapSystem.ChunkSystem.State
{
    /// <summary>
    /// 单次区块状态推进的结果。
    /// <para>用于在 Chunk 完成状态推进后，把“旧当前节点 / 新当前节点 / 是否到达稳定节点”等信息回传给 ChunkManager。</para>
    /// </summary>
    public sealed class ChunkStateUpdateResult(
        ChunkStateNode previousNode,
        ChunkStateNode newNode,
        bool isNewNodeStable,
        ChunkStateNode? previousStableNode,
        ChunkStateNode? newStableNode)
    {
        /// <summary>
        /// 更新前的当前节点。
        /// </summary>
        public ChunkStateNode PreviousNode { get; } = previousNode;

        /// <summary>
        /// 更新后的当前节点。
        /// </summary>
        public ChunkStateNode NewNode { get; } = newNode;

        /// <summary>
        /// 更新后的当前节点是否为稳定节点。
        /// </summary>
        public bool IsNewNodeStable { get; } = isNewNodeStable;

        /// <summary>
        /// 若本次推进到达了稳定节点，则这里记录推进前的稳定节点；否则为 null。
        /// </summary>
        public ChunkStateNode? PreviousStableNode { get; } = previousStableNode;

        /// <summary>
        /// 若本次推进到达了稳定节点，则这里记录新的稳定节点；否则为 null。
        /// </summary>
        public ChunkStateNode? NewStableNode { get; } = newStableNode;
    }
}
