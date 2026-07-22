using System;

namespace YokiFrame
{
    /// <summary>隔离 ActionKit 故障诊断与业务生命周期，日志后端失败不得阻断清理。</summary>
    internal static class ActionKitFailureReporter
    {
        /// <summary>尽力写入错误日志；异常格式化或日志后端再次抛错时保持静默。</summary>
        /// <param name="prefix">不含异常详情的稳定日志前缀。</param>
        /// <param name="exception">原始故障。</param>
        internal static void TryLog(string prefix, Exception exception)
        {
            try
            {
                LogKit.Error((prefix ?? string.Empty) + GetExceptionDetails(exception));
            }
            catch (Exception)
            {
                // 故障报告属于 best-effort 路径，不能覆盖原异常或中断 Action 清理。
            }
        }

        /// <summary>安全读取异常摘要；自定义 Message 抛错时回退到异常类型名。</summary>
        /// <param name="exception">待读取异常。</param>
        /// <returns>不会主动抛出的异常摘要。</returns>
        internal static string GetExceptionMessage(Exception exception)
        {
            if (exception == null) return string.Empty;
            try
            {
                return exception.Message ?? string.Empty;
            }
            catch (Exception)
            {
                return exception.GetType().FullName ?? nameof(Exception);
            }
        }

        /// <summary>安全格式化完整异常；自定义 ToString 抛错时回退到安全摘要。</summary>
        /// <param name="exception">待格式化异常。</param>
        /// <returns>适合错误日志的详情文本。</returns>
        private static string GetExceptionDetails(Exception exception)
        {
            if (exception == null) return string.Empty;
            try
            {
                return exception.ToString();
            }
            catch (Exception)
            {
                return GetExceptionMessage(exception);
            }
        }
    }
}
