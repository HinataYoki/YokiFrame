#if UNITY_EDITOR
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    internal static class UnityAutomationCapture
    {
        /// <summary>
        /// 预约 Game 视图截图并在编辑器更新里等 PNG 写完。入口先观察取消；
        /// 非运行中的 game 模式、尺寸越界会立即抛出，不会开始截图。
        /// </summary>
        /// <param name="projectRoot">工程根。</param>
        /// <param name="mode">截图模式，只接受 game。</param>
        /// <param name="path">证据路径。</param>
        /// <param name="autoNumber">是否自动编号。</param>
        /// <param name="token">取消标记。调度前抛出，轮询中改为取消任务。</param>
        /// <returns>相对工程根的 PNG 路径。</returns>
        internal static Task<string> Capture(string projectRoot, string mode, string path,
            bool autoNumber, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            string fullPath = PrepareCaptureFile(projectRoot, mode, path, autoNumber);
            string relativePath = fullPath.Substring(
                Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Length + 1)
                .Replace('\\', '/');
            var completion = new TaskCompletionSource<string>();
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            EditorApplication.CallbackFunction poll = null;
            poll = () =>
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        EditorApplication.update -= poll;
                        completion.TrySetCanceled();
                        return;
                    }
                    if (DateTime.UtcNow >= deadline)
                        throw new TimeoutException("Game screenshot was not completed within 15 seconds.");
                    if (!File.Exists(fullPath)) return;
                    long length = new FileInfo(fullPath).Length;
                    if (length > 32 * 1024 * 1024) throw new IOException("Screenshot exceeds 32 MiB.");
                    if (length < 24) return;
                    using (var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        // CaptureScreenshot finishes asynchronously. IEND is the PNG's final chunk.
                        stream.Seek(-12, SeekOrigin.End);
                        var tail = new byte[12];
                        if (stream.Read(tail, 0, tail.Length) != tail.Length
                            || tail[4] != 'I' || tail[5] != 'E' || tail[6] != 'N' || tail[7] != 'D') return;
                    }
                    EditorApplication.update -= poll;
                    completion.TrySetResult(relativePath);
                }
                catch (Exception exception)
                {
                    EditorApplication.update -= poll;
                    completion.TrySetException(exception);
                }
            };
            ScreenCapture.CaptureScreenshot(fullPath);
            EditorApplication.update += poll;
            return completion.Task;
        }

        /// <summary>
        /// 校验 Game 视图后创建证据文件，用空文件占名，避免并发截图复用同一路径。
        /// </summary>
        /// <param name="projectRoot">工程根。</param>
        /// <param name="mode">截图模式。</param>
        /// <param name="path">调用方给出的证据路径。</param>
        /// <param name="autoNumber">是否自动编号。</param>
        /// <returns>已占名的绝对 PNG 路径。</returns>
        private static string PrepareCaptureFile(string projectRoot, string mode, string path, bool autoNumber)
        {
            if (mode != "game" || !EditorApplication.isPlaying || EditorApplication.isPaused)
                throw new NotSupportedException("game capture requires a running, unpaused Game view.");
            if (Screen.width <= 0 || Screen.height <= 0 || Screen.width > 4096 || Screen.height > 4096)
                throw new InvalidOperationException("Game view dimensions exceed the capture limit.");
            string fullPath = AutomationPaths.EvidencePng(projectRoot, path, autoNumber);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            fullPath = AutomationPaths.EvidencePng(projectRoot, fullPath);
            // Reserve before scheduling capture so concurrent submissions cannot reuse the same name.
            using (new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { }
            return fullPath;
        }
    }
}
#endif
