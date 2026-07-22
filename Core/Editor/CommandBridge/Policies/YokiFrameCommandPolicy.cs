#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 提供 CommandBridge v2 的最小跨宿主命令策略。
    /// </summary>
    public sealed class YokiFrameCommandPolicy
    {
        /// <summary>
        /// 命令允许的最小超时时间，单位毫秒。
        /// </summary>
        public const int COMMAND_TIMEOUT_MIN_MS = YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MIN_MS;

        /// <summary>
        /// 命令允许的最大超时时间，单位毫秒。
        /// </summary>
        public const int COMMAND_TIMEOUT_MAX_MS = YokiFrameFileBridgeContract.COMMAND_TIMEOUT_MAX_MS;

        /// <summary>
        /// payload JSON 最大 UTF-8 字节数。
        /// </summary>
        public const int PAYLOAD_MAX_BYTES = YokiFrameFileBridgeContract.PAYLOAD_MAX_BYTES;

        /// <summary>
        /// 命令文件最大 UTF-8 字节数。
        /// </summary>
        public const int COMMAND_FILE_MAX_BYTES = YokiFrameFileBridgeContract.COMMAND_FILE_MAX_BYTES;

        private static readonly UTF8Encoding sUtf8 = new UTF8Encoding(false);
        private readonly string[] mAllowedSources;
        private readonly YokiFrameCommandDescriptor[] mAllowedCommands;
        private readonly IReadOnlyList<YokiFrameCommandDescriptor> mAllowedCommandView;

        /// <summary>
        /// 创建命令策略；调用方负责传入当前宿主允许的来源和命令。
        /// </summary>
        /// <param name="allowedSources">允许的命令来源。</param>
        /// <param name="allowedCommands">允许的 Kit/action 命令描述。</param>
        public YokiFrameCommandPolicy(string[] allowedSources, YokiFrameCommandDescriptor[] allowedCommands)
        {
            mAllowedSources = (string[])(allowedSources ?? throw new ArgumentNullException(nameof(allowedSources))).Clone();
            mAllowedCommands = (YokiFrameCommandDescriptor[])(allowedCommands ?? throw new ArgumentNullException(nameof(allowedCommands))).Clone();
            mAllowedCommandView = Array.AsReadOnly(mAllowedCommands);
        }

        /// <summary>
        /// 获取当前策略允许的命令只读视图，供宿主暴露 `System/list_commands` 诊断目录。
        /// </summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> AllowedCommands => mAllowedCommandView;

        /// <summary>
        /// 创建当前 Phase 5 首切片使用的默认策略。
        /// </summary>
        /// <returns>默认 CommandPolicy。</returns>
        public static YokiFrameCommandPolicy CreateDefault()
        {
            return CreateDefault(Array.Empty<YokiFrameCommandDescriptor>());
        }

        /// <summary>
        /// 创建默认跨宿主策略，并仅为当前宿主追加已经实现的命令描述。
        /// </summary>
        /// <param name="additionalCommands">当前宿主独有且已注册 handler 的命令。</param>
        /// <returns>包含默认命令与宿主命令的策略。</returns>
        public static YokiFrameCommandPolicy CreateDefault(YokiFrameCommandDescriptor[] additionalCommands)
        {
            if (additionalCommands == null)
            {
                throw new ArgumentNullException(nameof(additionalCommands));
            }

            YokiFrameCommandDescriptor[] defaultCommands =
            {
                new YokiFrameCommandDescriptor("System", "ping", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor("System", "bridge_status", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor("System", "list_commands", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor("System", "refresh_snapshots", YokiFrameCommandKind.Maintenance),
                new YokiFrameCommandDescriptor("System", "get_environment", YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor("System", "open_project_folder", YokiFrameCommandKind.UserAction),
                new YokiFrameCommandDescriptor("System", "open_log", YokiFrameCommandKind.UserAction)
            };
            var commands = new YokiFrameCommandDescriptor[defaultCommands.Length + additionalCommands.Length];
            Array.Copy(defaultCommands, commands, defaultCommands.Length);
            Array.Copy(additionalCommands, 0, commands, defaultCommands.Length, additionalCommands.Length);
            return CreateWithDefaultSources(commands);
        }

        /// <summary>
        /// 使用产品中立的默认来源集合创建策略，同时保留调用方提供的精确命令面。
        /// </summary>
        /// <param name="allowedCommands">当前宿主已经注册 handler 的命令描述。</param>
        /// <returns>允许 CLI、Workbench、Codex 与通用外部自动化来源的策略。</returns>
        public static YokiFrameCommandPolicy CreateWithDefaultSources(
            YokiFrameCommandDescriptor[] allowedCommands)
        {
            if (allowedCommands == null)
            {
                throw new ArgumentNullException(nameof(allowedCommands));
            }

            return new YokiFrameCommandPolicy(
                new[]
                {
                    YokiFrameCommandSourceContract.CLI,
                    YokiFrameCommandSourceContract.WORKBENCH,
                    YokiFrameCommandSourceContract.CODEX,
                    YokiFrameCommandSourceContract.EXTERNAL_AUTOMATION
                },
                allowedCommands);
        }

        /// <summary>
        /// 评估命令是否满足来源、大小、超时和 allowlist 规则。
        /// </summary>
        /// <param name="request">待评估命令摘要。</param>
        /// <returns>策略评估结果。</returns>
        public YokiFrameCommandPolicyDecision Evaluate(YokiFrameCommandPolicyRequest request)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            if (!IsAllowedSource(request.Source))
            {
                return YokiFrameCommandPolicyDecision.Reject(
                    "PolicyRejected",
                    "Command source is not allowed by YokiFrame CommandPolicy.");
            }

            if (request.TimeoutMs < COMMAND_TIMEOUT_MIN_MS || request.TimeoutMs > COMMAND_TIMEOUT_MAX_MS)
            {
                return YokiFrameCommandPolicyDecision.Reject(
                    "PolicyRejected",
                    "Command timeout is outside the allowed range.");
            }

            if (request.CommandFileBytes > COMMAND_FILE_MAX_BYTES)
            {
                return YokiFrameCommandPolicyDecision.Reject(
                    "PolicyRejected",
                    "Command file exceeds the allowed byte size.");
            }

            if (GetPayloadByteCount(request.PayloadJson) > PAYLOAD_MAX_BYTES)
            {
                return YokiFrameCommandPolicyDecision.Reject(
                    "PolicyRejected",
                    "Command payload exceeds the allowed byte size.");
            }

            if (!TryFindCommand(request.Kit, request.Action, out var command))
            {
                return YokiFrameCommandPolicyDecision.Reject(
                    "UnknownCommand",
                    "Unsupported FileBridge command.");
            }

            if (command.Kind == YokiFrameCommandKind.Dangerous
                && !YokiFrameCommandPayloadConfirmation.HasConfirmedTrue(request.PayloadJson))
            {
                return YokiFrameCommandPolicyDecision.Reject(
                    "ConfirmationRequired",
                    "Dangerous command requires payload.confirmed=true.");
            }

            return YokiFrameCommandPolicyDecision.Allow(command.Kind);
        }

        /// <summary>
        /// 判断来源是否在 allowlist 中。
        /// </summary>
        /// <param name="source">命令来源。</param>
        /// <returns>允许时返回 true。</returns>
        private bool IsAllowedSource(string source)
        {
            for (var index = 0; index < mAllowedSources.Length; index++)
            {
                if (string.Equals(source, mAllowedSources[index], StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 查找指定 Kit/action 的命令描述。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <param name="command">命令描述。</param>
        /// <returns>找到命令时返回 true。</returns>
        private bool TryFindCommand(string kit, string action, out YokiFrameCommandDescriptor command)
        {
            for (var index = 0; index < mAllowedCommands.Length; index++)
            {
                var candidate = mAllowedCommands[index];
                if (string.Equals(candidate.Kit, kit, StringComparison.Ordinal)
                    && string.Equals(candidate.Action, action, StringComparison.Ordinal))
                {
                    command = candidate;
                    return true;
                }
            }

            command = null;
            return false;
        }

        /// <summary>
        /// 计算 payload 的 UTF-8 字节数；空 payload 按空对象处理。
        /// </summary>
        /// <param name="payloadJson">payload JSON 文本。</param>
        /// <returns>UTF-8 字节数。</returns>
        private static int GetPayloadByteCount(string payloadJson)
        {
            var payload = string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson;
            return sUtf8.GetByteCount(payload);
        }
    }
}
#endif
