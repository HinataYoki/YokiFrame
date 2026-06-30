namespace YokiFrame
{
    public enum ManagedRuntimeDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    /// <summary>
    /// 运行时后端诊断项，用于工作台、AI 查询和构建前检查。
    /// </summary>
    public sealed class ManagedRuntimeDiagnostic
    {
        public ManagedRuntimeDiagnostic(
            ManagedRuntimeDiagnosticSeverity severity,
            string code,
            string message,
            string hint)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Hint = hint ?? string.Empty;
        }

        public ManagedRuntimeDiagnosticSeverity Severity { get; private set; }

        public string Code { get; private set; }

        public string Message { get; private set; }

        public string Hint { get; private set; }
    }
}
