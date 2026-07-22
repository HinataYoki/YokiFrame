#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>
    /// 提供 LogKit Workbench 状态、显式文件尾读和用户触发的会话设置/历史修改。
    /// </summary>
    public sealed class LogKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT_NAME = "LogKit";
        private const string GET_WORKBENCH_SNAPSHOT = "get_workbench_snapshot";
        private const string READ_LOG_FILE = "read_log_file";
        private const string SET_SETTINGS = "set_settings";
        private const string RESET_SETTINGS = "reset_settings";
        private const string CLEAR_HISTORY = "clear_history";
        private static readonly string[] sSupportedActions =
        {
            GET_WORKBENCH_SNAPSHOT,
            READ_LOG_FILE,
            SET_SETTINGS,
            RESET_SETTINGS,
            CLEAR_HISTORY
        };

        /// <summary>创建支持 LogKit 五个正式 Workbench action 的 handler。</summary>
        public LogKitCommandHandler() : base(KIT_NAME, sSupportedActions) { }

        /// <summary>创建当前 LogKit 的固定完整 state。</summary>
        /// <returns>满足 Shared Memory 体积边界的 Workbench JSON。</returns>
        public string CreateWorkbenchSnapshot()
        {
            return LogKitJsonWriter.WriteWorkbench(LogKitSnapshotBuilder.Create());
        }

        /// <summary>执行匹配 action，并把参数、宿主和 IO 异常转换为 terminal error。</summary>
        /// <param name="request">已通过 Kit/action 匹配的命令。</param>
        /// <returns>固定 state、文件预览或终态错误。</returns>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                if (request.Action == READ_LOG_FILE) return ReadLogFile(request.PayloadJson);
                if (request.Action == SET_SETTINGS) return SetSettings(request.PayloadJson);
                if (request.Action == RESET_SETTINGS) return ResetSettings();
                if (request.Action == CLEAR_HISTORY) return ClearHistory();
                return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error("InvalidPayload", exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("LogKitCommandFailed", exception.Message);
            }
        }

        /// <summary>校验 kind 后读取对应日志文件的有界尾部。</summary>
        private static YokiFrameCommandResult ReadLogFile(string payloadJson)
        {
            string kind = JsonHelper.ExtractString(payloadJson, "kind");
            if (kind != "editor" && kind != "player")
            {
                return YokiFrameCommandResult.Error(
                    "InvalidPayload",
                    "LogKit read_log_file requires kind=editor or kind=player.");
            }

            LogKitFilePreview preview = LogKitFilePreviewReader.Read(kind);
            return YokiFrameCommandResult.Success(LogKitJsonWriter.WriteFilePreview(preview));
        }

        /// <summary>原子校验完整 settings 对象后应用当前会话，并返回新 state。</summary>
        private YokiFrameCommandResult SetSettings(string payloadJson)
        {
            if (!LogKitHostEnvironment.Capture().SettingsApply)
            {
                return YokiFrameCommandResult.Error(
                    "RuntimeUnavailable",
                    "LogKit runtime settings cannot be applied before host initialization.");
            }

            if (!LogKitSettings.TryApplyCompletePayload(payloadJson, out var errorMessage))
            {
                return YokiFrameCommandResult.Error("InvalidPayload", errorMessage);
            }

            LogKitHostEnvironment.RefreshPathsFromSettings();
            return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
        }

        /// <summary>恢复当前会话默认设置、重新解析文件位置并返回新 state。</summary>
        private YokiFrameCommandResult ResetSettings()
        {
            if (!LogKitHostEnvironment.Capture().SettingsApply)
            {
                return YokiFrameCommandResult.Error(
                    "RuntimeUnavailable",
                    "LogKit runtime settings cannot be reset before host initialization.");
            }

            LogKitSettings.ResetToDefaults();
            LogKitHostEnvironment.RefreshPathsFromSettings();
            return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
        }

        /// <summary>清空当前 Core 内存历史和丢弃计数，并返回新 state。</summary>
        private YokiFrameCommandResult ClearHistory()
        {
            LogKit.ClearHistory();
            return YokiFrameCommandResult.Success(CreateWorkbenchSnapshot());
        }
    }
}
#endif
