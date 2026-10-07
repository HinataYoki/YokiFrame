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

namespace YokiFrame.RoslynKit.Compiler
{
    /// <summary>在内存中编译 C#，不加载产物，也不写出源文件或程序集文件。</summary>
    public static class MemoryCompiler
    {
        public const int MaxSourceBytes = 132 * 1024;
        public const int MaxArtifactBytes = 8 * 1024 * 1024;
        public const int MaxDiagnostics = 100;

        /// <summary>
        /// 编译一段 C# 9 源码。只返回 PE 和 PDB 字节，不加载程序集，也不写源文件。
        /// 跨边界只传递 BCL 类型，避免宿主直接引用 Roslyn。
        /// </summary>
        /// <param name="source">用户源码。</param>
        /// <param name="referencePaths">元数据引用路径。重复路径只加载一次。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <param name="symbols">成功时的可移植 PDB；失败时为空数组。</param>
        /// <param name="diagnostics">诊断。每项依次是 code、severity、message、path、line、column。</param>
        /// <returns>成功时的 PE。输入或引用非法时返回 null。</returns>
        public static byte[] Compile(
            string source, string[] referencePaths, CancellationToken cancellationToken,
            out byte[] symbols, out string[][] diagnostics)
        {
            var metadata = new List<AssemblyMetadata>();
            try { return CompileCore(source, referencePaths, cancellationToken, metadata, out symbols, out diagnostics); }
            finally
            {
                // 文件型元数据持有本机资源。成功、失败和取消都要释放。
                foreach (var item in metadata) item.Dispose();
            }
        }

        /// <summary>执行校验、引用收集、编译和写出。调用方负责释放 metadata。</summary>
        /// <param name="source">用户源码。</param>
        /// <param name="referencePaths">元数据引用路径。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <param name="metadata">已打开的文件元数据，由调用方释放。</param>
        /// <param name="symbols">成功时的 PDB。</param>
        /// <param name="diagnostics">诊断。</param>
        /// <returns>成功时的 PE；失败时返回 null。</returns>
        private static byte[] CompileCore(string source, string[] referencePaths, CancellationToken cancellationToken,
            List<AssemblyMetadata> metadata, out byte[] symbols, out string[][] diagnostics)
        {
            symbols = Array.Empty<byte>();
            diagnostics = Array.Empty<string[]>();
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryValidateSource(source, out diagnostics)) return null;
            var references = new List<MetadataReference>();
            if (!TryCollectReferences(referencePaths, cancellationToken, metadata, references, out diagnostics))
                return null;
            return EmitImage(CreateCompilation(source, references, cancellationToken), cancellationToken,
                out symbols, out diagnostics);
        }

        /// <summary>拒绝空白源码和超过 132 KiB 的源码。</summary>
        /// <param name="source">用户源码。</param>
        /// <param name="diagnostics">失败时的单条错误。</param>
        /// <returns>源码可用时返回 true。</returns>
        private static bool TryValidateSource(string source, out string[][] diagnostics)
        {
            diagnostics = Array.Empty<string[]>();
            if (!string.IsNullOrWhiteSpace(source) && Encoding.UTF8.GetByteCount(source) <= MaxSourceBytes)
                return true;
            diagnostics = Error("ScriptInputLimit", "Source must be nonempty and at most 132 KiB.");
            return false;
        }

