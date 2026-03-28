using System;
using Godot;
using System.Runtime.CompilerServices;
using WorldWeaver.MapSystem.ChunkSystem.State.Handler;

namespace WorldWeaver.MapSystem.ChunkSystem.State
{
    /// <summary>
    /// 区块状态机引擎。
    /// <para>每个 Chunk 实例拥有一个 ChunkState 实例。</para>
    /// <para>负责驱动状态转换、计算路径、请求回调。</para>
    /// </summary>
    public class ChunkState : IDisposable
    {


        /*******************************
                  实例成员与构造
        ********************************/

        /// <summary>
        /// 所属区块,作为执行回调操作、传递tile设置、移除等讯息的主体
        /// </summary>
        public Chunk OwnerChunk { get; private set; }


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ownerChunk">所属区块</param>
        /// <exception cref="ArgumentNullException">ownerChunk 为 null</exception>
        /// <exception cref="ArgumentException">ownerChunk 为 Chunk.Empty</exception>
        public ChunkState(Chunk ownerChunk)
        {
            if (ownerChunk == null)
            {
                GD.PushError("ownerChunk 不能为 null");
                throw new ArgumentNullException(nameof(ownerChunk), "ownerChunk 不能为 null");
            }
            else if (ownerChunk == Chunk.Empty)
            {
                GD.PushError("ownerChunk 不能为 Chunk.Empty");
                throw new ArgumentException("ownerChunk 不能为 Chunk.Empty", nameof(ownerChunk));
            }
            OwnerChunk = ownerChunk;
        }

        /// <summary>前一个状态节点（用于回退）</summary>
        public ChunkStateNode? PreviousNode { get; private set; } = null;

        /// <summary>当前状态节点</summary>
        public ChunkStateNode CurrentNode{get;private set;} = ChunkStateNode.Enter;

        /// <summary>当前所在稳定节点（上一次到达的稳定节点）</summary>
        public ChunkStateNode CurrentStableNode{get;private set;} = ChunkStateNode.Enter;

        /// <summary>目标稳定节点（中间过程，下一步要去哪个稳定节点）</summary>
        public ChunkStateNode TargetStableNode{get;private set;} = ChunkStateNode.NotInMemory;

        /// <summary>终点稳定节点（最终目的地）</summary>
        public ChunkStateNode FinalStableNode{get;private set;} = ChunkStateNode.NotInMemory;

        /// <summary>目标节点（中间过程，下一步要去哪个节点）</summary>
        public ChunkStateNode? TargetNode{get;private set;} = null;

        /*******************************
                  阻塞
        ********************************/

        /// <summary>阻塞节点数组（无法进入的节点），索引为ChunkStateNode枚举值</summary>
        private readonly bool[] _blockedNodes = new bool[Enum.GetValues(typeof(ChunkStateNode)).Length];

        /// <summary>阻塞指定节点</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlockNode(ChunkStateNode node) => _blockedNodes[(int)node] = true;

        /// <summary>检查指定节点是否被阻塞</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlocked(ChunkStateNode node) => _blockedNodes[(int)node];

        /// <summary>清空阻塞表</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearBlockedNodes() => Array.Clear(_blockedNodes, 0, _blockedNodes.Length);



        /*******************************
                  核心驱动逻辑
        ********************************/

        /// <summary>
        /// 设置终点稳定节点，并重新计算路径
        /// </summary>
        public bool SetFinalTarget(ChunkStateNode finalNode)
        {
            if (ChunkStateMachine.IsStable(finalNode) == false)
            {
                GD.PushError($"ChunkState: 试图设置非稳定节点 {finalNode} 为最终目标！");
                return false;
            }

            FinalStableNode = finalNode;
            return RecalculateTargetStable();
        }

