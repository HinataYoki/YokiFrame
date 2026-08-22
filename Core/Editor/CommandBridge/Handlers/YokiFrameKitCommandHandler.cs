#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 为单个 Kit 提供 action allowlist 和执行入口的基础 handler。
    /// handler 自身声明的 <see cref="Descriptors"/> 是该 Kit 命令面的单一事实源，
    /// 宿主策略必须由它聚合生成，禁止在策略侧再维护第二份命令清单。
    /// </summary>
    public abstract class YokiFrameKitCommandHandler : IYokiFrameCommandHandler
    {
        private readonly string mKit;
        private readonly string[] mActions;
        private readonly YokiFrameCommandDescriptor[] mDescriptors;

        /// <summary>
        /// 创建 Kit handler；调用方传入该 handler 支持的 action 集合。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="actions">该 Kit 下允许当前 handler 处理的 action。</param>
        protected YokiFrameKitCommandHandler(string kit, string[] actions)
            : this(kit, CreateReadOnlyDescriptors(kit, actions))
        {
        }

        /// <summary>
        /// 创建 Kit handler；调用方传入该 handler 支持的完整命令描述（含每个 action 的 Kind）。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="descriptors">该 Kit 下允许当前 handler 处理的命令描述。</param>
        protected YokiFrameKitCommandHandler(string kit, YokiFrameCommandDescriptor[] descriptors)
        {
            if (descriptors == null)
            {
                throw new ArgumentNullException(nameof(descriptors));
            }

            mKit = kit ?? throw new ArgumentNullException(nameof(kit));
            mDescriptors = descriptors;
            mActions = new string[descriptors.Length];
            for (var index = 0; index < descriptors.Length; index++)
            {
                mActions[index] = descriptors[index].Action;
            }
        }

        /// <summary>
        /// 获取当前 handler 声明的命令描述；宿主策略由此聚合，保证 allowlist 与可执行命令一致。
        /// </summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Descriptors
        {
            get { return mDescriptors; }
        }

        /// <summary>
        /// 判断请求是否属于当前 Kit 且 action 在 handler 支持范围内。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>匹配时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            if (request == null || !string.Equals(request.Kit, mKit, StringComparison.Ordinal))
            {
                return false;
            }

            return ContainsAction(request.Action);
        }

        /// <summary>
        /// 执行当前 Kit 的命令；不匹配的命令返回终态错误，避免调用侧超时。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令终态结果。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            if (!CanHandle(request))
            {
                return YokiFrameCommandResult.Error("HandlerMismatch", "Command handler does not support this kit or action.");
            }

            return HandleAction(request);
        }

        /// <summary>
        /// 执行已匹配当前 Kit/action 的命令。
        /// </summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令终态结果。</returns>
        protected abstract YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request);

        /// <summary>
        /// 判断 action 是否在当前 handler 支持列表中。
        /// </summary>
        /// <param name="action">action 标识。</param>
        /// <returns>命中时返回 true。</returns>
        private bool ContainsAction(string action)
        {
            for (var index = 0; index < mActions.Length; index++)
            {
                if (string.Equals(action, mActions[index], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 把纯 action 列表转换为默认 ReadOnly 的命令描述，供旧构造路径保持统一描述来源。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="actions">action 集合。</param>
        /// <returns>与 action 列表一一对应的 ReadOnly 描述。</returns>
        private static YokiFrameCommandDescriptor[] CreateReadOnlyDescriptors(string kit, string[] actions)
        {
            if (actions == null)
            {
                throw new ArgumentNullException(nameof(actions));
            }

            var descriptors = new YokiFrameCommandDescriptor[actions.Length];
            for (var index = 0; index < actions.Length; index++)
            {
                descriptors[index] = new YokiFrameCommandDescriptor(kit, actions[index], YokiFrameCommandKind.ReadOnly);
            }

            return descriptors;
        }
    }
}
#endif
