#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 运行步骤轨迹；只记录状态迁移，不含用户数据。
    /// </summary>
    public sealed class RoslynRunStep
    {
        /// <summary>获取或设置步骤名。</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>获取或设置步骤状态。</summary>
        public string State { get; set; } = string.Empty;

        /// <summary>获取或设置发生时间（UTC，ISO-8601 往返格式）。</summary>
        public string AtUtc { get; set; } = string.Empty;

        /// <summary>获取或设置补充说明。</summary>
        public string Message { get; set; } = string.Empty;
    }

    /// <summary>
    /// 异步运行记录（§10）：持久化在 <c>&lt;project&gt;/.yokiframe/engine/runs/</c>，跨域重载按代次判定归属。
    /// </summary>
    public sealed class RoslynRunRecord
    {
        /// <summary>获取或设置运行标识。</summary>
        public string RunId { get; set; } = string.Empty;

        /// <summary>获取或设置发起该运行的命令 requestId；请求索引的主键。</summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>获取或设置命令来源。</summary>
        public string Source { get; set; } = string.Empty;

        /// <summary>Historical entry name; retained only for reading old run records.</summary>
        public string LegacyEntry { get; set; } = string.Empty;

        /// <summary>Empty for legacy records; script runs do not name a catalog entry.</summary>
        public string Kind { get; set; } = string.Empty;

        public string PayloadHash { get; set; } = string.Empty;

        public int TimeoutMs { get; set; }

        /// <summary>获取或设置执行目标 wire 文本。</summary>
        public string Target { get; set; } = string.Empty;

        /// <summary>获取或设置参数 JSON。</summary>
        public string ArgsJson { get; set; } = "{}";

        /// <summary>获取或设置当前状态。</summary>
        public RunStatus State { get; set; } = RunStatus.Queued;

        /// <summary>获取或设置提交时间。</summary>
        public DateTime SubmittedAtUtc { get; set; }

        /// <summary>获取或设置开始时间。</summary>
        public DateTime? StartedAtUtc { get; set; }

        /// <summary>获取或设置最后更新时间。</summary>
        public DateTime UpdatedAtUtc { get; set; }

        /// <summary>获取或设置过期时间；用于 TTL 回收。</summary>
        public DateTime? ExpiresAtUtc { get; set; }

        /// <summary>获取或设置认领该运行的会话标识。</summary>
        public string OwnerSessionId { get; set; } = string.Empty;

        /// <summary>Stable host identity; other hosts must not reconcile or cancel this script.</summary>
        public string OwnerHostId { get; set; } = string.Empty;

        /// <summary>获取或设置认领时的域代次。</summary>
        public long Generation { get; set; }

        /// <summary>获取或设置认领尝试次数。</summary>
        public int Attempt { get; set; }

        /// <summary>获取或设置结果文件相对路径。</summary>
        public string ResultPath { get; set; } = string.Empty;

        /// <summary>获取或设置错误码。</summary>
        public string ErrorCode { get; set; } = string.Empty;

        /// <summary>获取或设置取消/超时宽限期毫秒；小于等于 0 表示使用框架默认值。</summary>
        public int GraceMs { get; set; }

        /// <summary>获取步骤轨迹。</summary>
        public List<RoslynRunStep> Steps { get; } = new List<RoslynRunStep>();

        /// <summary>获取或设置收到取消请求的时间。</summary>
        public DateTime? CancelRequestedAtUtc { get; set; }

        /// <summary>获取或设置收到超时请求的时间。</summary>
        public DateTime? TimeoutRequestedAtUtc { get; set; }

        /// <summary>获取或设置判定为脱离的时间。</summary>
        public DateTime? DetachedAtUtc { get; set; }

        /// <summary>获取或设置迟到完成的时间。</summary>
        public DateTime? LateCompletionAtUtc { get; set; }

        /// <summary>获取或设置迟到结果文件路径；终态不被覆盖。</summary>
        public string LateResultPath { get; set; } = string.Empty;

        /// <summary>获取或设置上下文失效后的框架调用次数。</summary>
        public int StaleContextCalls { get; set; }

        /// <summary>获取或设置框架附注（重载、迟到完成等）。</summary>
        public string Note { get; set; } = string.Empty;

        /// <summary>获取或设置终态结果。</summary>
        public RunResult Result { get; set; }

        /// <summary>获取是否为终态。</summary>
        public bool IsTerminal
        {
            get
            {
                switch (State)
                {
                    case RunStatus.Passed:
                    case RunStatus.Failed:
                    case RunStatus.Errored:
                    case RunStatus.Cancelled:
                    case RunStatus.Timeout:
                    case RunStatus.Detached:
                    case RunStatus.Unknown:
                    case RunStatus.CompileFailed:
                        return true;
                    default:
                        return false;
                }
            }
        }

        /// <summary>追加一条步骤轨迹。</summary>
        /// <param name="name">步骤名。</param>
        /// <param name="state">步骤状态。</param>
        /// <param name="message">补充说明。</param>
        public void AddStep(string name, string state, string message)
        {
            Steps.Add(new RoslynRunStep
            {
                Name = name ?? string.Empty,
                State = state ?? string.Empty,
                AtUtc = DateTime.UtcNow.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                Message = message ?? string.Empty
            });
        }
    }
}
#endif
