#if UNITY_EDITOR

using System;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Unity Editor FastChannel 生命周期、主线程请求处理和 registry endpoint 发布逻辑。
    /// </summary>
    internal static partial class YokiFrameEditorFileBridgePump
    {
        private const string FAST_CHANNEL_NAMED_PIPE = "namedPipe";
        private const string FAST_CHANNEL_NONE = "none";
        private const string FAST_CHANNEL_FALLBACK = "filebridge";
        private const string FAST_CHANNEL_TRANSITION_PENDING_KEY =
            "YokiFrame.FastChannel.TransitionPending";

        /// <summary>
        /// 订阅 Play Mode、Domain Reload 与 Editor 退出事件，确保 FastChannel session/generation 和 listener 生命周期一致。
        /// </summary>
        private static void RegisterFastChannelLifecycleHooks()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
        }

        /// <summary>
        /// 在进入 Play Mode 或回到 Edit Mode 后创建新 session/generation，避免关闭 Domain Reload 时复用旧 FastChannel endpoint。
        /// </summary>
        /// <param name="stateChange">Unity 当前 Play Mode 状态变化。</param>
        private static void OnPlayModeStateChanged(PlayModeStateChange stateChange)
        {
            if (stateChange == PlayModeStateChange.ExitingEditMode
                || stateChange == PlayModeStateChange.ExitingPlayMode)
            {
                SessionState.SetBool(FAST_CHANNEL_TRANSITION_PENDING_KEY, true);
                PublishDisconnectedState();
                return;
            }

            if (stateChange != PlayModeStateChange.EnteredPlayMode
                && stateChange != PlayModeStateChange.EnteredEditMode)
            {
                return;
            }

            SessionState.SetBool(FAST_CHANNEL_TRANSITION_PENDING_KEY, false);
            RotateFastChannelSession();
        }

        /// <summary>
        /// 判断当前程序集是否在 Play Mode 状态切换的 Domain Reload 中，避免提前发布短命 enabled endpoint。
        /// </summary>
        /// <returns>等待 EnteredPlayMode 或 EnteredEditMode 建立最终会话时返回 true。</returns>
        private static bool IsFastChannelTransitionPending()
        {
            return SessionState.GetBool(FAST_CHANNEL_TRANSITION_PENDING_KEY, false);
        }

        /// <summary>
        /// 在 Domain Reload 前停止 listener，避免后台 Pipe 持有即将卸载的静态类型或等待中的主线程任务。
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            PublishDisconnectedState();
            ReleaseAdmissionLease();
        }

        /// <summary>
        /// 在 Unity Editor 退出前停止 listener，避免遗留可连接的 Named Pipe endpoint。
        /// </summary>
        private static void OnEditorQuitting()
        {
            PublishDisconnectedState();
            ReleaseAdmissionLease();
        }

        /// <summary>
        /// 在当前 Host 已发布 disabled 状态并停止 listener 后释放项目级 admission lease。
        /// </summary>
        private static void ReleaseAdmissionLease()
        {
            var lease = sAdmissionLease;
            sAdmissionLease = null;
            lease?.Dispose();
        }

        /// <summary>
        /// 轮换会话、generation 和启动时间，重启 FastChannel 后立即刷新 FileBridge state。
        /// </summary>
        private static void RotateFastChannelSession()
        {
            StopFastChannelHost();
            YokiFrameEditorTelemetryWriter.Dispose();
            // Dispose 会释放项目级通知句柄；此处幂等重建，保证本轮 registry capabilities 立即包含 telemetry.notify。
            YokiFrameEditorTelemetryWriter.RegisterLifecycleHooks();
            sStateVersions.Clear();
            ClearNamedTelemetryVersions();
            sSessionId = Guid.NewGuid().ToString("N");
            sGeneration = CreateNextGeneration(DateTimeOffset.UtcNow.Ticks);
            sStartedAtUtc = DateTimeOffset.UtcNow.ToString("O");
            StartFastChannelHost();
            WriteCompleteBridgeStateSafely();
        }

        /// <summary>
        /// 在宿主即将重建或退出时立即发布 disabled endpoint，令 Client 丢弃旧连接并回落 FileBridge。
        /// </summary>
        private static void PublishDisconnectedState()
        {
            StopFastChannelHost();
            YokiFrameEditorTelemetryWriter.Dispose();
            sStateVersions.Clear();
            ClearNamedTelemetryVersions();
            try
            {
                sSequence++;
                EnsureBridgeDirectories();
                WriteEngineRegistry();
                WriteHeartbeat();
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame disconnected state write failed: " + exception.Message);
            }
        }

        /// <summary>
        /// 创建严格大于当前 generation 的代际，避免同一 tick 的快速状态转换复用旧连接身份。
        /// </summary>
        /// <param name="utcTicks">当前 UTC ticks。</param>
        /// <returns>新的单调递增 generation。</returns>
        private static long CreateNextGeneration(long utcTicks)
        {
            return utcTicks > sGeneration ? utcTicks : sGeneration + 1L;
        }

        /// <summary>
        /// 在 Windows Editor 上启动 Named Pipe listener；其它 Editor 平台只发布 disabled endpoint 并继续使用 FileBridge。
        /// </summary>
        private static void StartFastChannelHost()
        {
#if UNITY_EDITOR_WIN
            try
            {
                sFastChannelStartError = string.Empty;
                var pipeName = CreateFastChannelPipeName();
                sFastChannelHost = new YokiFrameEditorNamedPipeFastChannelHost(pipeName);
                sFastChannelHost.Start();
            }
            catch (Exception exception)
            {
                sFastChannelHost = null;
                sFastChannelStartError = exception.Message;
                Debug.LogWarning("YokiFrame FastChannel listener start failed: " + exception.Message);
            }
#endif
        }

        /// <summary>
        /// 停止当前 Windows Named Pipe listener；无 listener 或非 Windows Editor 时保持幂等。
        /// </summary>
        private static void StopFastChannelHost()
        {
#if UNITY_EDITOR_WIN
            var host = sFastChannelHost;
            sFastChannelHost = null;
            if (host != null)
            {
                host.Dispose();
            }
#endif
        }

        /// <summary>
        /// 在 Unity Editor 主线程 drain 后台 FastChannel 请求，避免 listener 线程直接调用 dispatcher 或 Unity API。
        /// </summary>
        private static void ProcessFastChannelRequestsSafely()
        {
#if UNITY_EDITOR_WIN
            var host = sFastChannelHost;
            if (host == null)
            {
                return;
            }

            try
            {
                host.ProcessPending(ProcessFastChannelFrame);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("YokiFrame FastChannel request processing failed: " + exception.Message);
            }
#endif
        }

        /// <summary>
        /// 根据收到的 Hello 或 Command frame 在主线程生成终态 response；只执行策略标记为 ReadOnly 的命令。
        /// </summary>
        /// <param name="request">后台 listener 已完成 framing 校验的请求 frame。</param>
        /// <returns>应写回当前连接的终态 response 或 error frame。</returns>
        private static YokiFrameFastChannelFrame ProcessFastChannelFrame(YokiFrameFastChannelFrame request)
        {
            if (request.MessageKind == YokiFrameFastChannelMessageKind.Hello)
            {
                return ProcessFastChannelHello(request);
            }

            if (request.MessageKind == YokiFrameFastChannelMessageKind.Command)
            {
                return ProcessFastChannelCommand(request);
            }

            return CreateFastChannelError("FastChannelMessageRejected", "FastChannel host accepts only Hello and Command requests.");
        }

        /// <summary>
        /// 校验 Client Hello 的身份 SafeId 与 engine/session/generation，并在完全匹配时返回当前 Host HelloAck。
        /// </summary>
        /// <param name="request">Hello frame。</param>
        /// <returns>匹配时的 HelloAck，失败时的 Error frame。</returns>
        private static YokiFrameFastChannelFrame ProcessFastChannelHello(YokiFrameFastChannelFrame request)
        {
            // 共享宿主握手校验器统一执行 SafeId 与会话一致性检查，避免各宿主校验强度漂移。
            if (!YokiFrameFastChannelHostHandshake.TryValidateHello(
                    request,
                    YokiFrameEditorFileBridgePaths.ENGINE_ID,
                    sSessionId,
                    sGeneration,
                    out var errorCode,
                    out var errorMessage))
            {
                return CreateFastChannelError(errorCode, errorMessage);
            }

            var acknowledgement = new YokiFrameEditorFastChannelIdentity
            {
                engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID,
                sessionId = sSessionId,
                generation = sGeneration
            };
            return new YokiFrameFastChannelFrame(
                YokiFrameFastChannelMessageKind.HelloAck,
                0,
                YokiFrameEditorFileBridgeJson.ToJson(acknowledgement));
        }

        /// <summary>
        /// 解析并执行 FastChannel command；为避免 response 丢失后的 FileBridge 重发语义风险，仅执行策略标记为 ReadOnly 的命令。
        /// </summary>
        /// <param name="request">已经完成握手后的 Command frame。</param>
        /// <returns>与 FileBridge 相同 schema 的 Response frame，或 Error frame。</returns>
        private static YokiFrameFastChannelFrame ProcessFastChannelCommand(YokiFrameFastChannelFrame request)
        {
            try
            {
                var envelope = YokiFrameEditorFileBridgeJson.FromJson<YokiFrameEditorCommandEnvelope>(request.PayloadJson);
                ValidateEnvelope(envelope);
                if (!IsFastChannelReadOnlyCommand(envelope.kit, envelope.action))
                {
                    return CreateFastChannelError("FastChannelCommandRejected", "The current endpoint does not advertise this command as read-only.");
                }

                var response = ExecuteCommand(envelope, Encoding.UTF8.GetByteCount(request.PayloadJson));
                return new YokiFrameFastChannelFrame(
                    YokiFrameFastChannelMessageKind.Response,
                    0,
                    YokiFrameEditorFileBridgeJson.ToJson(response));
            }
            catch (Exception)
            {
                return CreateFastChannelError("FastChannelCommandInvalid", "FastChannel command payload is invalid.");
            }
        }

        /// <summary>
        /// 判断命令是否在当前宿主策略中标记为 ReadOnly。
        /// </summary>
        /// <param name="kit">已通过基础协议验证的 Kit 标识。</param>
        /// <param name="action">已通过基础协议验证的 action 标识。</param>
        /// <returns>命令为 ReadOnly 时返回 true。</returns>
        private static bool IsFastChannelReadOnlyCommand(string kit, string action)
        {
            var commands = GetHostCommandPolicy().AllowedCommands;
            for (var index = 0; index < commands.Count; index++)
            {
                var command = commands[index];
                if (command.Kind == YokiFrameCommandKind.ReadOnly
                    && command.Kit == kit
                    && command.Action == action)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 创建标准 FastChannel Error frame；Unity JSON 在主线程序列化，确保动态错误文本被正确转义。
        /// </summary>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">面向 Client 的错误说明。</param>
        /// <returns>可直接写回连接的 Error frame。</returns>
        private static YokiFrameFastChannelFrame CreateFastChannelError(string code, string message)
        {
            var error = new YokiFrameEditorFastChannelError
            {
                code = code,
                message = message
            };
            return new YokiFrameFastChannelFrame(
                YokiFrameFastChannelMessageKind.Error,
                0,
                YokiFrameEditorFileBridgeJson.ToJson(error));
        }

        /// <summary>
        /// 创建 registry 的 FastChannel endpoint 数组；只有 listener 成功创建 Pipe 后才发布 enabled endpoint。
        /// </summary>
        /// <returns>当前 session 对应的一个 enabled 或 disabled endpoint。</returns>
        private static YokiFrameEditorFastChannelEndpoint[] CreateFastChannelEndpoints()
        {
#if UNITY_EDITOR_WIN
            var host = sFastChannelHost;
            if (host != null && host.IsReady)
            {
                return new[]
                {
                    new YokiFrameEditorFastChannelEndpoint
                    {
                        engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID,
                        sessionId = sSessionId,
                        generation = sGeneration,
                        transport = FAST_CHANNEL_NAMED_PIPE,
                        endpoint = host.PipeName,
                        enabled = true,
                        fallback = FAST_CHANNEL_FALLBACK,
                        readOnlyCommands = CreateFastChannelReadOnlyCommands()
                    }
                };
            }
#endif
            return new[]
            {
                new YokiFrameEditorFastChannelEndpoint
                {
                    engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID,
                    sessionId = sSessionId,
                    generation = sGeneration,
                    transport = FAST_CHANNEL_NONE,
                    endpoint = string.Empty,
                    enabled = false,
                    fallback = FAST_CHANNEL_FALLBACK,
                    readOnlyCommands = Array.Empty<string>()
                }
            };
        }

        /// <summary>
        /// 从当前 Unity CommandPolicy 生成 FastChannel 可执行的只读命令能力键。
        /// </summary>
        /// <returns>稳定排序前的 Kit/action 能力键数组。</returns>
        private static string[] CreateFastChannelReadOnlyCommands()
        {
            var commands = GetHostCommandPolicy().AllowedCommands;
            var readOnlyCommands = new System.Collections.Generic.List<string>();
            for (var index = 0; index < commands.Count; index++)
            {
                if (commands[index].Kind == YokiFrameCommandKind.ReadOnly)
                {
                    readOnlyCommands.Add(YokiFrameFastChannelContract.CreateCommandKey(
                        commands[index].Kit,
                        commands[index].Action));
                }
            }

            readOnlyCommands.Sort(StringComparer.Ordinal);
            return readOnlyCommands.ToArray();
        }

        /// <summary>
        /// 创建当前 registry capability 列表；FastChannel 未 ready 时不宣称它可以连接。
        /// </summary>
        /// <returns>当前已真实可用的 capability 字符串数组。</returns>
        private static string[] CreateBridgeCapabilities()
        {
#if UNITY_EDITOR_WIN
            if (sFastChannelHost != null && sFastChannelHost.IsReady)
            {
                return YokiFrameEditorTelemetryWriter.IsNotificationReady
                    ? new[] { "snapshot.read", "command.send", "bridge.status", "telemetry.read", "telemetry.notify", "fastchannel" }
                    : new[] { "snapshot.read", "command.send", "bridge.status", "telemetry.read", "fastchannel" };
            }
#endif
            return YokiFrameEditorTelemetryWriter.IsNotificationReady
                ? new[] { "snapshot.read", "command.send", "bridge.status", "telemetry.read", "telemetry.notify" }
                : new[] { "snapshot.read", "command.send", "bridge.status", "telemetry.read" };
        }

        /// <summary>
        /// 创建同机多项目不冲突的安全 Pipe 名称，包含项目范围哈希、session 与 generation。
        /// </summary>
        /// <returns>当前 Unity Editor session 专属 Pipe 名称。</returns>
        private static string CreateFastChannelPipeName()
        {
            var projectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(
                YokiFrameEditorFileBridgePaths.GetProjectRoot());
            return "YokiFrame.FastChannel.unity-editor." + projectScopeId + "." + sSessionId + "." + sGeneration;
        }
    }
}

#endif
