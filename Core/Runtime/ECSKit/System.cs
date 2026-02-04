using System;

namespace YokiFrame.ECS
{
    /// <summary>
    /// 系统更新阶段
    /// Creation -> Logic -> Destruction -> Sync
    /// </summary>
    public enum SystemPhase
    {
        /// <summary>
        /// 创建阶段 - 处理 SpawnRequest，统一创建实体
        /// </summary>
        Creation = 0,
        
        /// <summary>
        /// 逻辑阶段 - 输入、AI、移动、碰撞检测等所有业务逻辑
        /// 只做标记（SpawnRequest/DestroyTag），不直接创建/销毁
        /// </summary>
        Logic = 1,
        
        /// <summary>
        /// 销毁阶段 - 处理 DestroyTag，统一销毁实体和关联的 GameObject
        /// </summary>
        Destruction = 2,
        
        /// <summary>
        /// 同步阶段 - ECS 数据回写到 GameObject（Transform 等）
        /// </summary>
        Sync = 3
    }
    
    /// <summary>
    /// ECS 系统接口
    /// </summary>
    public interface IECSSystem
    {
        ECSWorld World { get; set; }
        SystemPhase Phase { get; }
        void OnCreate();
        void OnUpdate();
        void OnDestroy();
    }
    
    /// <summary>
    /// ECS 系统基类
    /// </summary>
    public abstract class SystemBase : IECSSystem
    {
        public ECSWorld World { get; set; }
        
        /// <summary>
        /// 系统所属阶段，默认为逻辑阶段
        /// </summary>
        public virtual SystemPhase Phase => SystemPhase.Logic;
        
        public virtual void OnCreate() { }
        public abstract void OnUpdate();
        public virtual void OnDestroy() { }
    }
}
