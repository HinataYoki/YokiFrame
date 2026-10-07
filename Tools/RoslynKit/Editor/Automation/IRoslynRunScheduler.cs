#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 异步运行调度器（§10）：tick 驱动、提交即返回、取消/超时都是"请求"而非"已停止"。
    /// </summary>
    public interface IRoslynRunScheduler
    {
        /// <summary>获取当前会话标识。</summary>
        string SessionId { get; }

        /// <summary>获取当前域代次。</summary>
        long Generation { get; }

        /// <summary>推进一帧；必须由宿主主线程按 tick 调用。</summary>
        void Tick();

        /// <summary>
        /// 读取最近运行记录（按提交时间倒序）；纯读、供 Workbench 展示，不触发任何用户代码。
        /// </summary>
        /// <param name="limit">最多返回条数；≤0 时返回空集合。</param>
        /// <returns>运行记录。</returns>
        IReadOnlyList<RoslynRunRecord> ReadRecentRuns(int limit);

        /// <summary>按运行标识读取记录。</summary>
        /// <param name="runId">运行标识。</param>
        /// <param name="record">运行记录。</param>
        /// <returns>命中时返回 true。</returns>
        bool TryReadRun(string runId, out RoslynRunRecord record);

        /// <summary>按 requestId 反查运行（§10.1）。</summary>
        /// <param name="requestId">命令 requestId。</param>
        /// <param name="record">运行记录。</param>
        /// <returns>命中时返回 true。</returns>
        bool TryLookupReadOnly(string requestId, out RoslynRunRecord record);

        /// <summary>请求取消运行；只置 CancelRequested，真正终止由调度器观察。</summary>
        /// <param name="runId">运行标识。</param>
        /// <param name="errorCode">失败错误码。</param>
        /// <param name="errorMessage">失败说明。</param>
        /// <returns>请求被接受时返回 true。</returns>
        bool TryCancel(string runId, out string errorCode, out string errorMessage);

        /// <summary>读取结果文件原文；纯读，不调用用户代码。</summary>
        /// <param name="relativePath">记录里的结果路径。</param>
        /// <param name="json">结果 JSON。</param>
        /// <returns>读取成功时返回 true。</returns>
        bool TryReadResultFile(string relativePath, out string json);

        /// <summary>仅对账本宿主的脚本运行；历史入口记录永不改写或重放。</summary>
        /// <param name="lease">认领租约；超过租约仍停在 Running 的运行视为孤儿。</param>
        /// <returns>对账处理的记录条数。</returns>
        int Reconcile(TimeSpan lease);
    }
}
#endif
