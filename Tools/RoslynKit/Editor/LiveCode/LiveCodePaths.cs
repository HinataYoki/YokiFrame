#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;

namespace YokiFrame
{
    public static class LiveCodePaths
    {
        /// <summary>解析新导出脚本路径。文件名必须等于类名加扩展名，且目标还不能存在。</summary>
        /// <param name="projectRoot">项目根。</param>
        /// <param name="outputPath">源码根下的相对路径。</param>
        /// <param name="className">导出类名。</param>
        /// <param name="sourceRoot">允许的源码根目录名。</param>
        /// <param name="extension">.cs 或 .gd，默认 .cs。</param>
        /// <returns>项目内绝对路径。</returns>
        public static string ExportScript(string projectRoot, string outputPath, string className,
            string sourceRoot, string extension = ".cs")
        {
            LiveCodeManager.ValidateName(className);
            ValidateRootAndExtension(sourceRoot, extension);
            if (outputPath == null || !outputPath.Replace('\\', '/').StartsWith(sourceRoot + "/", StringComparison.Ordinal)
                || Path.GetFileName(outputPath) != className + extension)
                throw new ArgumentException("Export path must be " + sourceRoot + "/.../" + className + extension + ".");
            string full = InsideSourceRoot(projectRoot, outputPath, sourceRoot);
            if (File.Exists(full)) throw new IOException("Export does not overwrite an existing script: " + outputPath);
            return full;
        }

        /// <summary>把 N 格式导出标识映射到 live-exports 记录路径。</summary>
        /// <param name="projectRoot">项目根。</param>
        /// <param name="exportId">导出标识。</param>
        /// <returns>记录 JSON 的绝对路径。</returns>
        public static string Record(string projectRoot, string exportId)
        {
            if (!Guid.TryParseExact(exportId, "N", out _)) throw new ArgumentException("Invalid export ID.");
            return YokiFrameFilePathPolicy.CombineInside(projectRoot, ".yokiframe", "engine", "live-exports", exportId + ".json");
        }

        /// <summary>解析已有导出脚本路径。不要求文件已经存在，但必须留在源码根内且扩展名匹配。</summary>
        /// <param name="projectRoot">项目根。</param>
        /// <param name="path">源码根下的相对路径。</param>
        /// <param name="sourceRoot">允许的源码根目录名。</param>
        /// <param name="extension">.cs 或 .gd，默认 .cs。</param>
        /// <returns>项目内绝对路径。</returns>
        public static string ExistingScript(string projectRoot, string path, string sourceRoot, string extension = ".cs")
        {
            ValidateRootAndExtension(sourceRoot, extension);
            if (path == null || !path.Replace('\\', '/').StartsWith(sourceRoot + "/", StringComparison.Ordinal)
                || Path.GetExtension(path) != extension) throw new ArgumentException("Invalid exported script path.");
            return InsideSourceRoot(projectRoot, path, sourceRoot);
        }

        /// <summary>把相对路径限制在指定源码根目录内。</summary>
        /// <param name="projectRoot">项目根。</param>
        /// <param name="path">项目相对路径。</param>
        /// <param name="sourceRoot">源码根目录名。</param>
        /// <returns>源码根内的绝对路径。</returns>
        private static string InsideSourceRoot(string projectRoot, string path, string sourceRoot)
        {
            string directory = YokiFrameFilePathPolicy.CombineInside(projectRoot, sourceRoot);
            string full = YokiFrameFilePathPolicy.CombineInside(projectRoot, path);
            return YokiFrameFilePathPolicy.EnsureInside(directory, full);
        }

        /// <summary>源码根必须是标识符，扩展名只能是 .cs 或 .gd。</summary>
        /// <param name="root">源码根目录名。</param>
        /// <param name="extension">脚本扩展名。</param>
        private static void ValidateRootAndExtension(string root, string extension)
        {
            LiveCodeManager.ValidateName(root);
            if (extension != ".cs" && extension != ".gd")
                throw new ArgumentException("Expected .cs or .gd source.");
        }
    }
}
#endif
