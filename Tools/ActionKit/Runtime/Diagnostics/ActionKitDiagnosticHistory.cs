#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>表示根 Action 离开调度器时的终态类型。</summary>
    internal enum ActionKitTerminalOutcome
    {
        /// <summary>正常完成。</summary>
        Completed,

        /// <summary>由稳定 controller 请求取消。</summary>
        Cancelled,

        /// <summary>生命周期或完成回调抛出异常。</summary>
        Faulted
    }

    /// <summary>保存一条不持有 Action 对象引用的终态诊断记录。</summary>
    internal readonly struct ActionKitTerminalEvent
    {
        /// <summary>创建根 Action 终态记录。</summary>
        /// <param name="actionId">根 Action 稳定 ID。</param>
        /// <param name="actionType">根 Action 类型名。</param>
        /// <param name="outcome">终态类型。</param>
        /// <param name="frame">发生终态的调度帧。</param>
        /// <param name="errorMessage">故障摘要；非故障为空。</param>
        internal ActionKitTerminalEvent(
            ulong actionId,
            string actionType,
            ActionKitTerminalOutcome outcome,
            long frame,
            string errorMessage)
        {
            ActionId = actionId;
            ActionType = actionType ?? string.Empty;
            Outcome = outcome;
            Frame = frame;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        /// <summary>获取根 Action ID。</summary>
        internal ulong ActionId { get; }

        /// <summary>获取根 Action 类型名。</summary>
        internal string ActionType { get; }

        /// <summary>获取终态类型。</summary>
        internal ActionKitTerminalOutcome Outcome { get; }

        /// <summary>获取终态发生帧。</summary>
        internal long Frame { get; }

        /// <summary>获取故障摘要。</summary>
        internal string ErrorMessage { get; }
    }

    /// <summary>使用固定数组保存最近根 Action 终态，避免高频完成路径持续扩容。</summary>
    internal static class ActionKitDiagnosticHistory
    {
        internal const int MAX_EVENTS = 64;
        internal const int MAX_ERROR_MESSAGE_LENGTH = 320;
        private static readonly object sSyncRoot = new();
        private static readonly ActionKitTerminalEvent[] sEvents =
            new ActionKitTerminalEvent[MAX_EVENTS];
        private static int sCount;
        private static int sNextIndex;
        private static long sTotalCount;

        /// <summary>获取当前有界历史数量。</summary>
        internal static int Count
        {
            get { lock (sSyncRoot) return sCount; }
        }

        /// <summary>获取当前会话累计终态数量。</summary>
        internal static long TotalCount
        {
            get { lock (sSyncRoot) return sTotalCount; }
        }

        /// <summary>记录一次终态，不保留 Action 或 Exception 对象。</summary>
        /// <param name="action">即将释放的根 Action。</param>
        /// <param name="outcome">根 Action 终态。</param>
        /// <param name="exception">可选故障异常。</param>
        internal static void Record(IAction action, ActionKitTerminalOutcome outcome, Exception exception = null)
        {
            if (action == null)
            {
                return;
            }

            ActionKitTerminalEvent terminalEvent = new(
                action.ActionID,
                action.GetType().Name,
                outcome,
                ActionKitScheduler.FrameCount,
                TruncateErrorMessage(ActionKitFailureReporter.GetExceptionMessage(exception)));
            lock (sSyncRoot)
            {
                sEvents[sNextIndex] = terminalEvent;
                sNextIndex = (sNextIndex + 1) % MAX_EVENTS;
                if (sCount < MAX_EVENTS)
                {
                    sCount++;
                }

                sTotalCount++;
            }
        }

        /// <summary>创建最新优先的独立终态数组，仅在显式诊断读取时分配。</summary>
        /// <returns>最新优先的有界终态记录。</returns>
        internal static ActionKitTerminalEvent[] CreateLatestSnapshot()
        {
            lock (sSyncRoot)
            {
                ActionKitTerminalEvent[] result = new ActionKitTerminalEvent[sCount];
                for (var index = 0; index < sCount; index++)
                {
                    int sourceIndex = (sNextIndex - 1 - index + MAX_EVENTS) % MAX_EVENTS;
                    result[index] = sEvents[sourceIndex];
                }

                return result;
            }
        }

        /// <summary>释放事件文本引用并重置当前会话累计数量。</summary>
        internal static void Clear()
        {
            lock (sSyncRoot)
            {
                Array.Clear(sEvents, 0, sEvents.Length);
                sCount = 0;
                sNextIndex = 0;
                sTotalCount = 0L;
            }
        }

        /// <summary>在固定环写入前裁剪异常文本，并避免从代理对中间截断。</summary>
        /// <param name="message">原始异常消息。</param>
        /// <returns>长度有界且不以孤立高代理项结尾的消息。</returns>
        private static string TruncateErrorMessage(string message)
        {
            if (string.IsNullOrEmpty(message) || message.Length <= MAX_ERROR_MESSAGE_LENGTH)
                return message ?? string.Empty;

            int length = MAX_ERROR_MESSAGE_LENGTH;
            if (char.IsHighSurrogate(message[length - 1]) && char.IsLowSurrogate(message[length]))
                length--;
            return message.Substring(0, length);
        }
    }
}
#endif
