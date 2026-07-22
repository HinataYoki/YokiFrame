#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Diagnostics;
using System.Threading;

namespace YokiFrame
{
    public static partial class ResKit
    {
        private static readonly ResLoadSource sUntrackedSource =
            new(false, string.Empty, string.Empty, 0);

        /// <summary>按当前开关采集一次 lease 来源；关闭时不创建 StackTrace 或字符串。</summary>
        private static ResLoadSource CaptureLoadSource()
        {
            if (!Volatile.Read(ref sEnableLoadLocationTracking))
            {
                return sUntrackedSource;
            }

            try
            {
                StackTrace stackTrace = new(2, true);
                StackFrame[] frames = stackTrace.GetFrames();
                if (frames == null)
                {
                    return new ResLoadSource(true, "ResKit", string.Empty, 0);
                }

                return FindExternalLoadSource(frames);
            }
            catch (Exception)
            {
                return new ResLoadSource(true, "ResKit", string.Empty, 0);
            }
        }

        /// <summary>从堆栈中选择第一个不属于 ResKit 内部实现的调用位置。</summary>
        private static ResLoadSource FindExternalLoadSource(StackFrame[] frames)
        {
            for (var index = 0; index < frames.Length; index++)
            {
                var method = frames[index].GetMethod();
                Type declaringType = method == null ? null : method.DeclaringType;
                string typeName = declaringType == null ? string.Empty : declaringType.FullName;
                if (IsInternalResKitFrame(typeName))
                {
                    continue;
                }

                string methodName = method == null ? "Unknown" : method.Name;
                string display = string.IsNullOrEmpty(typeName) ? methodName : typeName + "." + methodName;
                string filePath = frames[index].GetFileName();
                return new ResLoadSource(
                    true,
                    display,
                    string.IsNullOrEmpty(filePath) ? string.Empty : filePath.Replace('\\', '/'),
                    frames[index].GetFileLineNumber());
            }

            return new ResLoadSource(true, "ResKit", string.Empty, 0);
        }

        /// <summary>判断堆栈类型是否属于资源门面或基础运行时框架。</summary>
        private static bool IsInternalResKitFrame(string typeName)
        {
            return string.IsNullOrEmpty(typeName)
                || typeName.IndexOf("YokiFrame.ResKit", StringComparison.Ordinal) >= 0
                || typeName.IndexOf("YokiFrame.ResHandle", StringComparison.Ordinal) >= 0
                || typeName.IndexOf("System.Runtime", StringComparison.Ordinal) >= 0;
        }
    }
}
#endif
