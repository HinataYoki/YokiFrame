#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 描述一个被 CommandPolicy 允许的 Kit/action 命令。
    /// </summary>
    public sealed class YokiFrameCommandDescriptor
    {
        /// <summary>
        /// 创建命令描述；调用方必须传入已经设计过风险等级的 Kit/action。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <param name="kind">命令风险等级。</param>
        public YokiFrameCommandDescriptor(string kit, string action, YokiFrameCommandKind kind)
        {
            Kit = kit ?? throw new ArgumentNullException(nameof(kit));
            Action = action ?? throw new ArgumentNullException(nameof(action));
            Kind = kind;
        }

        /// <summary>
        /// 获取 Kit 标识。
        /// </summary>
        public string Kit { get; }

        /// <summary>
        /// 获取 action 标识。
        /// </summary>
        public string Action { get; }

        /// <summary>
        /// 获取命令风险等级。
        /// </summary>
        public YokiFrameCommandKind Kind { get; }

    }
}
#endif
