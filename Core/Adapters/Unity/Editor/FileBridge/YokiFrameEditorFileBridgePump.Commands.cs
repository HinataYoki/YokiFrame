#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Unity Editor FileBridge 的 Runtime dispatcher 接入和 System 命令 handler。
    /// </summary>
    internal static partial class YokiFrameEditorFileBridgePump
    {
        private const string SYSTEM_KIT = "System";
        private const string PING_ACTION = "ping";
        private const string BRIDGE_STATUS_ACTION = "bridge_status";
        private const string LIST_COMMANDS_ACTION = "list_commands";
        private const string REFRESH_SNAPSHOTS_ACTION = "refresh_snapshots";
        private const string GET_ENVIRONMENT_ACTION = "get_environment";
        private const string OPEN_PROJECT_FOLDER_ACTION = "open_project_folder";
        private const string OPEN_LOG_ACTION = "open_log";
        private const string OPEN_CODE_LOCATION_ACTION = "open_code_location";

        /// <summary>
        /// 创建 Editor pump 使用的 Runtime 命令分发器。
        /// </summary>
        /// <returns>绑定默认策略和 System handler 的命令分发器。</returns>
        private static YokiFrameCommandDispatcher CreateCommandDispatcher()
        {
            var policy = CreateHostCommandPolicy();
            // 静态初始化与 RefreshToolKitInteractions 重建都经过此处，缓存随 dispatcher 一起失效。
            sHostCommandPolicy = policy;
            return new YokiFrameCommandDispatcher(
                policy,
                new IYokiFrameCommandHandler[]
                {
                    new EditorSystemCommandHandler(
                        EditorUtility.RevealInFinder,
                        InternalEditorUtility.OpenFileAtLineExternal,
                        policy),
                    new YokiFrameUnityHarnessObservationCommandHandler(CreateHarnessContext),
                    sKitInteractions
                });
        }

        /// <summary>创建包含默认 System、Harness 与 Kit 命令的当前 Unity 宿主策略。</summary>
        /// <returns>Dispatcher、FastChannel 和命令目录共享的完整策略。</returns>
        private static YokiFrameCommandPolicy CreateHostCommandPolicy()
        {
            return YokiFrameCommandPolicy.CreateDefault(CreateHostCommandDescriptors());
        }

        /// <summary>获取与当前 dispatcher 同源的宿主策略缓存，避免每个命令帧重建完整策略。</summary>
        /// <returns>与 sCommandDispatcher 同一轮构建的策略；缓存缺失时按需构建。</returns>
        private static YokiFrameCommandPolicy GetHostCommandPolicy()
        {
            if (sHostCommandPolicy == null)
            {
                sHostCommandPolicy = CreateHostCommandPolicy();
            }

            return sHostCommandPolicy;
        }

        /// <summary>合并 Unity Harness 与当前 Registry 的真实命令描述。</summary>
        /// <returns>可交给 CommandPolicy 的独立数组。</returns>
        private static YokiFrameCommandDescriptor[] CreateHostCommandDescriptors()
        {
            var harnessCommands = CreateHarnessCommandDescriptors();
            var kitCommands = sKitInteractions.GetCommandDescriptors();
            YokiFrameCommandDescriptor[] commands = new YokiFrameCommandDescriptor[
                harnessCommands.Length + kitCommands.Length + 1];
            Array.Copy(harnessCommands, commands, harnessCommands.Length);
            Array.Copy(kitCommands, 0, commands, harnessCommands.Length, kitCommands.Length);
            commands[commands.Length - 1] = new YokiFrameCommandDescriptor(
                SYSTEM_KIT,
                OPEN_CODE_LOCATION_ACTION,
                YokiFrameCommandKind.UserAction);
            return commands;
        }

        /// <summary>
        /// 从 Editor FileBridge 信封创建 Runtime dispatcher 请求。
        /// </summary>
        /// <param name="envelope">已完成协议和 safe ID 校验的命令信封。</param>
        /// <param name="commandFileBytes">命令文件字节数，供 Runtime policy 复核文件大小边界。</param>
        /// <returns>Runtime 命令请求。</returns>
        private static YokiFrameCommandRequest CreateCommandRequest(
            YokiFrameEditorCommandEnvelope envelope,
            long commandFileBytes)
        {
            return new YokiFrameCommandRequest(
                envelope.source,
                envelope.kit,
                envelope.action,
                envelope.payloadJson,
                envelope.timeoutMs,
                commandFileBytes,
                envelope.requestId,
                ParseCreatedAtUtc(envelope.createdAtUtc));
        }

        /// <summary>
        /// 把已通过信封校验的创建时间转换为 UTC，供 dispatcher 计算执行 deadline。
        /// </summary>
        /// <param name="createdAtUtc">信封创建时间文本。</param>
        /// <returns>UTC 创建时间。</returns>
        private static DateTimeOffset ParseCreatedAtUtc(string createdAtUtc)
        {
            if (!DateTimeOffset.TryParse(
                    createdAtUtc,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind,
                    out var value))
            {
                throw new InvalidDataException("Command envelope createdAtUtc is invalid.");
            }

            return value.ToUniversalTime();
        }

        /// <summary>
        /// 执行 Unity Editor 当前支持的 System 命令。
        /// </summary>
        private sealed class EditorSystemCommandHandler : YokiFrameKitCommandHandler
        {
            private readonly YokiFrameCommandPolicy mPolicy;
            private readonly Action<string> mRevealPath;
            private readonly Func<string, int, bool> mOpenCodeLocation;

            /// <summary>创建可注入路径揭示、代码定位动作和命令策略的 System handler。</summary>
            /// <param name="revealPath">打开或揭示路径的宿主动作。</param>
            /// <param name="openCodeLocation">使用外部脚本编辑器打开文件行号的动作。</param>
            /// <param name="policy">当前宿主使用的命令策略。</param>
            public EditorSystemCommandHandler(
                Action<string> revealPath,
                Func<string, int, bool> openCodeLocation,
                YokiFrameCommandPolicy policy)
                : base(
                    SYSTEM_KIT,
                    new[]
                    {
                        PING_ACTION,
                        BRIDGE_STATUS_ACTION,
                        LIST_COMMANDS_ACTION,
                        REFRESH_SNAPSHOTS_ACTION,
                        GET_ENVIRONMENT_ACTION,
                        OPEN_PROJECT_FOLDER_ACTION,
                        OPEN_LOG_ACTION,
                        OPEN_CODE_LOCATION_ACTION
                    })
            {
                mRevealPath = revealPath ?? throw new ArgumentNullException(nameof(revealPath));
                mOpenCodeLocation = openCodeLocation ?? throw new ArgumentNullException(nameof(openCodeLocation));
                mPolicy = policy ?? throw new ArgumentNullException(nameof(policy));
            }

            /// <summary>
            /// 执行已通过策略和 Kit/action 匹配的 System 命令。
            /// </summary>
            /// <param name="request">命令请求。</param>
            /// <returns>命令终态结果。</returns>
            protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
            {
                if (request.Action == PING_ACTION)
                {
                    return YokiFrameCommandResult.Success(YokiFrameEditorFileBridgeJson.ToJson(CreatePingResult()));
                }

                if (request.Action == BRIDGE_STATUS_ACTION)
                {
                    return YokiFrameCommandResult.Success(YokiFrameEditorFileBridgeJson.ToJson(CreateBridgeStatusResult()));
                }

                if (request.Action == LIST_COMMANDS_ACTION)
                {
                    return YokiFrameCommandResult.Success(YokiFrameEditorFileBridgeJson.ToJson(CreateCommandCatalogResult(mPolicy.AllowedCommands)));
                }

                if (request.Action == REFRESH_SNAPSHOTS_ACTION)
                {
                    WriteCompleteBridgeState();
                    return YokiFrameCommandResult.Success(YokiFrameEditorFileBridgeJson.ToJson(CreateRefreshSnapshotsResult()));
                }

                if (request.Action == GET_ENVIRONMENT_ACTION)
                {
                    return YokiFrameCommandResult.Success(YokiFrameEditorFileBridgeJson.ToJson(CreateEnvironmentResult()));
                }

                if (request.Action == OPEN_PROJECT_FOLDER_ACTION)
                {
                    return OpenProjectFolder(mRevealPath);
                }

                if (request.Action == OPEN_LOG_ACTION)
                {
                    return OpenCurrentLog(mRevealPath);
                }

                if (request.Action == OPEN_CODE_LOCATION_ACTION)
                {
                    return OpenCodeLocation(request.PayloadJson, mOpenCodeLocation);
                }

                return YokiFrameCommandResult.Error("UnknownCommand", "Unsupported FileBridge command.");
            }
        }

        /// <summary>
        /// 根据当前策略创建命令目录结果，供 Workbench 动态渲染可用 Kit/action。
        /// </summary>
        /// <param name="commands">策略允许的命令描述。</param>
        /// <returns>命令目录结果。</returns>
        private static YokiFrameEditorCommandCatalogResult CreateCommandCatalogResult(IReadOnlyList<YokiFrameCommandDescriptor> commands)
        {
            Dictionary<string, List<YokiFrameEditorCommandCatalogAction>> groups = new Dictionary<string, List<YokiFrameEditorCommandCatalogAction>>(StringComparer.Ordinal);
            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                if (!groups.TryGetValue(command.Kit, out var actions))
                {
                    actions = new List<YokiFrameEditorCommandCatalogAction>();
                    groups.Add(command.Kit, actions);
                }

                actions.Add(new YokiFrameEditorCommandCatalogAction
                {
                    action = command.Action,
                    kind = command.Kind.ToString()
                });
            }

            List<YokiFrameEditorCommandCatalogKit> kits = new List<YokiFrameEditorCommandCatalogKit>();
            foreach (var group in groups)
            {
                kits.Add(new YokiFrameEditorCommandCatalogKit
                {
                    kit = group.Key,
                    actions = group.Value.ToArray()
                });
            }

            return new YokiFrameEditorCommandCatalogResult
            {
                mode = GetEditorMode(),
                sessionId = sSessionId,
                generation = sGeneration,
                sequence = sSequence,
                kits = kits.ToArray()
            };
        }

        /// <summary>
        /// 打开当前 Unity 项目根目录，并返回可审计的路径结果。
        /// </summary>
        /// <param name="revealPath">打开或揭示路径的宿主动作。</param>
        /// <returns>命令终态结果。</returns>
        private static YokiFrameCommandResult OpenProjectFolder(Action<string> revealPath)
        {
            var projectRoot = YokiFrameEditorFileBridgePaths.GetProjectRoot();
            RevealPath(revealPath, projectRoot);
            return YokiFrameCommandResult.Success(YokiFrameEditorFileBridgeJson.ToJson(CreateOpenPathResult(OPEN_PROJECT_FOLDER_ACTION, projectRoot)));
        }

        /// <summary>
        /// 打开 Unity 当前 console log 路径；路径不可用时返回终态错误，避免 CLI 等待超时。
        /// </summary>
        /// <param name="revealPath">打开或揭示路径的宿主动作。</param>
        /// <returns>命令终态结果。</returns>
        private static YokiFrameCommandResult OpenCurrentLog(Action<string> revealPath)
        {
            if (string.IsNullOrEmpty(Application.consoleLogPath))
            {
                return YokiFrameCommandResult.Error("LogPathUnavailable", "Unity console log path is unavailable.");
            }

            var logPath = Path.GetFullPath(Application.consoleLogPath);
            RevealPath(revealPath, logPath);
            return YokiFrameCommandResult.Success(YokiFrameEditorFileBridgeJson.ToJson(CreateOpenPathResult(OPEN_LOG_ACTION, logPath)));
        }

        /// <summary>校验项目内 C# 相对路径，并使用 Unity 配置的外部编辑器定位到行。</summary>
        /// <param name="payloadJson">包含 filePath 和 line 的命令 payload。</param>
        /// <param name="openCodeLocation">可替换的 Unity 外部编辑器动作。</param>
        /// <returns>成功打开的位置结果或终态错误。</returns>
        private static YokiFrameCommandResult OpenCodeLocation(
            string payloadJson,
            Func<string, int, bool> openCodeLocation)
        {
            YokiFrameCommandResult error;
            var request = ParseCodeLocationRequest(payloadJson, out error);
            if (request == null)
            {
                return error;
            }

            int line = request.line < 1 ? 1 : request.line;
            string fullPath = ResolveCodeLocationPath(request.filePath, out error);
            if (string.IsNullOrEmpty(fullPath))
            {
                return error;
            }

            if (!openCodeLocation(fullPath, line))
            {
                return YokiFrameCommandResult.Error(
                    "CodeEditorUnavailable",
                    "Unity failed to open the configured external code editor.");
            }

            return YokiFrameCommandResult.Success(YokiFrameEditorFileBridgeJson.ToJson(
                CreateOpenCodeLocationResult(request.filePath, line)));
        }

        /// <summary>解析并校验源码定位 payload 的基本字段。</summary>
        /// <param name="payloadJson">命令 payload JSON。</param>
        /// <param name="error">解析失败时返回的终态错误。</param>
        /// <returns>合法请求；失败时为空。</returns>
        private static YokiFrameEditorCodeLocationRequest ParseCodeLocationRequest(
            string payloadJson,
            out YokiFrameCommandResult error)
        {
            YokiFrameEditorCodeLocationRequest request;
            try
            {
                request = JsonUtility.FromJson<YokiFrameEditorCodeLocationRequest>(payloadJson);
            }
            catch (ArgumentException exception)
            {
                error = YokiFrameCommandResult.Error("InvalidPayload", exception.Message);
                return null;
            }

            if (request == null
                || string.IsNullOrEmpty(request.filePath)
                || Path.IsPathRooted(request.filePath)
                || !string.Equals(Path.GetExtension(request.filePath), ".cs", StringComparison.OrdinalIgnoreCase))
            {
                error = YokiFrameCommandResult.Error(
                    "InvalidCodeLocation",
                    "filePath must be a project-relative C# path.");
                return null;
            }

            error = null;
            return request;
        }

        /// <summary>把项目相对路径解析为 Assets 内已存在的 C# 文件。</summary>
        /// <param name="relativePath">已通过基本字段校验的项目相对路径。</param>
        /// <param name="error">路径校验失败时返回的终态错误。</param>
        /// <returns>合法绝对路径；失败时为空字符串。</returns>
        private static string ResolveCodeLocationPath(
            string relativePath,
            out YokiFrameCommandResult error)
        {
            try
            {
                string projectRoot = Path.GetFullPath(YokiFrameEditorFileBridgePaths.GetProjectRoot());
                string assetsRoot = EnsureTrailingSeparator(Path.GetFullPath(Path.Combine(projectRoot, "Assets")));
                string fullPath = Path.GetFullPath(Path.Combine(projectRoot, relativePath));
                if (fullPath.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(fullPath))
                {
                    error = null;
                    return fullPath;
                }
            }
            catch (Exception exception) when (exception is ArgumentException
                || exception is NotSupportedException
                || exception is PathTooLongException)
            {
                error = YokiFrameCommandResult.Error("InvalidCodeLocation", exception.Message);
                return string.Empty;
            }

            error = YokiFrameCommandResult.Error(
                "CodeLocationOutsideProject",
                "Code location must resolve to an existing file inside Assets.");
            return string.Empty;
        }

        /// <summary>确保目录路径以平台分隔符结尾，供 containment 比较使用。</summary>
        /// <param name="path">规范化目录路径。</param>
        /// <returns>带尾部分隔符的目录路径。</returns>
        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        /// <summary>创建源码定位成功结果。</summary>
        /// <param name="relativePath">项目相对源码路径。</param>
        /// <param name="line">一基行号。</param>
        /// <returns>包含当前宿主身份的结果 DTO。</returns>
        private static YokiFrameEditorOpenCodeLocationResult CreateOpenCodeLocationResult(
            string relativePath,
            int line)
        {
            return new YokiFrameEditorOpenCodeLocationResult
            {
                filePath = relativePath.Replace('\\', '/'),
                line = line,
                mode = GetEditorMode(),
                sessionId = sSessionId,
                generation = sGeneration,
                sequence = sSequence,
                opened = true
            };
        }

        /// <summary>
        /// 执行路径揭示动作；单独封装是为了让自动验证和未来宿主适配可以替换窗口调用。
        /// </summary>
        /// <param name="revealPath">打开或揭示路径的宿主动作。</param>
        /// <param name="path">要打开或揭示的路径。</param>
        private static void RevealPath(Action<string> revealPath, string path)
        {
            revealPath(path);
        }

        /// <summary>
        /// 创建打开路径命令的结果 DTO，统一返回 action、路径和当前 Editor 会话信息。
        /// </summary>
        /// <param name="action">System action 标识。</param>
        /// <param name="path">已打开或尝试打开的路径。</param>
        /// <returns>打开路径命令结果。</returns>
        private static YokiFrameEditorOpenPathResult CreateOpenPathResult(string action, string path)
        {
            return new YokiFrameEditorOpenPathResult
            {
                action = action,
                path = Path.GetFullPath(path).Replace('\\', '/'),
                mode = GetEditorMode(),
                sessionId = sSessionId,
                generation = sGeneration,
                sequence = sSequence,
                opened = true
            };
        }
    }
}

#endif