        /// <summary>
        /// 重新计算中间目标稳定节点
        /// </summary>
        private bool RecalculateTargetStable()
        {
            // 如果当前稳定节点就是终点，那么目标的稳定节点即当前稳定节点也就是终点稳定节点，此刻的当前、目标、终点是同一个节点
            if (CurrentStableNode == FinalStableNode)
            {
                TargetStableNode = CurrentStableNode;
                return true;
            }
            // 查表：获取从当前稳定节点去往终点的路径集合 (包含距离信息)

            // 存在通往目标稳定节点的稳定节点路径并获取
            int[] pathDistances = ChunkStateMachine.GetStablePathLookup(CurrentStableNode, FinalStableNode);
            if (pathDistances != null)
            {
                // 找到一个邻近的稳定节点 (Candidate)，使得它在 通往终点的路径上(pathDistances)中，并且距离终点最近
                
                // 通往且距离终点稳定节点最近的稳定节点
                ChunkStateNode bestCandidate = CurrentStableNode;
                // 是否找到一个候选节点
                bool isFound = false;
                // 候选节点的距离(要求是距离终点节点最近的距离)
                int minDistance = int.MaxValue;

                //遍历所有可能的稳定节点邻居
                foreach (ChunkStateNode candidate in ChunkStateMachine.GetStableAdjacency(CurrentStableNode))
                {
                    // 若不再通往目标稳定节点的路径上，则跳过
                    int candidateDist = pathDistances[(int)candidate];
                    // 候选节点到终点的距离
                    if (candidateDist == int.MinValue)
                        continue;
                    // 该候选节点距离终点更近，作为最优解更新
                    if (candidateDist < minDistance)
                    {
                        minDistance = candidateDist;
                        bestCandidate = candidate;
                        isFound = true;
                    }
                }
                // 找到了一个距离终点稳定节点最近的稳定节点
                if (isFound)
                {
                    TargetStableNode = bestCandidate;
                    return true;
                }
            }
            // 无路可走或无候选的目标稳定节点，则保持原地

            // 将目标稳定节点设为当前稳定节点(即保持原地)
            if (TargetStableNode != CurrentStableNode)
                TargetStableNode = CurrentStableNode;
            return false;
        }



