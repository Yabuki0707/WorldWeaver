using System;
using System.Runtime.CompilerServices;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.State.Handler;

namespace WorldWeaver.MapSystem.ChunkSystem.State
{
    /// <summary>
    /// 区块状态对象。
    /// <para>该类只负责维护状态节点、阻塞信息，并基于状态机查表结果回答“下一跳目标节点是谁”，不再直接执行 handler。</para>
    /// <para>状态推进的副作用由 ChunkManager 驱动 handler 完成；执行成功后，再由 ChunkManager 调用本类完成节点迁移。</para>
    /// </summary>
    public sealed class ChunkState : IDisposable
    {
        /*******************************
                  基础状态字段
        ********************************/

        /// <summary>
        /// 前一个状态节点（用于永久失败时回退）。
        /// </summary>
        public ChunkStateNode? PreviousNode { get; private set; }

        /// <summary>
        /// 当前状态节点。
        /// </summary>
        public ChunkStateNode CurrentNode { get; private set; } = ChunkStateNode.Enter;

        /// <summary>
        /// 当前所在稳定节点（上一次到达的稳定节点）。
        /// </summary>
        public ChunkStateNode CurrentStableNode { get; private set; } = ChunkStateNode.Enter;

        /// <summary>
        /// 最终目标稳定节点。
        /// <para>该字段只表达区块最终希望到达的稳定态，不承担路径规划职责。</para>
        /// </summary>
        public ChunkStateNode FinalStableNode { get; private set; } = ChunkStateNode.NotInMemory;

        /// <summary>
        /// 目标节点（下一步要切换到哪个节点）。
        /// <para>该字段只保存最近一次选路结果，是否复用由调用方自行决定。</para>
        /// </summary>
        public ChunkStateNode? TargetNode { get; private set; }


        /*******************************
                  阻塞表
        ********************************/

        /// <summary>
        /// 阻塞节点数组（无法进入的节点），索引为 ChunkStateNode 枚举值。
        /// </summary>
        private readonly bool[] _blockedNodes = new bool[Enum.GetValues(typeof(ChunkStateNode)).Length];

        /// <summary>
        /// 阻塞指定节点。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void BlockNode(ChunkStateNode node)
        {
            // 将目标节点标记为“临时不可进入”，用于下次选路时跳过。
            _blockedNodes[(int)node] = true;
        }

        /// <summary>
        /// 检查指定节点是否被阻塞。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool IsBlocked(ChunkStateNode node)
        {
            // 直接读取阻塞表标记，供选路逻辑快速判断。
            return _blockedNodes[(int)node];
        }

        /// <summary>
        /// 清空阻塞表。
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearBlockedNodes()
        {
            // 每次成功推进或需要“重新放开路径”时，统一清空阻塞节点集合。
            Array.Clear(_blockedNodes, 0, _blockedNodes.Length);
        }


        /*******************************
                  目标设置
        ********************************/

        /// <summary>
        /// 设置最终目标稳定节点。
        /// <para>该方法只负责写入最终终点，并使当前目标节点失效。</para>
        /// </summary>
        public bool SetFinalStableNode(ChunkStateNode finalNode)
        {
            // 终点必须是稳定节点，否则无法参与稳定路径规划。
            if (!ChunkStateMachine.IsStable(finalNode))
            {
                GD.PushError($"ChunkState: 试图设置非稳定节点 {finalNode} 为最终目标。");
                return false;
            }

            FinalStableNode = finalNode;
            TargetNode = null;
            return true;
        }


        /*******************************
                  目标节点选择
        ********************************/

        /// <summary>
        /// 选择下一步的目标节点。
        /// <para>该方法只负责“从当前状态推导下一跳”，不执行任何 handler。</para>
        /// <para>若最终没有可走的下一跳，则返回 false，并保持 <see cref="TargetNode"/> 为 null。</para>
        /// </summary>
        public bool SelectTargetNode()
        {
            // 每次调用都按当前条件重新作答，先丢弃上一次的目标节点结果,否则失败分支会留下陈旧结果。
            TargetNode = null;

            // 读取“当前稳定锚点 -> 最终终点稳定节点”的节点距离表。
            int[] nodeDistanceLookup =
                ChunkStateMachine.GetStableRouteNodeDistanceLookup(CurrentStableNode, FinalStableNode);
            if (nodeDistanceLookup == null)
            {
                return false;
            }
            
            // 邻居节点数组
            ChunkStateNode[] transitions = ChunkStateMachine.GetValidTransitions(CurrentNode);
            
            // 当前在遍历中获取的最高权重
            int maxWeightedScore = int.MinValue;
            // 当前在遍历中获取的最高权重的节点的优先度
            int maxPriority = int.MinValue;
            ChunkStateNode? bestTransitionNode = null;
            
            // 在当前节点可导向的邻接节点里，按统一权重评分选择下一跳。
            // 评分越大越优；同分时仅使用优先度决胜。
            foreach (ChunkStateNode transitionNode in transitions)
            {
                // 先读取候选邻接节点到最终稳定终点的距离。
                int distanceToFinalStable = nodeDistanceLookup[(int)transitionNode];
                
                // int.MaxValue 表示该节点不在“当前稳定锚点 -> 最终稳定终点”的可达范围上，直接跳过。
                if (distanceToFinalStable == int.MaxValue) continue;

                // 被局部阻塞表拦截的节点不参与竞争。
                if (IsBlocked(transitionNode)) continue;

                // 被全局禁用的节点不参与竞争。
                if (ChunkStateMachine.IsNodeGlobalDisabled(transitionNode)) continue;
                
                //获取优先度
                int priority = ChunkStateMachine.GetPriority(transitionNode);
                // 统一权重评分：优先度越高权重越高，距离越近权重越高(计算语境下也就是距离越近,权重减的也就越少)。
                // 默认语义下，1 点距离差异等价于 5 点优先度差异。
                int weightedScore =
                    priority - distanceToFinalStable * ChunkStateMachine.DistancePriorityWeightRatio;
                
                // 若当前节点权重最高或权重相同时优先度最高，则取缔作为下一跳
                if (weightedScore > maxWeightedScore ||
                    (weightedScore == maxWeightedScore && priority > maxPriority))
                {
                    // 一旦当前候选更优，就同步刷新综合评分与同分决胜优先级。
                    maxWeightedScore = weightedScore;
                    maxPriority = priority;
                    bestTransitionNode = transitionNode;
                }
            }

            if (bestTransitionNode == null)
            {
                // 没有可选下一跳时，尝试清空阻塞表让后续循环有机会重试。
                TryResetBlockedNodesForRetry();
                return false;
            }

            // 记录下一跳目标，供 Manager 在本轮执行处理器后推进状态。
            TargetNode = bestTransitionNode;
            return true;
        }

