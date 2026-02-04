using UnityEngine;

namespace YokiFrame.ECS.Examples
{
    /// <summary>
    /// 输入组件
    /// </summary>
    public struct InputData : IComponentData
    {
        public float Horizontal;
        public float Vertical;
    }
    
    /// <summary>
    /// 玩家标签
    /// </summary>
    public struct PlayerTag : IComponentData { }
    
    /// <summary>
    /// 敌人标签
    /// </summary>
    public struct EnemyTag : IComponentData { }
    
    /// <summary>
    /// 道具标签
    /// </summary>
    public struct PowerUpTag : IComponentData { }
}
