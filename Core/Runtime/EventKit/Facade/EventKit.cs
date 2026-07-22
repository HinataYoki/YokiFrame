using System;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 运行时事件门面，提供 Type、Enum 和 String 三种事件总线。
    /// </summary>
    public static class EventKit
    {
        /// <summary>
        /// 按负载类型路由的运行时事件总线。
        /// </summary>
        public static readonly TypeEvent Type = new();

        /// <summary>
        /// 按枚举值路由的运行时事件总线。
        /// </summary>
        public static readonly EnumEvent Enum = new();

        /// <summary>
        /// 按字符串路由的兼容事件总线；新代码优先使用 Type 或 Enum。
        /// </summary>
        [Obsolete("StringEvent 存在类型安全和重构风险，请优先使用 EventKit.Type 或 EventKit.Enum。")]
        public static readonly StringEvent String = new();

        /// <summary>
        /// 清理运行时门面下的全部事件监听器，通常用于测试或宿主卸载。
        /// </summary>
        public static void Clear()
        {
            Type.Clear();
            Enum.Clear();
#pragma warning disable CS0618
            String.Clear();
#pragma warning restore CS0618
        }
    }
}
