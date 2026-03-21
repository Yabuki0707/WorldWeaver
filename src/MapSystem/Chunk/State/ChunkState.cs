using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using System.Runtime.CompilerServices;
using rasu.Map.Chunk.State.Handlers;

namespace rasu.Map.Chunk.State
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
    /// 区块状态机引擎。
    /// <para>每个 Chunk 实例拥有一个 ChunkState 实例。</para>
    /// <para>负责驱动状态转换、计算路径、请求回调。</para>
    /// </summary>
    public class ChunkState : Object, IDisposable
    {
        /*******************************
                  静态数据与初始化
        ********************************/

        /// <summary>稳定节点集合</summary>
        public static readonly ChunkStateNode[] StableNodes = 
        [
            ChunkStateNode.Enter,
            ChunkStateNode.NotInMemory,
            ChunkStateNode.LoadedInMemory,
            ChunkStateNode.LoadedInGame,
            ChunkStateNode.Exit
        ];

        /// <summary>状态节点信息数组</summary>
        private static readonly ChunkStateNodeInfo[] StateNodeInfo = 
        [
            // 0: Exit
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.Exit,
                ValidTransitions = [],
                Priority = 5,
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
                Priority = 5,
                IsStable = true,
                Description = "不在内存",
                Handler = null
            },
            // 3: LoadingInMemory
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.LoadingInMemory,
                ValidTransitions = [ChunkStateNode.LoadedInMemory],
                Priority = 5,
                IsStable = false,
                Description = "正加载于内存",
                Handler = new LoadingInMemoryHandler()
            },
            // 4: ReadingInformation
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.ReadingInformation,
                ValidTransitions = [ChunkStateNode.LoadingInMemory],
                Priority = 5,
                IsStable = false,
                Description = "正在读取信息",
                Handler = new ReadingInformationHandler()
            },
            // 5: ReadingInformationInThread
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.ReadingInformationInThread,
                ValidTransitions = [ChunkStateNode.LoadingInMemory],
                Priority = 10,
                IsStable = false,
                Description = "正在线程读取信息",
                Handler = new ReadingInformationInThreadHandler()
            },
            // 6: LoadedInMemory
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.LoadedInMemory,
                ValidTransitions = [ChunkStateNode.DeletingFromMemory, ChunkStateNode.SavingInformation, ChunkStateNode.SavingInformationInThread, ChunkStateNode.LoadingInGame],
                Priority = 5,
                IsStable = true,
                Description = "已加载到内存",
                Handler = null
            },
            // 7: DeletingFromMemory
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.DeletingFromMemory,
                ValidTransitions = [ChunkStateNode.NotInMemory],
                Priority = 5,
                IsStable = false,
                Description = "正在删除出内存",
                Handler = new DeletingFromMemoryHandler()
            },
            // 8: SavingInformation
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.SavingInformation,
                ValidTransitions = [ChunkStateNode.DeletingFromMemory],
                Priority = 10,
                IsStable = false,
                Description = "正在保存信息",
                Handler = new SavingInformationHandler()
            },
            // 9: SavingInformationInThread
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.SavingInformationInThread,
                ValidTransitions = [ChunkStateNode.DeletingFromMemory],
                Priority = 15,
                IsStable = false,
                Description = "正在线程保存信息",
                Handler = new SavingInformationInThreadHandler()
            },
            // 10: LoadingInGame
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.LoadingInGame,
                ValidTransitions = [ChunkStateNode.LoadedInGame],
                Priority = 5,
                IsStable = false,
                Description = "正在加载于游戏",
                Handler = new LoadingInGameHandler()
            },
            // 11: DeletingFromGame
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.DeletingFromGame,
                ValidTransitions = [ChunkStateNode.LoadedInMemory],
                Priority = 5,
                IsStable = false,
                Description = "正在卸载于游戏",
                Handler = new DeletingFromGameHandler()
            },
            // 12: LoadedInGame
            new ChunkStateNodeInfo
            {
                Node = ChunkStateNode.LoadedInGame,
                ValidTransitions = [ChunkStateNode.DeletingFromGame],
                Priority = 5,
                IsStable = true,
                Description = "已加载于游戏",
                Handler = null
            },
        ];


        /// <summary>
        /// 节点枚举数量
        /// </summary>
        private static readonly int NodeCount = Enum.GetValues(typeof(ChunkStateNode)).Length;

        /// <summary>
        /// 路径索引数组大小
        /// </summary>
        private static readonly int PathLookupSize = NodeCount * NodeCount;

        /// <summary>
        /// 获取路径索引
        /// </summary>
        /// <param name="from">起始节点</param>
        /// <param name="to">目标节点</param>
        /// <returns>数组索引</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPathIndex(ChunkStateNode from, ChunkStateNode to)
        {
            return (int)from * NodeCount + (int)to;
        }

        /// <summary>
        /// 从路径索引获取起始节点和目标节点
        /// </summary>
        /// <param name="index">路径索引</param>
        /// <param name="isValidateIndex">是否验证索引在有效范围内</param>
        /// <returns>起始节点和目标节点的元组</returns>
        /// <exception cref="ArgumentOutOfRangeException">索引超出有效范围时抛出</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static (ChunkStateNode from, ChunkStateNode to) GetPathNodesFromIndex(int index, bool isValidateIndex = true)
        {
            if (isValidateIndex && (index < 0 || index >= PathLookupSize))
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"索引 {index} 超出有效范围 [0, {PathLookupSize})");
            }
            ChunkStateNode from = (ChunkStateNode)(index / NodeCount);
            ChunkStateNode to = (ChunkStateNode)(index % NodeCount);
            return (from, to);
        }


        /// <summary>
        /// 稳定节点路径表（宏观导航）
        /// <para>第一维索引: from * NodeCount + to</para>
        /// <para>第二维索引: 节点枚举值</para>
        /// <para>Value: 距离终点的步数，非稳定节点使用 int.MinValue 表示</para>
        /// </summary>
        private static readonly int[][] _stablePathLookup = new int[PathLookupSize][];

        /// <summary>
        /// 获取稳定路径表条目
        /// </summary>
        /// <param name="from">起始节点</param>
        /// <param name="to">目标节点</param>
        /// <returns>稳定路径表条目（int数组，索引为节点枚举值）</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int[] GetStablePathLookup(ChunkStateNode from, ChunkStateNode to)
        {
            int index = GetPathIndex(from, to);
            return _stablePathLookup[index];
        }


        /// <summary>
        /// 详细路径表（微观导航）
        /// <para>第一维索引: from * NodeCount + to</para>
        /// <para>第二维索引: 节点枚举值</para>
        /// <para>Value: true 表示该节点在路径上</para>
        /// </summary>
        private static readonly bool[][] _detailedPathLookup = new bool[PathLookupSize][];

        /// <summary>
        /// 获取详细路径表条目
        /// </summary>
        /// <param name="from">起始节点</param>
        /// <param name="to">目标节点</param>
        /// <returns>详细路径表条目（bool数组，索引为节点枚举值）</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool[] GetDetailedPathLookup(ChunkStateNode from, ChunkStateNode to)
        {
            int index = GetPathIndex(from, to);
            return _detailedPathLookup[index];
        }


        /// <summary>
        /// 稳定节点邻接图
        /// <para>索引为 ChunkStateNode 枚举值，值为该稳定节点的邻近稳定节点列表。</para>
        /// </summary>
        private static readonly List<ChunkStateNode>[] _stableAdjacency = new List<ChunkStateNode>[NodeCount];

        /*******************************
                  路径初始化
        ********************************/

        /// <summary>
        /// 静态构造函数，初始化路径数据
        /// </summary>
        static ChunkState()
        {
            InitializePaths();
        }

        /// <summary>
        /// 初始化两级路径表
        /// </summary>
        private static void InitializePaths()
        {
            // 1. 构建详细路径表 (微观导航)
            if (!InitializeDetailedPaths())
            {
                GD.PushError("ChunkState: 详细路径初始化失败！");
                return;
            }

            // 2. 构建稳定节点路径表 (宏观导航)
            if (!InitializeStablePaths())
            {
                GD.PushError("ChunkState: 稳定路径初始化失败！");
                return;
            }
            
            int detailedCount = _detailedPathLookup.Count(x => x != null);
            int stableCount = _stablePathLookup.Count(x => x != null);
            GD.Print($"ChunkState: 路径初始化完成。详细规则: {detailedCount}, 稳定路由: {stableCount}");
        }

        /// <summary>
        /// 第一阶段：初始化详细路径（微观导航）,基本单位为稳定节点到邻近稳定节点的路径
        /// <para>从稳定节点出发，仅经过普通节点，枚举到达邻近稳定节点的所有可达路径。</para>
        /// <para>将路径上的节点并入 _detailedPathLookup，用于微观下一跳判断。</para>
        /// </summary>
        /// <returns>是否初始化成功</returns>
        private static bool InitializeDetailedPaths()
        {
            // 遍历所有稳定节点作为起始点
            foreach (ChunkStateNode startStable in StableNodes)
            {
                // 遍历计数，用于死循环熔断
                int traversalCount = 0;
                // 迭代式 DFS（非递归）

                // 当前路径栈（栈顶为当前节点）
                Stack<ChunkStateNode> pathStack = [];
                pathStack.Push(startStable);
                // 栈顶节点的邻居处理进度
                List<int> neighborIndexStack = [0];
                // 当前路径去重集合，防止环路
                HashSet<ChunkStateNode> visitedInPath = [startStable];

                // 迭代式 DFS 主循环
                while (pathStack.Count > 0)
                {
                    // 当前节点
                    ChunkStateNode currentNode = pathStack.Peek();
                    // 当前节点的邻居列表
                    ChunkStateNode[] neighbors = GetValidTransitions(currentNode);
                    // 当前节点的邻居处理进度
                    int neighborIndex = neighborIndexStack[^1];

                    // 如果还有未处理的邻居
                    if (neighborIndex < neighbors.Length)
                    {
                        ChunkStateNode neighbor = neighbors[neighborIndex];
                        // 推进邻居处理进度
                        neighborIndexStack[^1] = neighborIndex + 1;
                        // 若邻居已在当前路径中，则跳过
                        if (visitedInPath.Contains(neighbor)) continue;
                        // 遇到稳定节点：记录可达路径集合(继续遍历，不对稳定节点进行深入,因为局限于到邻近稳定节点间的节点路径)
                        if (IsStable(neighbor))
                        {
                            int pathIndex = GetPathIndex(startStable, neighbor);
                            // 若该路径条目尚未初始化，则初始化 bool 数组
                            if (_detailedPathLookup[pathIndex] == null)
                                _detailedPathLookup[pathIndex] = new bool[NodeCount];
                            // 标记当前路径上的所有普通节点
                            foreach (ChunkStateNode node in pathStack)
                            {
                                _detailedPathLookup[pathIndex][(int)node] = true;
                            }
                            // 标记遇到的稳定节点
                            _detailedPathLookup[pathIndex][(int)neighbor] = true;
                        }
                        // 普通节点：继续深入
                        else
                        {
                            pathStack.Push(neighbor);
                            visitedInPath.Add(neighbor);
                            // 新节点邻居进度从 0 开始
                            neighborIndexStack.Add(0);
                        }
                    }
                    // 若所有邻居处理完毕，则回溯
                    else
                    {
                        pathStack.Pop();
                        visitedInPath.Remove(currentNode);
                        neighborIndexStack.RemoveAt(neighborIndexStack.Count-1);
                    }
                    // 以下为死循环熔断机制
                    traversalCount++;
                    if (traversalCount > 1024)// 防止死循环
                    {
                        GD.PushWarning($"ChunkState: 初始化详细路径时检测到潜在死循环，起始节点: {startStable}");
                        return false;
                    }
                }
            }
            return true;
        }


        /// <summary>
        /// 第二阶段：初始化稳定节点路径（宏观导航）
        /// <para>从详细路径表推导稳定节点邻接关系，并计算稳定节点间的路径距离。</para>
        /// </summary>
        private static bool InitializeStablePaths()
        {
            // *构建稳定节点邻接图*
            
            // 构建邻接关系：遍历所有稳定节点对
            foreach (ChunkStateNode from in StableNodes)
            {
                _stableAdjacency[(int)from] = [];
                foreach (ChunkStateNode to in StableNodes)
                {
                    if (from == to) continue;
                    if (GetDetailedPathLookup(from, to) != null)
                    {
                        _stableAdjacency[(int)from].Add(to);
                    }
                }
            }

            // *遍历稳定节点，计算到其他稳定节点的距离*
            foreach (ChunkStateNode startStable in StableNodes)
            {
                // 遍历计数，防止死循环
                int traversalCount = 0;
                
                // 当前宏观路径栈
                List<ChunkStateNode> pathStack = [startStable];
                // 邻居索引栈：记录每层邻居处理进度
                List<int> neighborIndexStack = [0];
                // 当前路径去重集合
                HashSet<ChunkStateNode> visitedInPath = [startStable];

                while (pathStack.Count > 0)
                {
                    // 当前节点
                    int stackTopIndex = pathStack.Count - 1;
                    ChunkStateNode currentNode = pathStack[stackTopIndex];
                    
                    // 获取当前节点的邻居处理进度
                    int currentNeighborIndex = neighborIndexStack[stackTopIndex];

                    // 获取邻居列表
                    List<ChunkStateNode> neighbors = _stableAdjacency[(int)currentNode];

                    // 若还有未处理的邻居
                    if (currentNeighborIndex < neighbors.Count)
                    {
                        ChunkStateNode neighbor = neighbors[currentNeighborIndex];
                        // 推进邻居处理进度
                        neighborIndexStack[stackTopIndex] = currentNeighborIndex + 1;
                        // 若邻居已在当前路径中，则跳过
                        if (visitedInPath.Contains(neighbor)) continue;
                        // 入栈继续深入
                        pathStack.Add(neighbor);
                        visitedInPath.Add(neighbor);
                        neighborIndexStack.Add(0);

                        // 记录路径距离：Start -> ... -> Neighbor
                        int pathindex = GetPathIndex(startStable, neighbor);
                        // 若该路径条目尚未初始化，则初始化 int 数组
                        if (_stablePathLookup[pathindex] == null)
                        {
                            _stablePathLookup[pathindex] = new int[NodeCount];
                            // 初始化所有值为 int.MinValue（表示不在路径上）
                            for (int nodeIndex = 0; nodeIndex < NodeCount; nodeIndex++)
                            {
                                _stablePathLookup[pathindex][nodeIndex] = int.MinValue;
                            }
                        }
                        // 记录路径中各节点到目标 neighbor 的距离
                        for (int nodeIndex = 0; nodeIndex < pathStack.Count; nodeIndex++)
                        {
                            ChunkStateNode nodeOnPath = pathStack[nodeIndex];
                            // 目标在末尾，距离为 (L-1) - i(nodeIndex)
                            int distToTarget = pathStack.Count - 1 - nodeIndex;
                            
                            // 记录最短距离
                            int existingDist = _stablePathLookup[pathindex][(int)nodeOnPath];
                            if (existingDist == int.MinValue || distToTarget < existingDist)
                            {
                                _stablePathLookup[pathindex][(int)nodeOnPath] = distToTarget;
                            }
                        }
                        // 继续深入
                        continue;
                    }
                    // 若所有邻居都已处理完毕
                    else
                    {
                        // 回溯
                        pathStack.RemoveAt(stackTopIndex);
                        visitedInPath.Remove(currentNode);
                        neighborIndexStack.RemoveAt(stackTopIndex);
                    }
                    // 死循环检测
                    traversalCount++;
                    if (traversalCount > 1024)// 防止死循环
                    {
                        GD.PushWarning($"ChunkState: 初始化稳定路径时检测到潜在死循环，起始节点: {startStable}");
                        return false;
                    }
                }
            }
            return true;
        }


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
        private readonly bool[] _blockedNodes = new bool[NodeCount];

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
            if (IsStable(finalNode) == false)
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
            int[] pathDistances = GetStablePathLookup(CurrentStableNode, FinalStableNode);
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
                foreach (ChunkStateNode candidate in _stableAdjacency[(int)CurrentStableNode])
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
                    OwnerChunk?.NotifyStateStableReached(new ChunkStateStableReachedEventArgs(OwnerChunk.CPosition, CurrentStableNode, TargetStableNode));
                    // 到达了中间的目标稳定节点，则更新当前稳定节点
                    CurrentStableNode = CurrentNode;
                }
                // 重新计算下一个中间目标,若下一个中间目标不存在,则返回失败
                if (RecalculateTargetStable()==false)
                    return false;
                // 当成功获取新的目标稳定节点 或 已到达终点稳定节点时 ,认定为成功
                return true;
            }
            // 获取当前稳定节点通往目标稳定节点的路径，若不存在则返回失败
            // 目标稳定节点是当前稳定节点的邻近稳定节点，因此可通过 _detailedPathLookup 获取两者间的路径
            bool[] pathLookup = GetDetailedPathLookup(CurrentStableNode, TargetStableNode);
            if (pathLookup == null)
            {
                return false;
            }
            // 若目标节点为空,则需要选择一个目标节点(下一跳的节点)
            if (TargetNode==null)
            { 
                // 获取当前节点导向的邻居节点们
                ChunkStateNode[] transitions = GetValidTransitions(CurrentNode);
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
                    int priority = GetPriority(transitionNode);
                    // 优先级为负数则不考虑
                    if (IsNodeGlobalDisabled(transitionNode)==true) continue;

                    // 更新最高优先级
                    if (priority > maxPriority)
                    {
                        maxPriority = priority;
                        bestTransitionNode = transitionNode;
                    }
                }
                // 若没有最优的下一跳节点,则返回错误,并尝试释放阻塞表里的节点(为了防止原地打转,只能被迫再次尝试被阻塞的节点)
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
                        GD.PushError($"ChunkState: 没有找到最优的下一跳节点,但阻塞表不为空,已清空阻塞表腾出更多选择,当前节点: {CurrentNode},阻塞表: {blockedNodeNames.ToString()}");
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
            StateHandler handler = GetHandler((ChunkStateNode)TargetNode);
            // 执行目标节点的回调操作的结果,true=成功,false=临时失败,null为永久性的失败(或较长时间内保持失败的阻塞),若回调为None则代表无需执行回调操作
            bool? executeResult = handler is null ? true : handler.Execute(OwnerChunk);
            // 若回调操作返回null则视为永久错误，将当前节点加入阻塞表并回退至前节点
            // 例外情况是目标节点被阻塞完了（所有可能目标节点均被阻塞），此时会清空阻塞表重新尝试。
            // 说明:发生null永久失败的情况并不是异常的，如果执行回调函数自身发生错误，报错应当在其内部执行
            if (executeResult == null)
            {
                // 将当前节点加入阻塞表（永久错误，当前节点无法推进）
                BlockNode(CurrentNode);
                // 回退至前一个节点（如果前节点存在且不是当前节点）
                if (PreviousNode != null && PreviousNode != CurrentNode)
                {
                    CurrentNode = PreviousNode.Value;
                    // 清空阻塞表？不清空，因为阻塞表记录了永久错误节点
                }
                TargetNode = null;
                return false;
            }
            // 若回调操作返回true则视为成功,更新当前节点为目标节点,清除阻塞表,并更新前节点
            else if (executeResult == true)
            {
                // 更新前节点为本次更新前的节点
                PreviousNode = CurrentNode;
                // 变更当前节点为目标节点
                CurrentNode = (ChunkStateNode)TargetNode;
                TargetNode = null;
                ClearBlockedNodes();
                return true;
            }
            // 若回调操作返回false则视为临时失败,保持现状
            else
            {
                return false;
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
            return StateNodeInfo[(int)node].ValidTransitions;
        }

        /// <summary>
        /// 获取指定状态节点的状态处理器
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>该节点的状态处理器</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StateHandler GetHandler(ChunkStateNode node)
        {
            return StateNodeInfo[(int)node].Handler;
        }

        /// <summary>
        /// 获取指定状态节点的优先级
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>该节点的优先级</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetPriority(ChunkStateNode node)
        {
            return StateNodeInfo[(int)node].Priority;
        }

        /// <summary>
        /// 判断指定状态节点是否为稳定状态
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>如果节点是稳定状态则返回true，否则返回false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStable(ChunkStateNode node)
        {
            return StateNodeInfo[(int)node].IsStable;
        }



        /// <summary>
        /// 查询节点是否被全局禁用（优先级为负）
        /// </summary>
        /// <param name="node">要查询的节点</param>
        /// <returns>若被全局禁用返回 true，否则返回 false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNodeGlobalDisabled(ChunkStateNode node)
        {
            return StateNodeInfo[(int)node].Priority < 0;
        }

        /// <summary>
        /// 将节点全局禁用：优先级设为负数（0 会被设置为 int.MinValue）
        /// </summary>
        /// <param name="node">要禁用的节点</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisableNodeGlobal(ChunkStateNode node)
        {
            int index = (int)node;
            ref ChunkStateNodeInfo info = ref StateNodeInfo[index];
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
        public static void RestoreNodePriority(ChunkStateNode node)
        {
            int index = (int)node;
            if (StateNodeInfo[index].Priority < 0)
                if (StateNodeInfo[index].Priority == int.MinValue)
                    StateNodeInfo[index].Priority = 0;
                StateNodeInfo[index].Priority = Math.Abs(StateNodeInfo[index].Priority);
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
