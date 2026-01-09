using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Buff 接口，定义 Buff 的基本行为和生命周期
    /// </summary>
    public interface IBuff
    {
        /// <summary>
        /// Buff ID
        /// </summary>
        int BuffId { get; }
        
        /// <summary>
        /// 持续时间（秒），-1 表示永久
        /// </summary>
        float Duration { get; }
        
        /// <summary>
        /// Tick 间隔（秒），0 表示不触发
        /// </summary>
        float TickInterval { get; }
        
        /// <summary>
        /// 最大堆叠数
        /// </summary>
        int MaxStack { get; }
        
        /// <summary>
        /// 堆叠模式
        /// </summary>
        StackMode StackMode { get; }
        
        /// <summary>
        /// Buff 标签数组
        /// </summary>
        int[] Tags { get; }
        
        /// <summary>
        /// 排斥标签数组
        /// </summary>
        int[] ExclusionTags { get; }
        
        /// <summary>
        /// Buff 效果列表
        /// </summary>
        IReadOnlyList<IBuffEffect> Effects { get; }

        /// <summary>
        /// 获取属性修改器列表
        /// </summary>
        IReadOnlyList<BuffModifier> Modifiers { get; }

        /// <summary>
        /// Buff 添加时回调
        /// </summary>
        void OnAdd(BuffContainer container, BuffInstance instance);
        
        /// <summary>
        /// Buff 移除时回调
        /// </summary>
        void OnRemove(BuffContainer container, BuffInstance instance);
        
        /// <summary>
        /// Buff 周期触发回调
        /// </summary>
        void OnTick(BuffContainer container, BuffInstance instance);
        
        /// <summary>
        /// Buff 到期时回调
        /// </summary>
        void OnExpire(BuffContainer container, BuffInstance instance);
        
        /// <summary>
        /// 堆叠层数变化时回调
        /// </summary>
        void OnStackChanged(BuffContainer container, BuffInstance instance, int oldStack, int newStack);
    }
}
