using Godot;
using System.Collections.Generic;
using WorldWeaver.CounterSystem;

namespace WorldWeaver.MapSystem
{
    [GlobalClass]
    public partial class World : Node
    {
        /*******************************
                  ID系统
        ********************************/
        
        /// <summary>
        /// World实例ID分配计数器
        /// </summary>
        private static readonly Counter _idCounter = new("WorldIdCounter");
        
        /// <summary>
        /// World实例映射表，以ID为键存储所有World实例
        /// </summary>
        private static readonly Dictionary<int, World> _worldInstances = [];
        
        /// <summary>
        /// World实例的唯一标识符
        /// </summary>
        private int _worldId;

        /// <summary>
        /// 获取World实例的唯一标识符
        /// </summary>
        public int WorldId => _worldId;


        /// <summary>
        /// 根据ID获取World实例
        /// </summary>
        /// <param name="id">World的ID</param>
        /// <returns>对应的World实例，如果不存在则返回null</returns>
        public static World GetById(int id)
        {
            return _worldInstances.GetValueOrDefault(id,null);
        }


        /*******************************
                  构造与销毁
        ********************************/
        
        public World()
        {
            // 分配ID
            _worldId = (int)_idCounter.GetAndIncrement();
            _worldInstances[_worldId] = this;
        }

        /// <summary>
        /// 从实例映射中移除当前World实例
        /// </summary>
        private void Cleanup()
        {
            // 从实例映射中移除,Remove方法保证了如果键不存在，不会抛出异常
            _worldInstances.Remove(_worldId);
        }
        
        /// <summary>
        /// 当World节点从场景树中移除时，自动清理实例映射
        /// </summary>
        public override void _ExitTree()
        {
            Cleanup();
        }
    }
}
