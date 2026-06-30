using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 托管运行时后端验证结果。
    /// </summary>
    public sealed class ManagedRuntimeValidationResult
    {
        private static readonly ManagedRuntimeDiagnostic[] sEmptyDiagnostics = new ManagedRuntimeDiagnostic[0];

        private ManagedRuntimeValidationResult(
            bool isValid,
            ManagedRuntimeInfo info,
            ManagedRuntimeDiagnostic[] diagnostics)
        {
            IsValid = isValid;
            Info = info;
            Diagnostics = diagnostics ?? sEmptyDiagnostics;
        }

        public bool IsValid { get; private set; }

        public ManagedRuntimeInfo Info { get; private set; }

        public IReadOnlyList<ManagedRuntimeDiagnostic> Diagnostics { get; private set; }

        public static ManagedRuntimeValidationResult Valid(
            ManagedRuntimeInfo info,
            params ManagedRuntimeDiagnostic[] diagnostics)
        {
            return new ManagedRuntimeValidationResult(true, info, CopyDiagnostics(diagnostics));
        }

        public static ManagedRuntimeValidationResult Invalid(
            ManagedRuntimeInfo info,
            params ManagedRuntimeDiagnostic[] diagnostics)
        {
            return new ManagedRuntimeValidationResult(false, info, CopyDiagnostics(diagnostics));
        }

        private static ManagedRuntimeDiagnostic[] CopyDiagnostics(ManagedRuntimeDiagnostic[] diagnostics)
        {
            if (diagnostics == null || diagnostics.Length == 0)
                return sEmptyDiagnostics;

            var copy = new ManagedRuntimeDiagnostic[diagnostics.Length];
            Array.Copy(diagnostics, copy, diagnostics.Length);
            return copy;
        }
    }
}
