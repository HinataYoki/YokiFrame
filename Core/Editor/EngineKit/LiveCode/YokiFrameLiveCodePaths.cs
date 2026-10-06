#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;

namespace YokiFrame
{
    public static class YokiFrameLiveCodePaths
    {
        public static string ExportScript(string projectRoot, string outputPath, string className,
            string sourceRoot, string extension = ".cs")
        {
            YokiFrameLiveCodeManager.ValidateName(className);
            ValidateRootAndExtension(sourceRoot, extension);
            if (outputPath == null || !outputPath.Replace('\\', '/').StartsWith(sourceRoot + "/", StringComparison.Ordinal)
                || Path.GetFileName(outputPath) != className + extension)
                throw new ArgumentException("Export path must be " + sourceRoot + "/.../" + className + extension + ".");
            string full = InsideSourceRoot(projectRoot, outputPath, sourceRoot);
            if (File.Exists(full)) throw new IOException("Export does not overwrite an existing script: " + outputPath);
            return full;
        }

        public static string Record(string projectRoot, string exportId)
        {
            if (!Guid.TryParseExact(exportId, "N", out _)) throw new ArgumentException("Invalid export ID.");
            return YokiFrameFilePathPolicy.CombineInside(projectRoot, ".yokiframe", "engine", "live-exports", exportId + ".json");
        }

        public static string ExistingScript(string projectRoot, string path, string sourceRoot, string extension = ".cs")
        {
            ValidateRootAndExtension(sourceRoot, extension);
            if (path == null || !path.Replace('\\', '/').StartsWith(sourceRoot + "/", StringComparison.Ordinal)
                || Path.GetExtension(path) != extension) throw new ArgumentException("Invalid exported script path.");
            return InsideSourceRoot(projectRoot, path, sourceRoot);
        }

        private static string InsideSourceRoot(string projectRoot, string path, string sourceRoot)
        {
            string directory = YokiFrameFilePathPolicy.CombineInside(projectRoot, sourceRoot);
            string full = YokiFrameFilePathPolicy.CombineInside(projectRoot, path);
            return YokiFrameFilePathPolicy.EnsureInside(directory, full);
        }

        private static void ValidateRootAndExtension(string root, string extension)
        {
            YokiFrameLiveCodeManager.ValidateName(root);
            if (extension != ".cs" && extension != ".gd")
                throw new ArgumentException("Expected .cs or .gd source.");
        }
    }
}
#endif
