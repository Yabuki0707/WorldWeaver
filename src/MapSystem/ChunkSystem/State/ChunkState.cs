using System;
using System.Runtime.CompilerServices;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.State.Handler;

namespace WorldWeaver.MapSystem.ChunkSystem.State
{
    /// <summary>
    /// 区块状态对象。
    /// <para>该类只负责维护状态节点、阻塞信息与目标选择规则，不再直接执行 handler。</para>
    /// <para>状态推进的副作用由 ChunkManager 驱动 handler 完成；执行成功后，再由 Chunk 调用本类完成节点迁移。</para>
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
        /// 中间目标稳定节点。
        /// <para>表示当前稳定节点在通往最终目标稳定节点过程中，下一步应先到达哪个稳定节点。</para>
        /// </summary>
        public ChunkStateNode TargetStableNode { get; private set; } = ChunkStateNode.NotInMemory;

        /// <summary>
        /// 最终目标稳定节点。
        /// </summary>
        public ChunkStateNode FinalStableNode { get; private set; } = ChunkStateNode.NotInMemory;

        /// <summary>
        /// 目标节点（下一步要切换到哪个节点）。
        /// <para>该字段是缓存字段：仅当其为 null 时才会重新计算；成功推进状态后才会被清空。</para>
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
                  目标设置与路径规划
        ********************************/

        /// <summary>
        /// 设置最终目标稳定节点，并重新规划中间稳定目标。
        /// </summary>
        public bool SetFinalTarget(ChunkStateNode finalNode)
        {
            // 终点必须是稳定节点，否则无法参与稳定路径规划。
            if (!ChunkStateMachine.IsStable(finalNode))
            {
                GD.PushError($"ChunkState: 试图设置非稳定节点 {finalNode} 为最终目标。");
                return false;
            }

            // 更新最终目标后，主动失效当前目标节点缓存，强制下次重新选路。
            FinalStableNode = finalNode;
            TargetNode = null;

            // 重新计算从当前稳定节点到最终稳定节点的下一段稳定目标。
            return RecalculateTargetStable();
        }

        /// <summary>
        /// 重新计算中间目标稳定节点。
        /// <para>该方法只做“宏观导航”：决定当前稳定节点通往最终稳定节点时，下一个应抵达的稳定节点。</para>
        /// </summary>
        private bool RecalculateTargetStable()
        {
            // 已经在最终稳定节点时，当前稳定节点就是下一段目标。
            if (CurrentStableNode == FinalStableNode)
            {
                TargetStableNode = CurrentStableNode;
                return true;
            }

            // 从稳定路径查表中读取“各候选稳定节点到终点的距离”。
            int[] pathDistances = ChunkStateMachine.GetStablePathLookup(CurrentStableNode, FinalStableNode);
            if (pathDistances != null)
            {
                // 默认保持原地，只有找到更优候选才替换。
                ChunkStateNode bestCandidate = CurrentStableNode;
                bool isFound = false;
                int minDistance = int.MaxValue;

                foreach (ChunkStateNode candidate in ChunkStateMachine.GetStableAdjacency(CurrentStableNode))
                {
                    // int.MinValue 代表该候选不在可达路径上，直接跳过。
                    int candidateDistance = pathDistances[(int)candidate];
                    if (candidateDistance == int.MinValue)
                    {
                        continue;
                    }

                    // 选择到终点距离更短的稳定邻居作为下一段目标。
                    if (candidateDistance < minDistance)
                    {
                        minDistance = candidateDistance;
                        bestCandidate = candidate;
                        isFound = true;
                    }
                }

                if (isFound)
                {
                    // 找到可达且更优的稳定候选，更新中间稳定目标。
                    TargetStableNode = bestCandidate;
                    return true;
                }
            }

            // 无路径或无候选时，回退为“保持当前稳定节点”。
            if (TargetStableNode != CurrentStableNode)
            {
                TargetStableNode = CurrentStableNode;
            }

            // 返回 false 让上层知道：本轮稳定目标未真正推进。
            return false;
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
            // 目标节点已缓存则复用，避免重复计算。
            if (TargetNode != null)
            {
                return true;
            }

            // 当前节点已经位于中间目标稳定节点时，需要先重新规划下一段稳定路径。
            if (CurrentNode == TargetStableNode)
            {
                if (CurrentStableNode != CurrentNode)
                {
                    // 当前节点已到稳定节点时，刷新“当前稳定节点”游标。
                    CurrentStableNode = CurrentNode;
                }

                if (CurrentStableNode == FinalStableNode)
                {
                    // 到达终点稳定节点后，本轮无需再选下一跳。
                    TargetStableNode = CurrentStableNode;
                    return false;
                }

                // 尚未到终点稳定节点，先更新下一段稳定目标。
                if (!RecalculateTargetStable())
                {
                    return false;
                }
            }

            // 读取“当前稳定节点 -> 目标稳定节点”这一段的详细路径节点集合。
            bool[] pathLookup = ChunkStateMachine.GetDetailedPathLookup(CurrentStableNode, TargetStableNode);
            if (pathLookup == null)
            {
                return false;
            }

            // 在当前节点可导向的邻接节点里，选出优先级最高且可达的下一跳。
            ChunkStateNode[] transitions = ChunkStateMachine.GetValidTransitions(CurrentNode);
            int maxPriority = -1;
            ChunkStateNode? bestTransitionNode = null;

            foreach (ChunkStateNode transitionNode in transitions)
            {
                // 不在本段路径上的节点不参与竞争。
                if (!pathLookup[(int)transitionNode])
                {
                    continue;
                }

                // 被局部阻塞表拦截的节点不参与竞争。
                if (IsBlocked(transitionNode))
                {
                    continue;
                }

                // 被全局禁用的节点不参与竞争。
                if (ChunkStateMachine.IsNodeGlobalDisabled(transitionNode))
                {
                    continue;
                }

                // 选择优先级最高的可用节点。
                int priority = ChunkStateMachine.GetPriority(transitionNode);
                if (priority > maxPriority)
                {
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

            // 提交节点推进：当前节点切换到目标节点，并清空目标缓存与阻塞表。
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
