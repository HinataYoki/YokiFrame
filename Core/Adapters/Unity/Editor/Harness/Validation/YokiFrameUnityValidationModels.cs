#if UNITY_EDITOR

using System;

namespace YokiFrame
{
    /// <summary>保存 FileBridge 当前 Unity Editor 会话身份。</summary>
    [Serializable]
    internal sealed class YokiFrameUnityHarnessContext
    {
        public string engineId = string.Empty;
        public string mode = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;
    }

    /// <summary>验证响应共有的会话字段，避免诊断结果脱离当前 Editor。</summary>
    [Serializable]
    internal abstract class YokiFrameUnityHarnessResult
    {
        public string engineId = string.Empty;
        public string mode = string.Empty;
        public string sessionId = string.Empty;
        public long generation;
        public long sequence;

        /// <summary>把当前 FileBridge 会话身份写入响应 DTO。</summary>
        /// <param name="context">当前 Unity Editor 会话。</param>
        public void ApplyContext(YokiFrameUnityHarnessContext context)
        {
            engineId = context.engineId;
            mode = context.mode;
            sessionId = context.sessionId;
            generation = context.generation;
            sequence = context.sequence;
        }
    }

    /// <summary>表示 Unity 编译状态查询结果。</summary>
    [Serializable]
    internal sealed class YokiFrameUnityValidationObservation : YokiFrameUnityHarnessResult
    {
        public string observedAtUtc = string.Empty;
        public string status = "Ready";
        public YokiFrameUnityCompilationObservation compilation = new YokiFrameUnityCompilationObservation();
        public YokiFrameUnityValidationIssue[] issues = Array.Empty<YokiFrameUnityValidationIssue>();
    }

    /// <summary>表示 Unity Editor 当前公开的脚本编译事实。</summary>
    [Serializable]
    internal sealed class YokiFrameUnityCompilationObservation
    {
        public string state = "Idle";
        public bool isCompiling;
        public bool scriptCompilationFailed;
        public bool isUpdating;
        public string source = string.Empty;
    }

    /// <summary>表示编译状态读取失败或降级的诊断条目。</summary>
    [Serializable]
    internal sealed class YokiFrameUnityValidationIssue
    {
        public string code = string.Empty;
        public string message = string.Empty;
        public string source = string.Empty;
    }

    /// <summary>供验证服务和 EditMode 测试注入的编译事实。</summary>
    internal sealed class YokiFrameUnityCompilationProbe
    {
        public bool IsCompiling { get; set; }
        public bool ScriptCompilationFailed { get; set; }
        public bool IsUpdating { get; set; }
    }

    /// <summary>抽象 Unity 编译事实源，不触发编译或资源刷新。</summary>
    internal interface IYokiFrameUnityValidationProbeProvider
    {
        /// <summary>读取当前 Unity 编译事实。</summary>
        /// <returns>编译事实。</returns>
        YokiFrameUnityCompilationProbe ReadCompilation();
    }

    /// <summary>保存 Console Error 查询的有界参数。</summary>
    [Serializable]
    internal sealed class YokiFrameUnityConsoleErrorRequest
    {
        public int maxCount;
    }

    /// <summary>在验证命令中报告结构化查询失败。</summary>
    internal sealed class YokiFrameUnityHarnessQueryException : Exception
    {
        /// <summary>创建带稳定错误码的诊断异常。</summary>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">诊断消息。</param>
        public YokiFrameUnityHarnessQueryException(string code, string message)
            : base(message)
        {
            Code = code;
        }

        /// <summary>获取稳定错误码。</summary>
        public string Code { get; }
    }

    /// <summary>限制 Console 查询 payload 为 JSON 对象。</summary>
    internal static class YokiFrameUnityHarnessPayloadParser
    {
        /// <summary>解析对象 payload，空 payload 按空对象处理。</summary>
        /// <typeparam name="T">目标 DTO 类型。</typeparam>
        /// <param name="payloadJson">JSON payload。</param>
        /// <param name="operation">诊断操作名称。</param>
        /// <returns>解析后的 DTO。</returns>
        public static T ParseObject<T>(string payloadJson, string operation) where T : new()
        {
            var json = string.IsNullOrEmpty(payloadJson) ? "{}" : payloadJson;
            if (json.TrimStart().Length == 0 || json.TrimStart()[0] != '{')
            {
                throw new YokiFrameUnityHarnessQueryException("InvalidPayload", operation + " payload must be a JSON object.");
            }

            try
            {
                var value = UnityEngine.JsonUtility.FromJson<T>(json);
                return value ?? new T();
            }
            catch (ArgumentException exception)
            {
                throw new YokiFrameUnityHarnessQueryException("InvalidPayload", operation + " payload is invalid JSON: " + exception.Message);
            }
        }
    }
}

#endif
