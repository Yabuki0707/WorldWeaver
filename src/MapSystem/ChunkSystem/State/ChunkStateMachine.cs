using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using System.Runtime.CompilerServices;
using WorldWeaver.MapSystem.ChunkSystem.Handler;

namespace WorldWeaver.MapSystem.ChunkSystem.State
{
    /// <summary>
    /// 区块状态节点枚举
    /// </summary>
    public enum ChunkStateNode
    {

        /// <summary>退出状态机</summary>
        Exit = 0,

        /// <summary>进入状态机（初始状态）</summary>
        Enter = 1,

        /// <summary>不存在于内存</summary>
        NotInMemory = 2,

        /// <summary>正在加载于内存</summary>
        LoadingInMemory = 3,

        /// <summary>正在读取信息（同步）</summary>
        ReadingInformation = 4,

        /// <summary>正在读取信息（异步）</summary>
        ReadingInformationInThread = 5,

        /// <summary>已加载于内存（Level 0）</summary>
        LoadedInMemory = 6,

        /// <summary>正在删除出内存</summary>
        DeletingFromMemory = 7,

        /// <summary>正在保存信息（同步）</summary>
        SavingInformation = 8,

        /// <summary>正在保存信息（异步）</summary>
        SavingInformationInThread = 9,

        /// <summary>正在加载于游戏</summary>
        LoadingInGame = 10,

        /// <summary>正在卸载于游戏</summary>
        DeletingFromGame = 11,

        /// <summary>已加载于游戏（Level 1）</summary>
        LoadedInGame = 12,

    }
    
    
    
    /// <summary>
    /// 区块状态机静态工具类。
    /// <para>负责管理状态节点信息、路径初始化和查询工具。</para>
    /// <para>注意：此静态类仅提供路径初始化、内容查询功能与相关节点信息查询功能，</para>
    /// <para>不负责具体的状态决策逻辑。状态决策由 ChunkState 实例负责。</para>
    /// </summary>
    public static class ChunkStateMachine
    {
        /*******************************
                  静态数据与初始化
        ********************************/
        
        
        /// <summary>稳定节点集合</summary>
        public static readonly ChunkStateNode[] STABLE_NODES = 
        [
            ChunkStateNode.Enter,
            ChunkStateNode.NotInMemory,
            ChunkStateNode.LoadedInMemory,
            ChunkStateNode.LoadedInGame,
            ChunkStateNode.Exit
        ];

        /// <summary>
        /// 距离与优先度的权重比。
        /// <para>默认值为 5，表示选路评分时 1 点距离差异等价于 5 点优先度差异。</para>
        /// </summary>
        public static int DistancePriorityWeightRatio { get; set; } = 5;
        