        /// <summary>
        /// 更新状态机（由 ChunkManager 驱动）
        /// 若当前节点的回调操作返回null,则将当前节点加入阻塞表,并进行回退
        /// </summary>
        /// <returns>是否成功变更状态或达到终点稳定状态</returns>
        public bool Update()
        {
            // 到达目标稳定节点则尝试更新新的目标
            // 此刻有两种情况，"刚到达目标稳定节点" 和 "停滞于目标稳定节点"
            if (CurrentNode == TargetStableNode)
            {
                // 若当前稳定节点未更新为已到达的目标稳定节点，则判定为 "刚到达目标稳定节点"
                if (CurrentStableNode != TargetStableNode)
                {
                    // 向上传递状态稳定变化消息到事件总线
                    OwnerChunk?.NotifyStateStableReached(CurrentStableNode, TargetStableNode);
                    // 到达了中间的目标稳定节点，则更新当前稳定节点
                    CurrentStableNode = CurrentNode;
                }
                // 重新计算下一个中间目标,若下一个中间目标不存在,则返回失败
                if (RecalculateTargetStable()==false) return false;
                // 当成功获取新的目标稳定节点 或 已到达终点稳定节点时 ,认定为成功
                return true;
            }
            // 获取当前稳定节点通往目标稳定节点的路径，若不存在则返回失败
            // 目标稳定节点是当前稳定节点的邻近稳定节点，因此可通过 _detailedPathLookup 获取两者间的路径
            bool[] pathLookup = ChunkStateMachine.GetDetailedPathLookup(CurrentStableNode, TargetStableNode);
            if (pathLookup == null)
            {
                return false;
            }
            // 若目标节点为空,则需要选择一个目标节点(下一跳的节点)
            if (TargetNode==null)
            { 
                // 获取当前节点导向的邻居节点们
                ChunkStateNode[] transitions = ChunkStateMachine.GetValidTransitions(CurrentNode);
                // 遍历当前节点的所有 ValidTransitions，看哪个在 pathLookup 中，并选择优先级最高的
                
                // 最优的下一跳节点的优先级,初始化为-1，负数优先级代表全局禁止进入
                int maxPriority = -1;
                // 最优的下一跳节点
                ChunkStateNode? bestTransitionNode = null;
                // 遍历并选择未被阻塞且优先级最高的合法邻近节点

                foreach (ChunkStateNode transitionNode in transitions)
                {
                    // 不在当前节点到目标节点的路径上则跳过
                    if (pathLookup[(int)transitionNode]==false) continue;
                    // 若目标节点阻塞则跳过
                    if (IsBlocked(transitionNode)) continue;
                    // 获取节点的优先级
                    int priority = ChunkStateMachine.GetPriority(transitionNode);
                    // 优先级为负数则不考虑
                    if (ChunkStateMachine.IsNodeGlobalDisabled(transitionNode)==true) continue;

                    // 更新最高优先级
                    if (priority > maxPriority)
                    {
                        maxPriority = priority;
                        bestTransitionNode = transitionNode;
                    }
                }
                // 若没有最优的下一跳节点,则收集错误信息并返回错误,并尝试释放阻塞表里的节点(为了防止原地打转,只能被迫再次尝试被阻塞的节点)
                if (bestTransitionNode==null)
                {
                    // 检查阻塞表是否存在被阻塞的节点
                    bool isAnyBlocked = false;
                    // 被阻塞节点名称字符串构建器
                    System.Text.StringBuilder blockedNodeNames = new();
                    // 遍历阻塞表，收集被阻塞的节点名称
                    for (int blockedNodeIndex = 0; blockedNodeIndex < _blockedNodes.Length; blockedNodeIndex++)
                    {
                        if (_blockedNodes[blockedNodeIndex])
                        {
                            isAnyBlocked = true;
                            // 若已有节点名称，则添加分隔符
                            if (blockedNodeNames.Length > 0)
                                blockedNodeNames.Append(',');
                            // 将枚举索引转换为节点名称
                            blockedNodeNames.Append(((ChunkStateNode)blockedNodeIndex).ToString());
                        }
                    }
                    // 若阻塞表不为空，则清空阻塞表腾出更多选择
                    if (isAnyBlocked)
                    {
                        GD.PushError($"ChunkState: 没有找到最优的下一跳节点,但阻塞表不为空,已清空阻塞表腾出更多选择,当前节点: {CurrentNode},阻塞表: {blockedNodeNames}");
                        ClearBlockedNodes();
                    }
                    return false;
                }
                // 若找到一个最优的下一跳节点则更新
                else
                    TargetNode = bestTransitionNode;
            }
            // 若前一个节点为null，则无需执行回调，直接变更为目标节点
            if (PreviousNode == null)
            {
                // 更新前节点为当前节点
                PreviousNode = CurrentNode;
                // 变更当前节点为目标节点
                CurrentNode = (ChunkStateNode)TargetNode;
                TargetNode = null;
                ClearBlockedNodes();
                return true;
            }
            // 目标节点的状态处理器
            StateHandler handler = ChunkStateMachine.GetHandler((ChunkStateNode)TargetNode);
            // 执行目标节点的回调操作结果：Success=成功，RetryLater=临时失败，PermanentFailure=永久失败。
            StateExecutionResult executeResult = handler?.Execute(OwnerChunk) ?? StateExecutionResult.Success;

            switch (executeResult)
            {
                case StateExecutionResult.PermanentFailure:
                    // 回退至前一个节点，执行新的处理操作（如果前节点存在且不是当前节点）
                    if (PreviousNode != null && PreviousNode != CurrentNode)
                    {
                        // 清空阻塞表，因为前一个节点处理操作阻塞的节点不一定在其他操作路线里被阻塞
                        ClearBlockedNodes();
                        // 将前一个当前节点加入阻塞表（禁止推进到该节点，防止其再发生错误）
                        BlockNode(CurrentNode);
                        CurrentNode = PreviousNode.Value;
                    }
                    TargetNode = null;
                    return false;

                case StateExecutionResult.Success:
                    // 更新前节点为本次更新前的节点
                    PreviousNode = CurrentNode;
                    // 变更当前节点为目标节点
                    CurrentNode = (ChunkStateNode)TargetNode;
                    TargetNode = null;
                    ClearBlockedNodes();
                    return true;

                case StateExecutionResult.RetryLater:
                default:
                    // 临时失败，保持现状
                    return false;
            }
        }
        
        
        /*******************************
                  IDisposable 实现
        ********************************/

        /// <summary>
        /// 释放 ChunkState 资源
        /// </summary>
        public void Dispose()
        {
            // 清理托管资源
            ClearBlockedNodes();
            OwnerChunk = null;
            GC.SuppressFinalize(this);
        }
    }
}
