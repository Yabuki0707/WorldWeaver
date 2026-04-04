using System;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.State;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块运行时的实体，为 ChunkState、ChunkData 等模块的上级。
    /// <para>管理区块的状态和生命周期。</para>
    /// <para>Chunk 模块的创建必须使用 Chunk 类内部的方法。</para>
    /// </summary>
    public class Chunk : Object, IDisposable
    {

        /*******************************
                  核心属性
        ********************************/

        /// <summary>所属的 ChunkManager</summary>
        public ChunkManager OwnerManager { get; private set; }

        /// <summary>区块位置</summary>
        public ChunkPosition CPosition { get; private set; }

        /// <summary>区块唯一标识符（WorldId + LayerId + Position）</summary>
        public string Uid { get; private set; }

        /// <summary>状态</summary>
        /// <para>除非是空 Chunk，否则 State 不可能是 null</para>
        public ChunkState State { get; private set; }

        /// <summary>
        /// 当前状态节点
        /// <para>若 state（状态机）为 null，则返回 Exit</para>
        /// </summary>
        public ChunkStateNode StateNode => State?.CurrentNode ?? ChunkStateNode.Exit;

        /// <summary>
        /// 终点稳定状态节点（仅供外部读取，实际控制在 State 中）
        /// <para>空区块的终点稳定状态为 Exit</para>
        /// </summary>
        public ChunkStateNode FinalStateNode { get => State?.FinalStableNode ?? ChunkStateNode.Exit; set => State?.SetFinalTarget(value); }


        /// <summary>
        /// 区块数据
        /// </summary>
        /// <para>空 Chunk 始终为 null；非空 Chunk 取决于状态节点</para>
        public ChunkData Data { get; private set; }


        /*******************************
                  构造
        ********************************/

        /// <summary>
        /// 空区块实例（空对象模式）
        /// <para>判断一个区块是否为空区块，请使用 chunk == Chunk.Empty</para>
        /// </summary>
        public static readonly Chunk Empty = new();


        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="ownerManager">所属的管理器</param>
        /// <param name="cPosition">区块位置</param>
        public Chunk(ChunkManager ownerManager, ChunkPosition cPosition)
        {
            OwnerManager = ownerManager;
            CPosition = cPosition;    
            State = new(this);
            State.SetFinalTarget(ChunkStateNode.NotInMemory); // 默认目标是待机状态
            // 生成UID (通过 Manager 访问 Layer)
            Uid = GenerateUid(ownerManager.OwnerLayer.WorldId, ownerManager.OwnerLayer.LayerId, cPosition);
        }

        /// <summary>
        /// 仅用于创建空 Chunk
        /// </summary>
        private Chunk()
        {
            OwnerManager = null;
            CPosition = ChunkPosition.Zero;
            Uid = null;
            State = null;
            Data = null;
        }



        /// <summary>
        /// 生成区块唯一标识符
        /// </summary>
        private static string GenerateUid(int worldId, int layerId, ChunkPosition cPosition)
        {
            return $"WId{worldId}_LId{layerId}_P({cPosition.X},{cPosition.Y})";
        }


        /*******************************
                  验证方法
        ********************************/

        /// <summary>
        /// 验证当前区块是否在属性上等同于 Empty（所有关键属性为 null）
        /// <para>注意：此方法不检查引用相等性 (chunk == Chunk.Empty)，而是检查属性值</para>
        /// </summary>
        public bool IsStructurallyEmpty()
        {
            return OwnerManager == null && 
                   Uid == null && 
                   State == null && 
                   Data == null && 
                   CPosition.X == ChunkPosition.Zero.X &&
                   CPosition.Y == ChunkPosition.Zero.Y;
        }

        /// <summary>
        /// 验证指定区块是否在属性上等同于 Empty（所有关键属性为 null）
        /// <para>注意：此方法不检查引用相等性 (chunk == Chunk.Empty)，而是检查属性值</para>
        /// </summary>
        public static bool IsStructurallyEmpty(Chunk chunk)
        {
            return chunk != null && chunk.IsStructurallyEmpty();
        }

        /// <summary>
        /// 验证区块大小指数的有效性
        /// </summary>
        /// <param name="chunkSizeExp">待验证的区块大小指数</param>
        /// <returns>验证通过返回 true，否则返回 false</returns>
        public static bool ValidateChunkSizeExp(Vector2I chunkSizeExp)
        {
            return chunkSizeExp.X > 0 && chunkSizeExp.Y > 0;
        }


        /*******************************
                  模块创建方法
        ********************************/

        /// <summary>
        /// 设置有效的区块数据（如果数据无效或未初始化，则不设置并报错）
        /// </summary>
        /// <param name="setData">要设置的区块数据</param>
        public bool InitializeValidChunkData(ChunkData setData)
        {
            if (setData == null)
            {
                GD.PushError($"Chunk {Uid}: 尝试设置的ChunkData实例为 null");
                return false;
            }
            if (setData.Width != OwnerManager.OwnerLayer.ChunkSize.Width || setData.Height != OwnerManager.OwnerLayer.ChunkSize.Height)
            {
                GD.PushError($"Chunk {Uid}: 尝试设置的ChunkData的尺寸不匹配。应为 {OwnerManager.OwnerLayer.ChunkSize},设置的实例大小为 ({setData.Width},{setData.Height})");
                return false;
            }

            Data = setData;
            return true;
        }

        /*******************************
                  工具方法
        ********************************/

        /// <summary>
        /// 转换为字符串
        /// </summary>
        public override string ToString()
        {
            return $"Chunk(Uid: {Uid})";
        }

        /// <summary>
        /// 输出包含状态信息的字符串
        /// </summary>
        public string ToStateString()
        {
            return $"Chunk(Uid: {Uid}, StateNode: {StateNode}, FinalStateNode: {FinalStateNode})";
        }


        /*******************************
                  消息传递方法
        ********************************/

        /// <summary>
        /// 向上传递 Chunk 状态稳定变化消息到事件总线
        /// </summary>
        internal void NotifyStateStableReached(ChunkStateNode previousNode, ChunkStateNode newNode)
        {
            NotifyStateStableReached(new ChunkStateStableReachedEventArgs(CPosition, previousNode, newNode));
        }

        /// <summary>
        /// 向上传递 Chunk 状态稳定变化消息到事件总线
        /// </summary>
        internal void NotifyStateStableReached(ChunkStateStableReachedEventArgs args)
        {
            OwnerManager?.OnChunkStateStableReached(args);
        }


        /*******************************
                  IDisposable 实现
        ********************************/

        /// <summary>
        /// 释放 Chunk 资源
        /// </summary>
        public void Dispose()
        {
            // 清理托管资源
            State?.Dispose();
            Data?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
