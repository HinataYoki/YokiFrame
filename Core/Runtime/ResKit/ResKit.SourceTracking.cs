using System;
using System.Diagnostics;

namespace YokiFrame
{
    public static partial class ResKit
    {
        private static SourceLocation CaptureLoadSource()
        {
            if (!EnableLoadLocationTracking)
                return new SourceLocation("ResKit", string.Empty, 0);

            var stackTrace = new StackTrace(2, true);
            var frames = stackTrace.GetFrames();
            if (frames == null)
                return new SourceLocation("ResKit", string.Empty, 0);

            for (var i = 0; i < frames.Length; i++)
            {
                var method = frames[i].GetMethod();
                var declaringType = method != null ? method.DeclaringType : null;
                var typeName = declaringType != null ? declaringType.FullName : string.Empty;
                if (IsInternalResKitFrame(typeName))
                    continue;

                var methodName = method != null ? method.Name : "Unknown";
                var filePath = frames[i].GetFileName();
                var line = frames[i].GetFileLineNumber();
                var display = string.IsNullOrEmpty(typeName) ? methodName : typeName + "." + methodName;
                return new SourceLocation(display, string.IsNullOrEmpty(filePath) ? string.Empty : filePath.Replace('\\', '/'), line);
            }

            return new SourceLocation("ResKit", string.Empty, 0);
        }

        private static bool IsInternalResKitFrame(string typeName)
        {
            return string.IsNullOrEmpty(typeName) ||
                   typeName.IndexOf("YokiFrame.ResKit", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("YokiFrame.ResHandle", StringComparison.Ordinal) >= 0 ||
                   typeName.IndexOf("System.Runtime", StringComparison.Ordinal) >= 0;
        }

        private readonly struct SourceLocation
        {
            public readonly string Display;
            public readonly string FilePath;
            public readonly int Line;

            public SourceLocation(string display, string filePath, int line)
            {
                Display = display;
                FilePath = filePath;
                Line = line;
            }
        }
    }
}
