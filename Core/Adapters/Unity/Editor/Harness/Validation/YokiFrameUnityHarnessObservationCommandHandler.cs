#if UNITY_EDITOR

using System;

namespace YokiFrame
{
    /// <summary>注册并执行 YokiFrame 的两个 Unity 只读诊断命令。</summary>
    internal sealed class YokiFrameUnityHarnessObservationCommandHandler : IYokiFrameCommandHandler
    {
        private readonly Func<YokiFrameUnityHarnessContext> mContextProvider;

        /// <summary>使用动态会话身份 provider 创建诊断 handler。</summary>
        /// <param name="contextProvider">读取当前 Unity Editor 会话身份的 provider。</param>
        public YokiFrameUnityHarnessObservationCommandHandler(Func<YokiFrameUnityHarnessContext> contextProvider)
        {
            mContextProvider = contextProvider ?? throw new ArgumentNullException(nameof(contextProvider));
        }

        /// <summary>创建两个只读命令描述。</summary>
        /// <returns>验证命令描述数组。</returns>
        public static YokiFrameCommandDescriptor[] CreateCommandDescriptors()
        {
            return new[]
            {
                new YokiFrameCommandDescriptor(YokiFrameUnityValidationContract.KIT_NAME, YokiFrameUnityValidationContract.INSPECT_STATUS_ACTION, YokiFrameCommandKind.ReadOnly),
                new YokiFrameCommandDescriptor(YokiFrameUnityValidationContract.KIT_NAME, YokiFrameUnityValidationContract.GET_CONSOLE_ERRORS_ACTION, YokiFrameCommandKind.ReadOnly)
            };
        }

        /// <summary>判断请求是否属于 Unity 只读诊断命令。</summary>
        /// <param name="request">待路由命令。</param>
        /// <returns>命中时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return request != null
                && request.Kit == YokiFrameUnityValidationContract.KIT_NAME
                && (request.Action == YokiFrameUnityValidationContract.INSPECT_STATUS_ACTION
                    || request.Action == YokiFrameUnityValidationContract.GET_CONSOLE_ERRORS_ACTION);
        }

        /// <summary>执行编译状态或 Console Error 查询。</summary>
        /// <param name="request">已通过 policy 的命令请求。</param>
        /// <returns>结构化诊断结果或稳定错误。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            if (!CanHandle(request))
            {
                return YokiFrameCommandResult.Error("HandlerMismatch", "Unity validation handler does not support this command.");
            }

            try
            {
                var context = mContextProvider();
                if (context == null || string.IsNullOrEmpty(context.sessionId) || context.generation <= 0L)
                {
                    throw new YokiFrameUnityHarnessQueryException("ValidationIdentityUnavailable", "Unity validation session identity is unavailable.");
                }

                var observation = ExecuteObservation(request, context);
                return YokiFrameCommandResult.Success(YokiFrameEditorFileBridgeJson.ToJson(observation));
            }
            catch (YokiFrameUnityHarnessQueryException exception)
            {
                return YokiFrameCommandResult.Error(exception.Code, exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("ValidationObservationFailed", exception.Message);
            }
        }

        /// <summary>按 action 校验 payload 并执行对应只读诊断。</summary>
        /// <param name="request">当前命令请求。</param>
        /// <param name="context">当前 Unity Editor 会话身份。</param>
        /// <returns>编译状态或 Console Error 观察结果。</returns>
        private static object ExecuteObservation(
            YokiFrameCommandRequest request,
            YokiFrameUnityHarnessContext context)
        {
            if (request.Action == YokiFrameUnityValidationContract.INSPECT_STATUS_ACTION)
            {
                YokiFrameUnityHarnessPayloadParser.ParseObject<YokiFrameUnityEmptyPayload>(
                    request.PayloadJson,
                    YokiFrameUnityValidationContract.INSPECT_STATUS_ACTION);
                return YokiFrameUnityValidationStatus.Inspect(context);
            }

            var payload = YokiFrameUnityHarnessPayloadParser.ParseObject<YokiFrameUnityConsoleErrorRequest>(
                request.PayloadJson,
                YokiFrameUnityValidationContract.GET_CONSOLE_ERRORS_ACTION);
            return YokiFrameUnityConsoleErrors.Inspect(context, payload.maxCount);
        }

        /// <summary>用于限制 inspect_status 仅接受 JSON 对象。</summary>
        [Serializable]
        private sealed class YokiFrameUnityEmptyPayload
        {
        }
    }
}

#endif
