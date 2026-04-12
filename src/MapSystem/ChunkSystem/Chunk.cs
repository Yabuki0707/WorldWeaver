using System;
using Godot;
using WorldWeaver.MapSystem.ChunkSystem.Data;
using WorldWeaver.MapSystem.ChunkSystem.State;
using WorldWeaver.MapSystem.ChunkSystem.State.Handler;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块运行时实体。
    /// <para>该类只承载自身身份、数据与状态，不再保存上级管理器引用。</para>
    /// <para>状态推进由 ChunkManager 驱动；Chunk 只负责暴露“当前是否需要执行状态处理器”以及“执行成功后如何推进状态”。</para>
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
            set => State?.SetFinalTarget(value);
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
        /// <para>判断一个区块是否为空区块，请使用 <c>chunk == Chunk.Empty</c>。</para>
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
            State.SetFinalTarget(ChunkStateNode.NotInMemory);
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

            // 通过校验后再挂载数据。
            Data = setData;
            return true;
        }


        /*******************************
                  状态驱动辅助
        ********************************/

        /// <summary>
        /// 获取本轮状态更新所需执行的处理器。
        /// <para>流程如下：</para>
        /// <para>1. 若当前尚无 TargetNode，则先让 State 选择一个目标节点；</para>
        /// <para>2. 若依旧没有目标节点，说明本轮无需推进，返回 null；</para>
        /// <para>3. 若存在目标节点，则按“当前节点”选择处理器；若未配置处理器则在 Chunk 层保底替换为空处理器。</para>
        /// <para>约定：返回 null 仅表示“本轮无更新需求”，不再用于表示“有需求但无需副作用”。</para>
        /// </summary>
        public StateHandler GetStateUpdateHandler()
        {
            // 空 Chunk 或异常状态下直接返回“无需求”。
            if (State == null)
            {
                return null;
            }

            // 目标节点为空时，由 State 负责一次“下一跳”计算。
            if (State.TargetNode == null && !State.SelectTargetNode())
            {
                return null;
            }

            // 计算后依旧为空，表示本轮没有可推进节点。
            if (State.TargetNode == null)
            {
                return null;
            }

            // 处理器按“当前节点”获取；若节点未配置 handler，则在 Chunk 层兜底为空处理器。
            StateHandler handler = ChunkStateMachine.GetHandler(State.CurrentNode);
            return handler ?? EmptyStateHandler.INSTANCE;
        }

        /// <summary>
        /// 在状态处理器执行成功后推进状态。
        /// <para>该方法只处理“成功执行后如何迁移状态”，不负责执行 handler 本身。</para>
        /// </summary>
        public ChunkStateUpdateResult StateUpdate()
        {
            // 状态提交完全委托给 ChunkState，Chunk 仅做流程包装。
            return State?.UpdateToTargetNode();
        }

        /// <summary>
        /// 处理状态处理器执行失败后的状态收敛逻辑。
        /// <para>当前仅在永久失败时执行回退/阻塞处理；RetryLater 保持现状等待下轮继续尝试。</para>
        /// </summary>
        public void HandleStateExecutionFailure(StateExecutionResult executionResult)
        {
            // 失败处理细则在 ChunkState 内集中维护，Chunk 不做二次判定。
            State?.HandleExecutionFailure(executionResult);
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
