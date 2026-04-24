using System;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.ChunkSystem.State;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块运行时实体。
    /// <para>该类只承载自身身份、数据与状态，不再保存上级管理器引用。</para>
    /// <para>状态推进完全由 ChunkManager 直接驱动；Chunk 本体不再包装状态机流程。</para>
    /// </summary>
    public class Chunk : Object, IDisposable
    {
        /*******************************
                  核心属性
        ********************************/

        /// <summary>
        /// 区块位置。
        /// </summary>
        public ChunkPosition CPosition { get; }

        /// <summary>
        /// 区块唯一标识符。
        /// </summary>
        public string Uid { get; }

        /// <summary>
        /// 区块状态。
        /// <para>除非是空 Chunk，否则 State 不可能为 null。</para>
        /// </summary>
        public ChunkState State { get; }

        /// <summary>
        /// 当前状态节点。
        /// <para>若 state 为 null，则返回 Exit。</para>
        /// </summary>
        public ChunkStateNode StateNode => State?.CurrentNode ?? ChunkStateNode.Exit;

        /// <summary>
        /// 最终目标稳定节点。
        /// </summary>
        public ChunkStateNode FinalStateNode
        {
            get => State?.FinalStableNode ?? ChunkStateNode.Exit;
            set => State?.SetFinalStableNode(value);
        }

        /// <summary>
        /// 区块数据。
        /// <para>空 Chunk 始终为 null；非空 Chunk 是否存在数据取决于当前状态与处理流程。</para>
        /// </summary>
        public ChunkData Data { get; private set; }


        /*******************************
                  构造
        ********************************/

        /// <summary>
        /// 空区块实例（空对象模式）。
        /// <para>判断一个区块是否为空区块，请使用 <c>Chunk.IsNullOrEmpty(chunk)</c>。</para>
        /// </summary>
        public static readonly Chunk EMPTY = new();

        /// <summary>
        /// 创建区块。
        /// </summary>
        /// <param name="chunkPosition">区块坐标。</param>
        /// <param name="uid">区块唯一标识符。</param>
        public Chunk(ChunkPosition chunkPosition, string uid)
        {
            // uid 是持久化与诊断链路里的稳定标识，必须提前拦截空值。
            if (string.IsNullOrWhiteSpace(uid))
            {
                throw new ArgumentException("uid 不能为空。", nameof(uid));
            }

            // Chunk 本体只保存局部身份与状态，不注入上级依赖。
            CPosition = chunkPosition;
            Uid = uid;
            State = new ChunkState();

            // 新建后默认目标为“驻留内存”稳定态，后续由 Manager 驱动推进。
            State.SetFinalStableNode(ChunkStateNode.NotInMemory);
        }

        /// <summary>
        /// 仅用于创建空 Chunk。
        /// </summary>
        private Chunk()
        {
            CPosition = ChunkPosition.Zero;
            Uid = null;
            State = null;
            Data = null;
        }

        /// <summary>
        /// 生成区块唯一标识符。
        /// </summary>
        public static string GenerateUid(int worldId, int layerId, ChunkPosition chunkPosition)
        {
            // UID 规则保持固定格式，便于文件路径与日志检索。
            return $"WId{worldId}_LId{layerId}_P({chunkPosition.X},{chunkPosition.Y})";
        }


        /*******************************
                  验证方法
        ********************************/

        /// <summary>
        /// 验证当前区块是否在属性上等同于 Empty。
        /// </summary>
        public bool IsStructurallyEmpty()
        {
            // 空对象模式判断：关键字段均为空且位置为零点。
            return Uid == null &&
                   State == null &&
                   Data == null &&
                   CPosition == ChunkPosition.Zero;
        }

        /// <summary>
        /// 验证指定区块是否在属性上等同于 Empty。
        /// </summary>
        public static bool IsStructurallyEmpty(Chunk chunk)
        {
            // 统一复用实例判断，避免外部重复拼空值条件。
            return chunk != null && chunk.IsStructurallyEmpty();
        }

        /// <summary>
        /// 判断指定区块是否为 null 或空区块实例。
        /// </summary>
        public static bool IsNullOrEmpty(Chunk chunk)
        {
            // 统一收拢 null 与空对象模式判断，避免外部重复书写两段条件。
            return chunk == null || chunk == EMPTY;
        }

        /// <summary>
        /// 验证区块大小指数的有效性。
        /// </summary>
        public static bool ValidateChunkSizeExp(Vector2I chunkSizeExp)
        {
            // 保持与既有逻辑一致：仅校验指数必须为正。
            return chunkSizeExp.X > 0 && chunkSizeExp.Y > 0;
        }


        /*******************************
                  模块创建方法
        ********************************/

        /// <summary>
        /// 设置有效的区块数据。
        /// <para>由于 Chunk 不再持有上级 Layer/Manager，因此期望尺寸由调用方显式传入。</para>
        /// </summary>
        public bool InitializeValidChunkData(ChunkData setData, MapElementSize expectedChunkSize)
        {
            // 先校验输入数据存在性。
            if (setData == null)
            {
                GD.PushError($"Chunk {Uid}: 尝试设置的 ChunkData 实例为 null。");
                return false;
            }

            // 再校验上层传入的期望尺寸存在性，避免无约束写入。
            if (expectedChunkSize == null)
            {
                GD.PushError($"Chunk {Uid}: expectedChunkSize 不能为空。");
                return false;
            }

            // 尺寸不匹配直接拒绝，防止后续索引越界或数据错位。
            if (setData.Width != expectedChunkSize.Width || setData.Height != expectedChunkSize.Height)
            {
                GD.PushError(
                    $"Chunk {Uid}: 尝试设置的 ChunkData 尺寸不匹配。应为 {expectedChunkSize}，设置的实例大小为 ({setData.Width},{setData.Height})。");
                return false;
            }

            // 若当前已经持有旧数据，先释放旧实例，避免遗留无主数据。
            if (Data != null && !ReferenceEquals(Data, setData))
            {
                Data.Dispose();
            }

            // 通过校验后再挂载数据。
            Data = setData;
            return true;
        }

        /// <summary>
        /// 释放当前区块持有的内存数据。
        /// </summary>
        public void ReleaseChunkData()
        {
            if (Data == null)
            {
                return;
            }

            // 先释放底层数据，再清空引用，表示该区块当前不再驻留内存数据。
            Data.Dispose();
            Data = null;
        }

        /*******************************
                  工具方法
        ********************************/

        /// <summary>
        /// 转换为字符串。
        /// </summary>
        public override string ToString()
        {
            // 保持简洁输出，便于日志中快速定位区块。
            return $"Chunk(Uid: {Uid})";
        }

        /// <summary>
        /// 输出包含状态信息的字符串。
        /// </summary>
        public string ToStateString()
        {
            // 提供状态视图字符串，用于调试状态机推进过程。
            return $"Chunk(Uid: {Uid}, StateNode: {StateNode}, FinalStateNode: {FinalStateNode})";
        }


        /*******************************
                  IDisposable 实现
        ********************************/

        /// <summary>
        /// 释放 Chunk 资源。
        /// </summary>
        public void Dispose()
        {
            // 先释放数据再释放状态对象，避免状态回调读到已释放数据。
            Data?.Dispose();
            State?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
