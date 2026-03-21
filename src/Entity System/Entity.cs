using Godot;
using System;
using System.Collections.Generic;




[GlobalClass]
public partial class Entity : Node,IDisposable
{
    
    /// <summary>
    /// 代表该实体是否激活
    /// </summary>
    private bool IsActive = true;



    /// <summary>
    /// 反映该节点当前所属的世界的序号。
    /// </summary>
    public int CurrentWorldOrder {get; set;} = 0;

    /// <summary>
    /// 反映该节点当前所属的层级的序号。
    /// </summary>
    public int CurrentLayerOrder {get; set;} = 0;

    /// <summary>
    /// 反映该节点当前所属的区块坐标
    /// </summary>
    public Vector2I? CurrentChunkPosition {get; set;} = null;

    /// <summary>
    /// 实体的唯一标识符
    /// </summary>
    private Guid EntityUID = Guid.Empty;


    /// <summary>
    /// 组件列表
    /// </summary>
    public Dictionary<string,Node> ComponentList = [];

    static Dictionary<Guid, Entity> EntitiesMap = [];


    public Entity()
        {
            this.EntityUID = Guid.NewGuid();
            EntitiesMap[EntityUID]=this;
        }


    static Entity GetEntity(Guid entityUID)
    {
        if (EntitiesMap.ContainsKey(entityUID) == false)
            return null;
        return EntitiesMap[entityUID];
    }

    new void Dispose()
    {
        EntitiesMap.Remove(EntityUID);
    }


}