        /// <summary>状态节点信息数组</summary>
        private static readonly ChunkStateNodeInfo[] _STATE_NODES_INFO = 
        [
            // 0: Exit
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.Exit,
                ValidTransitions = [],
                Priority = 1+DistancePriorityWeightRatio,
                IsStable = true,
                Description = "移除出状态机",
                Handler = null
            },
            // 1: Enter
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.Enter,
                ValidTransitions = [ChunkStateNode.NotInMemory],
                Priority = 0,
                IsStable = true,
                Description = "进入状态机",
                Handler = null
            },
            // 2: NotInMemory
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.NotInMemory,
                ValidTransitions = [ChunkStateNode.Exit, ChunkStateNode.ReadingInformation, ChunkStateNode.ReadingInformationInThread],
                Priority = 1+DistancePriorityWeightRatio,
                IsStable = true,
                Description = "不在内存",
                Handler = null
            },
            // 3: LoadingInMemory
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.LoadingInMemory,
                ValidTransitions = [ChunkStateNode.LoadedInMemory],
                Priority = 1+DistancePriorityWeightRatio,
                IsStable = false,
                Description = "正加载于内存",
                Handler = new LoadingInMemoryHandler()
            },
            // 4: ReadingInformation
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.ReadingInformation,
                ValidTransitions = [ChunkStateNode.LoadingInMemory],
                Priority = 1+DistancePriorityWeightRatio,
                IsStable = false,
                Description = "正在读取信息",
                Handler = new ReadingInformationHandler()
            },
            // 5: ReadingInformationInThread
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.ReadingInformationInThread,
                ValidTransitions = [ChunkStateNode.LoadingInMemory],
                Priority = 1+2*DistancePriorityWeightRatio,
                IsStable = false,
                Description = "正在线程读取信息",
                Handler = new ReadingInformationInThreadHandler()
            },
            // 6: LoadedInMemory
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.LoadedInMemory,
                ValidTransitions = [ChunkStateNode.SavingInformation, ChunkStateNode.SavingInformationInThread, ChunkStateNode.LoadingInGame],
                Priority = 0,
                IsStable = true,
                Description = "已加载到内存",
                Handler = null
            },
            // 7: DeletingFromMemory
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.DeletingFromMemory,
                ValidTransitions = [ChunkStateNode.NotInMemory],
                Priority = 0,
                IsStable = false,
                Description = "正在删除出内存",
                Handler = new DeletingFromMemoryHandler()
            },
            // 8: SavingInformation
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.SavingInformation,
                ValidTransitions = [ChunkStateNode.DeletingFromMemory],
                Priority = 1+DistancePriorityWeightRatio,
                IsStable = false,
                Description = "正在保存信息",
                Handler = new SavingInformationHandler()
            },
            // 9: SavingInformationInThread
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.SavingInformationInThread,
                ValidTransitions = [ChunkStateNode.DeletingFromMemory],
                Priority = 1+2*DistancePriorityWeightRatio,
                IsStable = false,
                Description = "正在线程保存信息",
                Handler = new SavingInformationInThreadHandler()
            },
            // 10: LoadingInGame
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.LoadingInGame,
                ValidTransitions = [ChunkStateNode.LoadedInGame],
                Priority = 1+DistancePriorityWeightRatio,
                IsStable = false,
                Description = "正在加载于游戏",
                Handler = new LoadingInGameHandler()
            },
            // 11: DeletingFromGame
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.DeletingFromGame,
                ValidTransitions = [ChunkStateNode.LoadedInMemory],
                Priority = 0,
                IsStable = false,
                Description = "正在卸载于游戏",
                Handler = new DeletingFromGameHandler()
            },
            // 12: LoadedInGame
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.LoadedInGame,
                ValidTransitions = [ChunkStateNode.DeletingFromGame],
                Priority = 0,
                IsStable = true,
                Description = "已加载于游戏",
                Handler = null
            },
        ];
        
        /// <summary>
        /// 节点枚举数量
        /// </summary>
        private static readonly int _NODE_COUNT = Enum.GetValues(typeof(ChunkStateNode)).Length;

        /// <summary>
        /// 路由查表数组大小。
        /// </summary>
        private static readonly int _ROUTE_LOOKUP_SIZE = _NODE_COUNT * _NODE_COUNT;
        
        
        /// <summary>
        /// 获取稳定路由查表索引。
        /// </summary>
        /// <param name="from">起始稳定节点</param>
        /// <param name="to">终点稳定节点</param>
        /// <returns>数组索引</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetRouteLookupIndex(ChunkStateNode from, ChunkStateNode to)
        {
            return (int)from * _NODE_COUNT + (int)to;
        }

        /// <summary>
        /// 从稳定路由查表索引中还原起始稳定节点与终点稳定节点。
        /// </summary>
        /// <param name="index">路径索引</param>
        /// <param name="isValidateIndex">是否验证索引在有效范围内</param>
        /// <returns>起始节点和目标节点的元组</returns>
        /// <exception cref="ArgumentOutOfRangeException">索引超出有效范围时抛出</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ChunkStateNode from, ChunkStateNode to) GetRouteNodesFromLookupIndex(int index, bool isValidateIndex = true)
        {
            if (isValidateIndex && (index < 0 || index >= _ROUTE_LOOKUP_SIZE))
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"索引 {index} 超出有效范围 [0, {_ROUTE_LOOKUP_SIZE})");
            }
            ChunkStateNode from = (ChunkStateNode)(index / _NODE_COUNT);
            ChunkStateNode to = (ChunkStateNode)(index % _NODE_COUNT);
            return (from, to);
        }


        /// <summary>
        /// 稳定路由节点距离表。
        /// <para>键为“起始稳定节点 -> 终点稳定节点”这一条稳定路由。</para>
        /// <para>值为一个按节点枚举值索引的距离数组，数组中记录“该节点距离终点稳定节点的最小步数”。</para>
        /// <para>若某节点不在该稳定路由可达范围内，则对应值为 <see cref="int.MaxValue"/>。</para>
        /// </summary>
        private static readonly int[][] _STABLE_ROUTE_NODE_DISTANCE_LOOKUP = new int[_ROUTE_LOOKUP_SIZE][];

        /// <summary>
        /// 获取稳定路由节点距离表条目。
        /// </summary>
        /// <param name="from">起始稳定节点</param>
        /// <param name="to">终点稳定节点</param>
        /// <returns>节点距离数组（索引为节点枚举值）</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int[] GetStableRouteNodeDistanceLookup(ChunkStateNode from, ChunkStateNode to)
        {
            int index = GetRouteLookupIndex(from, to);
            return _STABLE_ROUTE_NODE_DISTANCE_LOOKUP[index];
        }

        /*******************************
                  路径初始化
        ********************************/

        /// <summary>
        /// 静态构造函数，初始化路径数据
        /// </summary>
        static ChunkStateMachine()
        {
            InitializeRouteData();
        }

        /// <summary>
        /// 初始化稳定路由相关数据。
        /// </summary>
        private static void InitializeRouteData()
        {
            if (!InitializeStableRouteNodeDistanceLookup())
            {
                GD.PushError("ChunkStateMachine: 稳定路由节点距离表初始化失败！");
                return;
            }

            int routeCount = _STABLE_ROUTE_NODE_DISTANCE_LOOKUP.Count(x => x != null);
            GD.Print($"ChunkStateMachine: 稳定路由节点距离表初始化完成。有效路由数: {routeCount}");
        }

        /// <summary>
        /// 初始化稳定路由节点距离表。
        /// <para>对每个起始稳定节点执行一次 DFS，枚举从该稳定节点出发能到达的所有稳定终点。</para>
        /// <para>每当搜索到一个稳定节点，就把当前路径上的所有节点到该终点稳定节点的距离写入查表，并在重复命中时保留更小值。</para>
        /// <para>注意：起始稳定节点不会预先放入当前路径去重集合，这样 DFS 才允许“绕一圈回到起始稳定节点再继续前进”的合法路径被记录下来。</para>
        /// </summary>
        /// <returns>是否初始化成功</returns>
        private static bool InitializeStableRouteNodeDistanceLookup()
        {
            foreach (ChunkStateNode startStable in STABLE_NODES)
            {
                // 总遍历次数，作为粗粒度熔断，避免状态图配置异常时 DFS 无限展开。
                int traversalCount = 0;

                // 栈中完整保存当前 DFS 路径。
                // 一旦命中某个稳定终点，就需要对整条路径做一次回溯写表，因此这里必须保留完整路径而不是只保留当前节点。
                List<ChunkStateNode> pathStack = [startStable];

                // 与 pathStack 对齐的“邻居遍历进度栈”。
                // 使用显式栈而不是递归，是为了在前进/回退时精确控制每层已经处理到哪个邻居。
                List<int> neighborIndexStack = [0];

                // 当前路径去重集合，只用于阻止“当前 DFS 路径内部”形成环。
                // 这里故意不预置 startStable，允许后续路径先绕回起始稳定节点，再从它继续走向更远的稳定终点。
                HashSet<ChunkStateNode> visitedInPathSet = [];
                
                // 迭代式 DFS 主循环：能前进就前进，所有邻居处理完后再显式回退。
                while (pathStack.Count > 0)
                {
                    // 栈顶节点就是当前正在展开邻居的节点。
                    int stackTopIndex = pathStack.Count - 1;
                    ChunkStateNode currentNode = pathStack[stackTopIndex];

                    // 当前节点可导向的邻居集合，以及本层当前处理到的邻居下标。
                    ChunkStateNode[] neighbors = GetValidTransitions(currentNode);
                    int neighborIndex = neighborIndexStack[stackTopIndex];
                    
                    // 路径向未处理的邻居节点推进
                    if (neighborIndex < neighbors.Length)
                    {
                        // 该邻居节点
                        ChunkStateNode neighbor = neighbors[neighborIndex];

                        // 推进处理进度:不管这个邻居最终能不能走，本层“下一次该看哪个邻居”都要先推进，避免重复处理。
                        neighborIndexStack[stackTopIndex] = neighborIndex + 1;

                        // 若邻居已在当前路径中出现过，则跳过，避免当前 DFS 分支形成环。
                        if (visitedInPathSet.Contains(neighbor))
                        {
                            continue;
                        }

                        // 邻居可以进入时，先把它压入当前路径，并为它准备一层新的邻居遍历进度。
                        pathStack.Add(neighbor);
                        visitedInPathSet.Add(neighbor);
                        neighborIndexStack.Add(0);

                        // 命中稳定节点时，立刻把“起始稳定节点 -> 当前稳定终点”这条路径上的所有节点距离写入查表。
                        // 写完后并不停止，而是继续深搜，以便发现同一起点到更远稳定终点的路径。
                        if (IsStable(neighbor))
                        {
                            RecordStableRouteNodeDistances(pathStack);
                        }
                    }
                    // 当前节点的邻居已全部处理完，需要做一次显式回退。
                    else
                    {
                        // 撤销pathStack 里的路径占用
                        pathStack.RemoveAt(stackTopIndex);
                        // 撤销visitedInPathSet 里的当前分支去重占用
                        visitedInPathSet.Remove(currentNode);
                        // 撤消neighborIndexStack 里的当前层遍历进度
                        neighborIndexStack.RemoveAt(stackTopIndex);
                    }

                    // 每轮循环都累加一次，用于兜底熔断。
                    traversalCount++;
                    if (traversalCount > 4096)
                    {
                        GD.PushWarning($"ChunkStateMachine: 初始化稳定路由节点距离表时检测到潜在死循环，起始稳定节点: {startStable}");
                        return false;
                    }
                }
            }

            return true;
        }
        
        /// <summary>
        /// 将当前 DFS 路径写入对应稳定路由的节点距离表。
        /// <para>这里的起始稳定节点与目标稳定节点都直接取自路径栈自身：</para>
        /// <para><c>pathStack[0]</c> 是当前 DFS 的起始稳定节点，<c>pathStack[^1]</c> 是本次命中的目标稳定节点。</para>
        /// <para>这样可以避免“外部参数”和“实际路径”出现两份真相。</para>
        /// </summary>
        private static void RecordStableRouteNodeDistances(List<ChunkStateNode> pathStack)
        {
            //起始稳定节点，即索引0处的值
            ChunkStateNode startStable = pathStack[0];
            //终点稳定节点，即最后一位索引处的值
            ChunkStateNode destinationStable = pathStack[^1];

            // 每个“起始稳定节点 -> 终点稳定节点”组合只对应一张距离数组，首次命中时再创建。
            int routeLookupIndex = GetRouteLookupIndex(startStable, destinationStable);
            ref int[] nodeDistances = ref _STABLE_ROUTE_NODE_DISTANCE_LOOKUP[routeLookupIndex];
            // 若该稳定路由尚未初始化，则创建一张默认“全部不可达”的距离数组。
            if (nodeDistances == null)
            {
                nodeDistances = new int[_NODE_COUNT];
                Array.Fill(nodeDistances, int.MaxValue);
            }

            // 对栈上每个节点进行距离录入。
            for (int nodeIndex = 0; nodeIndex < pathStack.Count; nodeIndex++)
            {
                // 当前遍历到的栈上节点
                ChunkStateNode nodeOnPath = pathStack[nodeIndex];

                // 当前稳定终点位于栈尾，因此任一路径节点到终点的距离都等于“终点下标 - 当前节点下标”。
                int distanceToDestination = pathStack.Count - 1 - nodeIndex;
                int existingDistance = nodeDistances[(int)nodeOnPath];

                // 同一节点可能通过不同 DFS 路径多次命中同一稳定终点，这里始终保留更短的那条路径距离。
                if (distanceToDestination < existingDistance)
                {
                    nodeDistances[(int)nodeOnPath] = distanceToDestination;
                }
            }
        }

        /*******************************
                  静态查询工具
        ********************************/

        /// <summary>
        /// 获取指定状态节点的所有导向的节点
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>该节点的所有导向的节点的数组</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChunkStateNode[] GetValidTransitions(ChunkStateNode node)
        {
            return _STATE_NODES_INFO[(int)node].ValidTransitions;
        }

        /// <summary>
        /// 获取指定状态节点的状态处理器
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>该节点配置的状态处理器；若未配置则返回 null。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StateHandler GetHandler(ChunkStateNode node)
        {
            return _STATE_NODES_INFO[(int)node].Handler;
        }

        /// <summary>
        /// 获取指定状态节点的优先级
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>该节点的优先级</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetPriority(ChunkStateNode node)
        {
            return _STATE_NODES_INFO[(int)node].Priority;
        }

        /// <summary>
        /// 判断指定状态节点是否为稳定状态
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>如果节点是稳定状态则返回true，否则返回false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStable(ChunkStateNode node)
        {
            return _STATE_NODES_INFO[(int)node].IsStable;
        }


        /// <summary>
        /// 查询节点是否被全局禁用（优先级为负）
        /// </summary>
        /// <param name="node">要查询的节点</param>
        /// <returns>若被全局禁用返回 true，否则返回 false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNodeGlobalDisabled(ChunkStateNode node)
        {
            return _STATE_NODES_INFO[(int)node].Priority < 0;  
        }

        /// <summary>
        /// 将节点全局禁用：优先级设为负数（0 会被设置为 int.MinValue）
        /// </summary>
        /// <param name="node">要禁用的节点</param>
        /// <remarks>
        /// <para>⚠️ 此方法不是线程安全的！</para>
        /// <para>修改全局状态会影响所有使用该状态机的 Chunk 实例。</para>
        /// <para>请确保在单线程环境中调用，或在多线程环境中适当地同步。</para>
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisableNodeGlobal(ChunkStateNode node)
        {
            int index = (int)node;
            ChunkStateNodeInfo info = _STATE_NODES_INFO[index];
            if (info.Priority == 0)
            {
                info.Priority = int.MinValue;
            }
            else if (info.Priority > 0)
            {
                info.Priority = -info.Priority;
            }
        }

        /// <summary>
        /// 恢复节点优先级为正数（int.MinValue 恢复为 0）
        /// </summary>
        /// <param name="node">要恢复的节点</param>
        /// <remarks>
        /// <para>⚠️ 此方法不是线程安全的！</para>
        /// <para>修改全局状态会影响所有使用该状态机的 Chunk 实例。</para>
        /// <para>请确保在单线程环境中调用，或在多线程环境中适当地同步。</para>
        /// </remarks>
        public static bool RestoreNodePriority(ChunkStateNode node)
        {
            int index = (int)node;
            if (_STATE_NODES_INFO[index].Priority < 0)
            {
                if (_STATE_NODES_INFO[index].Priority == int.MinValue)
                {
                    _STATE_NODES_INFO[index].Priority = 0;
                    return true;
                }
                _STATE_NODES_INFO[index].Priority = Math.Abs(_STATE_NODES_INFO[index].Priority);
                return true;
            }

            return false;
        }
    }
}