        /// <summary>
        /// 当所有可行下一跳都因阻塞表而失效时，清空阻塞表以便后续重试。
        /// </summary>
        private void TryResetBlockedNodesForRetry()
        {
            // 收集当前阻塞表内容，便于日志定位“为什么无路可走”。
            bool isAnyBlocked = false;
            System.Text.StringBuilder blockedNodeNames = new();

            for (int blockedNodeIndex = 0; blockedNodeIndex < _blockedNodes.Length; blockedNodeIndex++)
            {
                if (!_blockedNodes[blockedNodeIndex])
                {
                    continue;
                }

                isAnyBlocked = true;
                if (blockedNodeNames.Length > 0)
                {
                    blockedNodeNames.Append(',');
                }

                blockedNodeNames.Append(((ChunkStateNode)blockedNodeIndex).ToString());
            }

            if (isAnyBlocked)
            {
                GD.PushError(
                    $"ChunkState: 没有找到可用的下一跳节点，但阻塞表不为空，已清空阻塞表以便后续重试。当前节点: {CurrentNode}, 阻塞表: {blockedNodeNames}");

                // 清空阻塞后，下个周期将重新评估这些节点。
                ClearBlockedNodes();
            }
        }


        /*******************************
                  状态提交与失败处理
        ********************************/

        /// <summary>
        /// 将当前状态推进到已选定的目标节点。
        /// <para>该方法应只在 handler 执行成功后调用。</para>
        /// </summary>
        public ChunkStateUpdateResult UpdateToTargetNode()
        {
            // 没有目标节点时，不执行任何状态推进。
            if (TargetNode == null)
            {
                return null;
            }

            // 快照推进前的信息，用于生成回传结果。
            ChunkStateNode previousNode = CurrentNode;
            ChunkStateNode newNode = TargetNode.Value;
            ChunkStateNode? previousStableNode = null;
            ChunkStateNode? newStableNode = null;
            bool isNewNodeStable = ChunkStateMachine.IsStable(newNode);

            // 提交节点推进：当前节点切换到目标节点，并清空本轮目标结果与阻塞表。
            PreviousNode = previousNode;
            CurrentNode = newNode;
            TargetNode = null;
            ClearBlockedNodes();

            // 若推进后落在稳定节点，同步刷新稳定节点游标。
            if (isNewNodeStable)
            {
                previousStableNode = CurrentStableNode;
                CurrentStableNode = newNode;
                newStableNode = newNode;
            }

            // 返回本次推进摘要，供 ChunkManager 统一广播事件。
            return new ChunkStateUpdateResult(
                previousNode,
                newNode,
                isNewNodeStable,
                previousStableNode,
                newStableNode);
        }

        /// <summary>
        /// 处理 handler 执行失败后的状态收敛。
        /// </summary>
        public void HandleExecutionFailure(StateExecutionResult executionResult)
        {
            switch (executionResult)
            {
                case StateExecutionResult.PermanentFailure:
                    // 永久失败：回退到上一节点，并阻塞当前节点避免立刻重复踩雷。
                    if (PreviousNode != null && PreviousNode != CurrentNode)
                    {
                        ClearBlockedNodes();
                        BlockNode(CurrentNode);
                        CurrentNode = PreviousNode.Value;
                    }

                    // 清空目标节点，让下个周期重新选路。
                    TargetNode = null;
                    break;

                case StateExecutionResult.RetryLater:
                    // 临时失败：保持当前状态，等待后续重试。
                case StateExecutionResult.Success:
                default:
                    // Success 不应走到这里；default 兜底保持不变。
                    break;
            }
        }


        /*******************************
                  IDisposable 实现
        ********************************/

        /// <summary>
        /// 释放 ChunkState 资源。
        /// </summary>
        public void Dispose()
        {
            // 仅清空本地阻塞表；不持有外部资源与上级引用，无需额外释放。
            ClearBlockedNodes();
        }
    }
}
