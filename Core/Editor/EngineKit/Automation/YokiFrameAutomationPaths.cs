#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Globalization;
using System.IO;

namespace YokiFrame
{
    public static class YokiFrameAutomationPaths
    {
        public static string ProjectFile(string projectRoot, string path)
        {
            return YokiFrameFilePathPolicy.CombineInside(projectRoot, path);
        }

        public static string EvidencePng(string projectRoot, string path, bool autoNumber = false)
        {
            string resolved = ProjectFile(projectRoot, path);
            string evidence = ProjectFile(projectRoot, ".yokiframe/automation/evidence");
            YokiFrameFilePathPolicy.EnsureInside(evidence, resolved);
            if (!resolved.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Capture output must be a PNG in .yokiframe/automation/evidence.");
            if (!File.Exists(resolved) && !Directory.Exists(resolved)) return resolved;
            if (!autoNumber)
                throw new IOException("Capture will not overwrite existing evidence: " + path
                    + ". Pass autoNumber:true to select a new numbered PNG.");
            string stem = Path.Combine(Path.GetDirectoryName(resolved), Path.GetFileNameWithoutExtension(resolved));
            for (int i = 1; i <= 10000; i++)
            {
                string candidate = stem + "-" + i.ToString("D3", CultureInfo.InvariantCulture) + ".png";
                candidate = ProjectFile(projectRoot, candidate);
                YokiFrameFilePathPolicy.EnsureInside(evidence, candidate);
                if (!File.Exists(candidate) && !Directory.Exists(candidate)) return candidate;
            }
            throw new IOException("Capture automatic numbering exhausted 10000 candidates.");
        }
    }
}
#endif