        /// <summary>按绝对路径去重加载引用。IO 或坏映像转为诊断，不向外抛出。</summary>
        /// <param name="referencePaths">引用路径，允许为 null。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <param name="metadata">成功打开的元数据会追加到这里。</param>
        /// <param name="references">对应的编译引用。</param>
        /// <param name="diagnostics">失败时的诊断。</param>
        /// <returns>引用全部可用时返回 true。</returns>
        private static bool TryCollectReferences(string[] referencePaths, CancellationToken cancellationToken,
            List<AssemblyMetadata> metadata, List<MetadataReference> references, out string[][] diagnostics)
        {
            diagnostics = Array.Empty<string[]>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (string path in referencePaths ?? Array.Empty<string>())
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (!paths.Add(Path.GetFullPath(path))) continue;
                    var image = AssemblyMetadata.CreateFromFile(path);
                    metadata.Add(image);
                    references.Add(image.GetReference(filePath: path));
                }
                return true;
            }
            catch (Exception exception) when (exception is IOException || exception is ArgumentException
                || exception is BadImageFormatException || exception is UnauthorizedAccessException)
            {
                diagnostics = Error("ScriptReferenceInvalid", exception.Message);
                return false;
            }
        }

        /// <summary>创建只输出调试 DLL、不允许 unsafe 的 C# 9 编译。</summary>
        /// <param name="source">用户源码。</param>
        /// <param name="references">已经打开的元数据引用。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <returns>尚未写出的编译。</returns>
        private static CSharpCompilation CreateCompilation(string source, List<MetadataReference> references,
            CancellationToken cancellationToken)
        {
            SyntaxTree tree = CSharpSyntaxTree.ParseText(
                SourceText.From(source, Encoding.UTF8),
                new CSharpParseOptions(LanguageVersion.CSharp9),
                "automation.cs", cancellationToken);
            return CSharpCompilation.Create(
                "YokiFrame.Automation." + Guid.NewGuid().ToString("N"),
                new[] { tree }, references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary,
                    optimizationLevel: OptimizationLevel.Debug, allowUnsafe: false));
        }

        /// <summary>写出 PE 和 PDB。产物超过 8 MiB 或编译失败时返回 null。</summary>
        /// <param name="compilation">已经创建的编译。</param>
        /// <param name="cancellationToken">取消标记。</param>
        /// <param name="symbols">成功时的 PDB。</param>
        /// <param name="diagnostics">编译诊断，最多 100 条。</param>
        /// <returns>成功时的 PE。</returns>
        private static byte[] EmitImage(CSharpCompilation compilation, CancellationToken cancellationToken,
            out byte[] symbols, out string[][] diagnostics)
        {
            symbols = Array.Empty<byte>();
            diagnostics = Array.Empty<string[]>();
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

                diagnostics = CollectDiagnostics(emitted);
                if (!emitted.Success) return null;
                cancellationToken.ThrowIfCancellationRequested();
                symbols = pdb.ToArray();
                return pe.ToArray();
            }
        }

        /// <summary>收集非 Hidden 诊断。达到上限时用一条警告代替其余内容。</summary>
        /// <param name="emitted">Roslyn 写出结果。</param>
        /// <returns>诊断数组。</returns>
        private static string[][] CollectDiagnostics(EmitResult emitted)
        {
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

                messages.Add(FormatDiagnostic(diagnostic));
            }

            return messages.ToArray();
        }

        /// <summary>把一条诊断格式化成固定六列。</summary>
        /// <param name="diagnostic">Roslyn 诊断。</param>
        /// <returns>code、severity、message、path、line、column。</returns>
        private static string[] FormatDiagnostic(Diagnostic diagnostic)
        {
            FileLinePositionSpan span = diagnostic.Location.GetMappedLineSpan();
            return new[]
            {
                diagnostic.Id, diagnostic.Severity.ToString(),
                Limit(diagnostic.GetMessage(CultureInfo.InvariantCulture)), span.Path ?? "",
                (span.IsValid ? span.StartLinePosition.Line + 1 : 0).ToString(CultureInfo.InvariantCulture),
                (span.IsValid ? span.StartLinePosition.Character + 1 : 0).ToString(CultureInfo.InvariantCulture)
            };
        }

        /// <summary>构造一条 Error 诊断。</summary>
        /// <param name="code">稳定错误码。</param>
        /// <param name="message">说明。</param>
        /// <returns>只含这一条诊断的数组。</returns>
        private static string[][] Error(string code, string message)
        {
            return new[] { new[] { code, "Error", message, "", "0", "0" } };
        }

        /// <summary>把诊断文本截到 4096 字符，避免超大消息进入结果。</summary>
        /// <param name="message">原始诊断文本。</param>
        /// <returns>截断后的文本。</returns>
        private static string Limit(string message)
        {
            return message.Length <= 4096 ? message : message.Substring(0, 4096);
        }

        /// <summary>限制 PE 和 PDB 写入不超过 8 MiB。超出时抛出 IOException，由写出流程转成诊断。</summary>
        private sealed class BoundedStream : MemoryStream
        {
            /// <summary>写入一块字节前检查上限。</summary>
            /// <param name="buffer">来源缓冲。</param>
            /// <param name="offset">起始偏移。</param>
            /// <param name="count">字节数。</param>
            public override void Write(byte[] buffer, int offset, int count)
            {
                Check(Position + count);
                base.Write(buffer, offset, count);
            }

            /// <summary>写入单字节前检查上限。</summary>
            /// <param name="value">字节。</param>
            public override void WriteByte(byte value)
            {
                Check(Position + 1);
                base.WriteByte(value);
            }

            /// <summary>调整长度前检查上限。</summary>
            /// <param name="value">新长度。</param>
            public override void SetLength(long value)
            {
                Check(value);
                base.SetLength(value);
            }

            /// <summary>超过 8 MiB 时抛出 IOException。</summary>
            /// <param name="value">预计长度。</param>
            private static void Check(long value)
            {
                if (value > MaxArtifactBytes)
                    throw new IOException("Compiled PE/PDB exceeds 8 MiB.");
            }
        }
    }
}
