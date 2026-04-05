using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using System.Runtime.CompilerServices;
using WorldWeaver.MapSystem.ChunkSystem.State.Handler;

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
        public static readonly ChunkStateNode[] StableNodes = 
        [
            ChunkStateNode.Enter,
            ChunkStateNode.NotInMemory,
            ChunkStateNode.LoadedInMemory,
            ChunkStateNode.LoadedInGame,
            ChunkStateNode.Exit
        ];

        /// <summary>状态节点信息数组</summary>
        private static readonly ChunkStateNodeInfo[] _stateNodesInfo = 
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
        private static readonly int _nodeCount = Enum.GetValues(typeof(ChunkStateNode)).Length;

        /// <summary>
        /// 路径索引数组大小
        /// </summary>
        private static readonly int _pathLookupSize = _nodeCount * _nodeCount;

        /// <summary>
        /// 获取路径索引
        /// </summary>
        /// <param name="from">起始节点</param>
        /// <param name="to">目标节点</param>
        /// <returns>数组索引</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetPathIndex(ChunkStateNode from, ChunkStateNode to)
        {
            return (int)from * _nodeCount + (int)to;
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
            if (isValidateIndex && (index < 0 || index >= _pathLookupSize))
            {
                throw new ArgumentOutOfRangeException(nameof(index), $"索引 {index} 超出有效范围 [0, {_pathLookupSize})");
            }
            ChunkStateNode from = (ChunkStateNode)(index / _nodeCount);
            ChunkStateNode to = (ChunkStateNode)(index % _nodeCount);
            return (from, to);
        }


        /// <summary>
        /// 稳定节点路径表（宏观导航）
        /// <para>第一维索引: from * _nodeCount + to</para>
        /// <para>第二维索引: 节点枚举值</para>
        /// <para>Value: 距离终点的步数，非稳定节点使用 int.MinValue 表示</para>
        /// </summary>
        private static readonly int[][] _stablePathLookup = new int[_pathLookupSize][];

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
        /// <para>第一维索引: from * _nodeCount + to</para>
        /// <para>第二维索引: 节点枚举值</para>
        /// <para>Value: true 表示该节点在路径上</para>
        /// </summary>
        private static readonly bool[][] _detailedPathLookup = new bool[_pathLookupSize][];

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
        private static readonly ChunkStateNode[][] _stableAdjacency = new ChunkStateNode[_nodeCount][];

        /// <summary>
        /// 获取指定稳定节点的邻近稳定节点列表
        /// </summary>
        /// <param name="stableNode">稳定节点</param>
        /// <returns>邻近稳定节点列表</returns>
        /// <exception cref="ArgumentException">如果传入的节点不是稳定节点</exception>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ChunkStateNode[] GetStableAdjacency(ChunkStateNode stableNode)
        {
            if (!IsStable(stableNode))
            {
                throw new ArgumentException($"节点 {stableNode} 不是稳定节点", nameof(stableNode));
            }
            return _stableAdjacency[(int)stableNode];
        }

        /*******************************
                  路径初始化
        ********************************/

        /// <summary>
        /// 静态构造函数，初始化路径数据
        /// </summary>
        static ChunkStateMachine()
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
                GD.PushError("ChunkStateMachine: 详细路径初始化失败！");
                return;
            }

            // 2. 构建稳定节点路径表 (宏观导航)
            if (!InitializeStablePaths())
            {
                GD.PushError("ChunkStateMachine: 稳定路径初始化失败！");
                return;
            }
            
            int detailedCount = _detailedPathLookup.Count(x => x != null);
            int stableCount = _stablePathLookup.Count(x => x != null);
            GD.Print($"ChunkStateMachine: 路径初始化完成。详细规则: {detailedCount}, 稳定路由: {stableCount}");
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
                            _detailedPathLookup[pathIndex] ??= new bool[_nodeCount];
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
                        GD.PushWarning($"ChunkStateMachine: 初始化详细路径时检测到潜在死循环，起始节点: {startStable}");
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
                List<ChunkStateNode> adjacencyList = [];
                foreach (ChunkStateNode to in StableNodes)
                {
                    if (from == to) continue;
                    if (GetDetailedPathLookup(from, to) != null)
                    {
                        adjacencyList.Add(to);
                    }
                }
                _stableAdjacency[(int)from] = adjacencyList.ToArray();
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
                    ChunkStateNode[] neighbors = _stableAdjacency[(int)currentNode];

                    // 若还有未处理的邻居
                    if (currentNeighborIndex < neighbors.Length)
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
                        int pathIndex = GetPathIndex(startStable, neighbor);
                        // 若该路径条目尚未初始化，则初始化 int 数组
                        if (_stablePathLookup[pathIndex] == null)
                        {
                            _stablePathLookup[pathIndex] = new int[_nodeCount];
                            // 初始化所有值为 int.MinValue（表示不在路径上）
                            for (int nodeIndex = 0; nodeIndex < _nodeCount; nodeIndex++)
                            {
                                _stablePathLookup[pathIndex][nodeIndex] = int.MinValue;
                            }
                        }
                        // 记录路径中各节点到目标 neighbor 的距离
                        for (int nodeIndex = 0; nodeIndex < pathStack.Count; nodeIndex++)
                        {
                            ChunkStateNode nodeOnPath = pathStack[nodeIndex];
                            // 目标在末尾，距离为 (L-1) - i(nodeIndex)
                            int distToTarget = pathStack.Count - 1 - nodeIndex;
                            
                            // 记录最短距离
                            int existingDist = _stablePathLookup[pathIndex][(int)nodeOnPath];
                            if (existingDist == int.MinValue || distToTarget < existingDist)
                            {
                                _stablePathLookup[pathIndex][(int)nodeOnPath] = distToTarget;
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
                        GD.PushWarning($"ChunkStateMachine: 初始化稳定路径时检测到潜在死循环，起始节点: {startStable}");
                        return false;
                    }
                }
            }
            return true;
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
            return _stateNodesInfo[(int)node].ValidTransitions;
        }

        /// <summary>
        /// 获取指定状态节点的状态处理器
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>该节点配置的状态处理器；若未配置则返回 null。</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static StateHandler GetHandler(ChunkStateNode node)
        {
            return _stateNodesInfo[(int)node].Handler;
        }

        /// <summary>
        /// 获取指定状态节点的优先级
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>该节点的优先级</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int GetPriority(ChunkStateNode node)
        {
            return _stateNodesInfo[(int)node].Priority;
        }

        /// <summary>
        /// 判断指定状态节点是否为稳定状态
        /// </summary>
        /// <param name="node">要查询的状态节点</param>
        /// <returns>如果节点是稳定状态则返回true，否则返回false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsStable(ChunkStateNode node)
        {
            return _stateNodesInfo[(int)node].IsStable;
        }


        /// <summary>
        /// 查询节点是否被全局禁用（优先级为负）
        /// </summary>
        /// <param name="node">要查询的节点</param>
        /// <returns>若被全局禁用返回 true，否则返回 false</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsNodeGlobalDisabled(ChunkStateNode node)
        {
            return _stateNodesInfo[(int)node].Priority < 0;  
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
            ChunkStateNodeInfo info = _stateNodesInfo[index];
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
            if (_stateNodesInfo[index].Priority < 0)
            {
                if (_stateNodesInfo[index].Priority == int.MinValue)
                {
                    _stateNodesInfo[index].Priority = 0;
                    return true;
                }
                _stateNodesInfo[index].Priority = Math.Abs(_stateNodesInfo[index].Priority);
                return true;
            }

            return false;
        }
    }
}
