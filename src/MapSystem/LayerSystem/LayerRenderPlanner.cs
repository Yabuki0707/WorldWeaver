using Godot;

namespace WorldWeaver.MapSystem.LayerSystem
{
    /// <summary>
    /// 层渲染规划器，作为MapLayer和VisualLayer的中间协调层
    /// 负责获取MapLayer内的信息并交由VisualLayer渲染
    /// </summary>
    [GlobalClass]
    public partial class LayerRenderPlanner : Node
    {
        /// <summary>
        /// 关联的MapLayer实例
        /// </summary>
        public MapLayer OwnerLayer { get; set; }

        /// <summary>
        /// 渲染执行层实例
        /// </summary>
        public VisualLayer VisualLayer { get; private set; }


        /// <summary>
        /// 进入树时初始化VisualLayer
        /// </summary>
        public override void _EnterTree()
        {
            VisualLayer = new();
            AddChild(VisualLayer);
        }


        /// <summary>
        /// 退出树时释放VisualLayer资源
        /// </summary>
        public override void _ExitTree()
        {
            if (VisualLayer != null)
            {
                RemoveChild(VisualLayer);
                VisualLayer.QueueFree();
                VisualLayer = null;
            }
        }
    }
}
