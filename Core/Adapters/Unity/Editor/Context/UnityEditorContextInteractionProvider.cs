#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>把 Unity Editor 公共上下文接入统一 Kit Interaction Registry。</summary>
    public sealed class UnityEditorContextInteractionProvider : IYokiFrameVersionedKitInteractionProvider
    {
        private const string KIT = "UnityEditor";
        private const string STATE = "state";
        private const string GET_CONTEXT = "get_context";
        private static readonly IReadOnlyList<string> sSnapshotNames =
            Array.AsReadOnly(new[] { STATE });
        private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands =
            Array.AsReadOnly(new[]
            {
                new YokiFrameCommandDescriptor(KIT, GET_CONTEXT, YokiFrameCommandKind.ReadOnly)
            });

        /// <summary>获取稳定的 Unity Editor Context Kit 标识。</summary>
        public string Kit => KIT;

        /// <summary>获取唯一上下文 state Snapshot 名称。</summary>
        public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

        /// <summary>获取只读上下文查询命令目录。</summary>
        public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

        /// <summary>获取 Selection/Scene/Prefab 状态的单调 revision。</summary>
        public long StateVersion => UnityEditorContextService.Revision;

        /// <summary>判断请求是否为 UnityEditor/get_context。</summary>
        /// <param name="request">待匹配命令。</param>
        /// <returns>命中当前 Kit/action 时返回 true。</returns>
        public bool CanHandle(YokiFrameCommandRequest request)
        {
            return request != null
                && string.Equals(request.Kit, KIT, StringComparison.Ordinal)
                && string.Equals(request.Action, GET_CONTEXT, StringComparison.Ordinal);
        }

        /// <summary>执行只读上下文查询并返回终态结果。</summary>
        /// <param name="request">已通过 Kit/action 匹配的请求。</param>
        /// <returns>上下文 JSON 或稳定错误。</returns>
        public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
        {
            if (!CanHandle(request))
            {
                return YokiFrameCommandResult.Error(
                    "HandlerMismatch",
                    "Unity Editor context handler does not support this command.");
            }

            try
            {
                RequireEmptyObject(request.PayloadJson);
                return YokiFrameCommandResult.Success(CreateSnapshot(STATE));
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error("InvalidPayload", exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("UnityEditorContextFailed", exception.Message);
            }
        }

        /// <summary>创建当前 Unity Editor state Snapshot。</summary>
        /// <param name="snapshotName">必须为 state。</param>
        /// <returns>有界上下文 JSON。</returns>
        public string CreateSnapshot(string snapshotName)
        {
            if (!string.Equals(snapshotName, STATE, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Unsupported Unity Editor context snapshot: " + snapshotName,
                    nameof(snapshotName));
            }

            return UnityEditorContextSnapshotWriter.Write(UnityEditorContextService.Capture());
        }

        /// <summary>只接受空 payload，防止只读查询携带隐藏写操作参数。</summary>
        /// <param name="payloadJson">待验证的 JSON payload。</param>
        private static void RequireEmptyObject(string payloadJson)
        {
            string normalized = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson.Trim();
            if (!string.Equals(normalized, "{}", StringComparison.Ordinal))
            {
                throw new ArgumentException("UnityEditor/get_context payload must be an empty JSON object.");
            }
        }
    }
}
#endif
