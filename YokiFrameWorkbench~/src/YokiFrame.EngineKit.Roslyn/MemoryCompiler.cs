using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace YokiFrame.EngineKit.Roslyn
{
    /// <summary>Compiles source without loading it or creating source/assembly files.</summary>
    public static class MemoryCompiler
    {
        public const int MaxSourceBytes = 132 * 1024;
        public const int MaxArtifactBytes = 8 * 1024 * 1024;
        public const int MaxDiagnostics = 100;

        // Only BCL types cross this boundary, keeping Roslyn out of the host's assembly references.
        public static byte[] Compile(
            string source, string[] referencePaths, CancellationToken cancellationToken,
            out byte[] symbols, out string[][] diagnostics)
        {
            var metadata = new List<AssemblyMetadata>();
            try { return CompileCore(source, referencePaths, cancellationToken, metadata, out symbols, out diagnostics); }
            finally
            {
                // File-backed metadata owns native resources. Release on success, failure and cancellation.
                foreach (var item in metadata) item.Dispose();
            }
        }

        private static byte[] CompileCore(string source, string[] referencePaths, CancellationToken cancellationToken,
            List<AssemblyMetadata> metadata, out byte[] symbols, out string[][] diagnostics)
        {
            symbols = Array.Empty<byte>();
            diagnostics = Array.Empty<string[]>();
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(source) || Encoding.UTF8.GetByteCount(source) > MaxSourceBytes)
            {
                diagnostics = Error("ScriptInputLimit", "Source must be nonempty and at most 132 KiB.");
                return null;
            }

            var references = new List<MetadataReference>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (string path in referencePaths ?? Array.Empty<string>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (paths.Add(Path.GetFullPath(path)))
                    {
                        var image = AssemblyMetadata.CreateFromFile(path);
                        metadata.Add(image);
                        references.Add(image.GetReference(filePath: path));
                    }
                }
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException
                || exception is BadImageFormatException || exception is UnauthorizedAccessException)
            {
                diagnostics = Error("ScriptReferenceInvalid", exception.Message);
                return null;
            }

            SyntaxTree tree = CSharpSyntaxTree.ParseText(
                SourceText.From(source, Encoding.UTF8),
                new CSharpParseOptions(LanguageVersion.CSharp9),
                "automation.cs", cancellationToken);
            var compilation = CSharpCompilation.Create(
                "YokiFrame.Automation." + Guid.NewGuid().ToString("N"),
                new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug, allowUnsafe: false));

            using (var pe = new BoundedStream())
            using (var pdb = new BoundedStream())
            {
                EmitResult emitted;
                try
                {
                    emitted = compilation.Emit(pe, pdb,
                        options: new EmitOptions(debugInformationFormat: DebugInformationFormat.PortablePdb),
                        cancellationToken: cancellationToken);
                }
                catch (IOException exception)
                {
                    diagnostics = Error("ScriptArtifactLimit", exception.Message);
                    return null;
                }

                var messages = new List<string[]>();
                foreach (Diagnostic diagnostic in emitted.Diagnostics)
                {
                    if (diagnostic.Severity == DiagnosticSeverity.Hidden) continue;
                    if (messages.Count == MaxDiagnostics - 1)
                    {
                        messages.Add(new[] { "ScriptDiagnosticsTruncated", "Warning",
                            "Additional diagnostics were omitted.", "", "0", "0" });
                        break;
                    }
                    FileLinePositionSpan span = diagnostic.Location.GetMappedLineSpan();
                    messages.Add(new[]
                    {
                        diagnostic.Id, diagnostic.Severity.ToString(),
                        Limit(diagnostic.GetMessage(CultureInfo.InvariantCulture)), span.Path ?? "",
                        (span.IsValid ? span.StartLinePosition.Line + 1 : 0).ToString(CultureInfo.InvariantCulture),
                        (span.IsValid ? span.StartLinePosition.Character + 1 : 0).ToString(CultureInfo.InvariantCulture)
                    });
                }
                diagnostics = messages.ToArray();
                if (!emitted.Success) return null;
                cancellationToken.ThrowIfCancellationRequested();
                symbols = pdb.ToArray();
                return pe.ToArray();
            }
        }

        private static string[][] Error(string code, string message)
        {
            return new[] { new[] { code, "Error", message, "", "0", "0" } };
        }

        private static string Limit(string message)
        {
            return message.Length <= 4096 ? message : message.Substring(0, 4096);
        }

        private sealed class BoundedStream : MemoryStream
        {
            public override void Write(byte[] buffer, int offset, int count)
            {
                Check(Position + count);
                base.Write(buffer, offset, count);
            }
            public override void WriteByte(byte value)
            {
                Check(Position + 1);
                base.WriteByte(value);
            }
            public override void SetLength(long value)
            {
                Check(value);
                base.SetLength(value);
            }
            private static void Check(long value)
            {
                if (value > MaxArtifactBytes)
                    throw new IOException("Compiled PE/PDB exceeds 8 MiB.");
            }
        }
    }
}
