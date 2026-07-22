#if UNITY_EDITOR

using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 为 Unity Editor 最小 FileBridge pump 提供命令来源、动作和资源边界校验。
    /// </summary>
    internal static class YokiFrameEditorCommandPolicy
    {
        /// <summary>
        /// 检查命令文件整体大小；超过上限时让调用侧进入 deadletter，避免读取超大文件。
        /// </summary>
        /// <param name="commandPath">命令文件路径。</param>
        public static void EnsureCommandFileSize(string commandPath)
        {
            var fileInfo = new FileInfo(commandPath);
            if (fileInfo.Length <= YokiFrameCommandPolicy.COMMAND_FILE_MAX_BYTES)
            {
                return;
            }

            throw new InvalidDataException("Command file exceeds FileBridge policy size limit.");
        }
    }
}

#endif
