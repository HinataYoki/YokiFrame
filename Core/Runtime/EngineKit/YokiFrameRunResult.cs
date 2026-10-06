using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 通用自动化运行结论：状态、断言、日志、异常与耗时。
    /// </summary>
    public sealed class YokiFrameRunResult
    {
        /// <summary>获取或设置运行状态。</summary>
        public YokiFrameRunStatus Status { get; set; } = YokiFrameRunStatus.Queued;

        /// <summary>获取断言明细。</summary>
        public IReadOnlyList<YokiFrameRunAssertion> Assertions { get; set; } = Array.Empty<YokiFrameRunAssertion>();

        /// <summary>获取运行期日志。</summary>
        public IReadOnlyList<string> Logs { get; set; } = Array.Empty<string>();

        /// <summary>获取未捕获异常类型全名；无异常时为空。</summary>
        public string ExceptionType { get; set; } = string.Empty;

        /// <summary>获取未捕获异常消息；无异常时为空。</summary>
        public string ExceptionMessage { get; set; } = string.Empty;

        /// <summary>获取未捕获异常堆栈；无异常时为空。</summary>
        public string ExceptionStack { get; set; } = string.Empty;

        /// <summary>获取运行耗时毫秒。</summary>
        public long DurationMs { get; set; }

        /// <summary>获取运行期间推进的帧数。</summary>
        public int Frames { get; set; }

        /// <summary>获取上下文失效后仍被调用的次数。</summary>
        public int StaleContextCalls { get; set; }

        /// <summary>获取框架附加说明（重载、迟到完成、清理等）。</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>判断是否全部断言通过且无异常。</summary>
        /// <returns>通过时返回 true。</returns>
        public bool IsPassed()
        {
            return Status == YokiFrameRunStatus.Passed;
        }
    }
}
