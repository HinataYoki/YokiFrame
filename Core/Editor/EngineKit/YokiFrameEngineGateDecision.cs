#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示 Gate 对一次 Engine Kit 命令的裁决；拒绝时带稳定错误码与当前开关状态。
    /// </summary>
    public sealed class YokiFrameEngineGateDecision
    {
        private YokiFrameEngineGateDecision(
            bool isAllowed,
            string errorCode,
            string errorMessage,
            YokiFrameEngineSettingsState settingsState)
        {
            IsAllowed = isAllowed;
            ErrorCode = errorCode ?? string.Empty;
            ErrorMessage = errorMessage ?? string.Empty;
            SettingsState = settingsState;
        }

        /// <summary>获取是否放行。</summary>
        public bool IsAllowed { get; }

        /// <summary>获取拒绝错误码；放行时为空字符串。</summary>
        public string ErrorCode { get; }

        /// <summary>获取拒绝说明；放行时为空字符串。</summary>
        public string ErrorMessage { get; }

        /// <summary>获取裁决时观察到的开关状态。</summary>
        public YokiFrameEngineSettingsState SettingsState { get; }

        /// <summary>创建放行裁决。</summary>
        /// <param name="settingsState">裁决时的开关状态。</param>
        /// <returns>放行裁决。</returns>
        public static YokiFrameEngineGateDecision Allow(YokiFrameEngineSettingsState settingsState)
        {
            return new YokiFrameEngineGateDecision(true, string.Empty, string.Empty, settingsState);
        }

        /// <summary>创建拒绝裁决。</summary>
        /// <param name="errorCode">稳定错误码。</param>
        /// <param name="errorMessage">面向调用方的说明。</param>
        /// <param name="settingsState">裁决时的开关状态。</param>
        /// <returns>拒绝裁决。</returns>
        public static YokiFrameEngineGateDecision Reject(
            string errorCode,
            string errorMessage,
            YokiFrameEngineSettingsState settingsState)
        {
            if (string.IsNullOrEmpty(errorCode))
            {
                throw new ArgumentNullException(nameof(errorCode));
            }

            return new YokiFrameEngineGateDecision(false, errorCode, errorMessage, settingsState);
        }
    }
}
#endif
