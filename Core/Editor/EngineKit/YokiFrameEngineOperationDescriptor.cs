#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 描述一个引擎操作 action 的风险等级与可执行性分类；是 Engine Kit 命令面的最小事实源。
    /// </summary>
    public sealed class YokiFrameEngineOperationDescriptor
    {
        /// <summary>
        /// 创建操作描述。
        /// </summary>
        /// <param name="action">action 标识。</param>
        /// <param name="kind">风险等级。</param>
        /// <param name="isDiagnostic">是否为诊断/查询类操作；该类操作不受执行开关阻断。</param>
        /// <param name="isCancellation">是否为取消类操作；关闭开关时仍需可用于停止既有运行。</param>
        /// <param name="targets">该操作支持的执行目标；默认仅 editor。</param>
        /// <param name="isTargetAgnostic">是否为目标无关操作（诊断与能力查询）；此类操作只受执行开关约束，不受目标绑定限制。</param>
        public YokiFrameEngineOperationDescriptor(
            string action,
            YokiFrameCommandKind kind,
            bool isDiagnostic = false,
            bool isCancellation = false,
            YokiFrameEngineExecutionTarget targets = YokiFrameEngineExecutionTarget.Editor,
            bool isTargetAgnostic = false,
            Func<bool> executionPermission = null)
        {
            if (string.IsNullOrEmpty(action))
            {
                throw new ArgumentNullException(nameof(action));
            }

            Action = action;
            Kind = kind;
            IsDiagnostic = isDiagnostic;
            IsCancellation = isCancellation;
            Targets = targets;
            IsTargetAgnostic = isTargetAgnostic;
            mExecutionPermission = executionPermission;
        }

        private readonly Func<bool> mExecutionPermission;
        public bool ExecutionPermitted { get { return mExecutionPermission == null || mExecutionPermission(); } }

        /// <summary>获取 action 标识。</summary>
        public string Action { get; }

        /// <summary>获取风险等级。</summary>
        public YokiFrameCommandKind Kind { get; }

        /// <summary>获取是否为诊断/查询类操作。</summary>
        public bool IsDiagnostic { get; }

        /// <summary>获取是否为取消类操作；关闭开关时仍可用于停止既有运行。</summary>
        public bool IsCancellation { get; }

        /// <summary>获取该操作支持的执行目标；仅作为能力声明，请求不接受组合值。</summary>
        public YokiFrameEngineExecutionTarget Targets { get; }

        /// <summary>获取是否为目标无关操作；为 true 时目标绑定校验被跳过。</summary>
        public bool IsTargetAgnostic { get; }

        /// <summary>判断操作是否支持指定目标。</summary>
        /// <param name="target">待判断目标。</param>
        /// <returns>支持时返回 true。</returns>
        public bool Supports(YokiFrameEngineExecutionTarget target)
        {
            return (Targets & target) != 0;
        }

        /// <summary>
        /// 获取执行开关豁免是否成立：诊断/查询与取消类操作在关闭状态下仍可用，
        /// 但标记为 Dangerous 的操作永不豁免。
        /// </summary>
        public bool ExemptFromExecutionSwitch
        {
            get { return (IsDiagnostic || IsCancellation) && Kind != YokiFrameCommandKind.Dangerous; }
        }

        /// <summary>
        /// 转换为 CommandBridge 命令描述。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <returns>命令描述。</returns>
        public YokiFrameCommandDescriptor ToCommandDescriptor(string kit)
        {
            return new YokiFrameCommandDescriptor(kit, Action, Kind);
        }
    }
}
#endif
