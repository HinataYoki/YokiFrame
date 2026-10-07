#if UNITY_EDITOR
using System;
using UnityEditor;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// play_control：请求进入/退出播放、暂停、继续与单步。
    /// </summary>
    /// <remarks>
    /// 按"提交即返回"语义实现：先返回 accepted，再让 Unity 在后续 tick 完成状态切换，
    /// 因此进入播放触发的域重载不会吞掉本次响应；真实状态必须由 domain_state 复核。
    /// </remarks>
    internal sealed class UnityPlayControlOperation : IRoslynOperation
    {
        internal const string ACTION = "play_control";

        private const string COMMAND_FIELD = "command";
        private const string COMMAND_ENTER = "enter";
        private const string COMMAND_EXIT = "exit";
        private const string COMMAND_PAUSE = "pause";
        private const string COMMAND_RESUME = "resume";
        private const string COMMAND_STEP = "step";

        private const string OBSERVE_NOTE =
            "Play mode switches and script reloads take effect on later ticks; verify with domain_state.";

        /// <summary>创建 play_control 操作。</summary>
        internal UnityPlayControlOperation()
        {
            Descriptor = new RoslynOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.Dangerous,
                isDiagnostic: false,
                isCancellation: false,
                targets: RoslynExecutionTarget.Editor);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>执行播放控制请求。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (!TryReadCommand(request.PayloadJson, out string command, out string error))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
            }

            bool previousPlaying = EditorApplication.isPlaying;
            bool previousPaused = EditorApplication.isPaused;
            string previousState = previousPlaying ? "PlayMode" : "EditMode";
            if (!TryApplyCommand(command, previousPlaying, previousPaused, out bool accepted, out YokiFrameCommandResult rejected))
            {
                return rejected;
            }

            return YokiFrameCommandResult.Success(
                WritePlayResult(command, accepted, previousState, previousPaused));
        }

        /// <summary>
        /// 识别命令并施加对应的编辑器状态变化。未知命令返回 false；
        /// 已处于目标状态时仍返回 true，accepted 为 false。
        /// </summary>
        /// <param name="command">已规范化的命令。</param>
        /// <param name="previousPlaying">调用前是否在播放。</param>
        /// <param name="previousPaused">调用前是否暂停。</param>
        /// <param name="accepted">本次是否真正改变了状态。</param>
        /// <param name="rejected">未知命令时的错误结果。</param>
        /// <returns>命令被识别时返回 true。</returns>
        private static bool TryApplyCommand(
            string command,
            bool previousPlaying,
            bool previousPaused,
            out bool accepted,
            out YokiFrameCommandResult rejected)
        {
            rejected = null;
            switch (command)
            {
                case COMMAND_ENTER:
                    accepted = ApplyEnter(previousPlaying);
                    return true;
                case COMMAND_EXIT:
                    accepted = ApplyExit(previousPlaying);
                    return true;
                case COMMAND_PAUSE:
                    accepted = ApplyPause(previousPlaying, previousPaused);
                    return true;
                case COMMAND_RESUME:
                    accepted = ApplyResume(previousPlaying, previousPaused);
                    return true;
                case COMMAND_STEP:
                    accepted = ApplyStep(previousPlaying, previousPaused);
                    return true;
                default:
                    accepted = false;
                    rejected = YokiFrameCommandResult.Error(
                        RoslynErrorCodes.INVALID_PAYLOAD,
                        "Unsupported play_control command: " + command + ".");
                    return false;
            }
        }

        /// <summary>未在播放时请求进入播放。</summary>
        /// <param name="previousPlaying">调用前是否在播放。</param>
        /// <returns>实际写入 isPlaying 时返回 true。</returns>
        private static bool ApplyEnter(bool previousPlaying)
        {
            bool accepted = !previousPlaying;
            if (accepted)
            {
                EditorApplication.isPlaying = true;
            }

            return accepted;
        }

        /// <summary>正在播放时请求退出。</summary>
        /// <param name="previousPlaying">调用前是否在播放。</param>
        /// <returns>实际写入 isPlaying 时返回 true。</returns>
        private static bool ApplyExit(bool previousPlaying)
        {
            bool accepted = previousPlaying;
            if (accepted)
            {
                EditorApplication.isPlaying = false;
            }

            return accepted;
        }

        /// <summary>正在播放且未暂停时请求暂停。</summary>
        /// <param name="previousPlaying">调用前是否在播放。</param>
        /// <param name="previousPaused">调用前是否暂停。</param>
        /// <returns>实际写入 isPaused 时返回 true。</returns>
        private static bool ApplyPause(bool previousPlaying, bool previousPaused)
        {
            bool accepted = previousPlaying && !previousPaused;
            if (accepted)
            {
                EditorApplication.isPaused = true;
            }

            return accepted;
        }

        /// <summary>正在播放且已暂停时请求继续。</summary>
        /// <param name="previousPlaying">调用前是否在播放。</param>
        /// <param name="previousPaused">调用前是否暂停。</param>
        /// <returns>实际清除暂停时返回 true。</returns>
        private static bool ApplyResume(bool previousPlaying, bool previousPaused)
        {
            bool accepted = previousPlaying && previousPaused;
            if (accepted)
            {
                EditorApplication.isPaused = false;
            }

            return accepted;
        }

        /// <summary>正在播放且已暂停时单步。只调用 Step，不改暂停标志。</summary>
        /// <param name="previousPlaying">调用前是否在播放。</param>
        /// <param name="previousPaused">调用前是否暂停。</param>
        /// <returns>实际单步时返回 true。</returns>
        private static bool ApplyStep(bool previousPlaying, bool previousPaused)
        {
            bool accepted = previousPlaying && previousPaused;
            if (accepted)
            {
                EditorApplication.Step();
            }

            return accepted;
        }

        /// <summary>在状态写入之后读取编辑器标志并组装响应。</summary>
        /// <param name="command">已执行的命令。</param>
        /// <param name="accepted">本次是否改变了状态。</param>
        /// <param name="previousState">调用前的 PlayMode 或 EditMode。</param>
        /// <param name="previousPaused">调用前是否暂停。</param>
        /// <returns>成功载荷 JSON。</returns>
        private static string WritePlayResult(
            string command,
            bool accepted,
            string previousState,
            bool previousPaused)
        {
            string note = accepted ? OBSERVE_NOTE : "Requested state already applied; nothing changed.";
            return new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("command", command)
                .Property("accepted", accepted)
                .Property("previousState", previousState)
                .Property("previousPaused", previousPaused)
                .Property("isPlaying", EditorApplication.isPlaying)
                .Property("isPaused", EditorApplication.isPaused)
                .Property("observeAction", "domain_state")
                .Property("note", note)
                .EndObject()
                .ToString();
        }

        /// <summary>
        /// 读取 payload 的 command 字段；缺失或非法时给出稳定错误。
        /// </summary>
        /// <param name="payloadJson">payload JSON。</param>
        /// <param name="command">命令文本。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>解析成功时返回 true。</returns>
        private static bool TryReadCommand(string payloadJson, out string command, out string error)
        {
            command = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                error = "play_control requires a command field.";
                return false;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(payloadJson);
            }
            catch (JsonException exception)
            {
                error = "play_control payload is not valid JSON: " + exception.Message;
                return false;
            }

            using (document)
            {
                if (!document.RootElement.TryGetProperty(COMMAND_FIELD, out JsonElement value)
                    || value.ValueKind != JsonValueKind.String)
                {
                    error = "play_control requires a string command field.";
                    return false;
                }

                command = (value.GetString() ?? string.Empty).Trim().ToLowerInvariant();
                if (command.Length == 0)
                {
                    error = "play_control command is empty.";
                    return false;
                }

                return true;
            }
        }
    }
}
#endif
