using System.IO;
using UnityEngine;

namespace YokiFrame
{
    public static class PathUtils
    {
        public static string GetDirectoryPath(string inputPath)
        {
            if (string.IsNullOrEmpty(inputPath)) return "";

            // 统一路径格式：替换反斜杠，并移除末尾的斜杠
            string processedPath = inputPath.Replace('\\', '/').TrimEnd('/');

            // 找到最后一个斜杠的位置
            int lastSlashIndex = processedPath.LastIndexOf('/');

            // 根据斜杠位置分割路径
            if (lastSlashIndex != -1)
            {
                return processedPath.Substring(0, lastSlashIndex);
            }
            else
            {
                // 没有斜杠时返回空字符串（表示当前目录）
                return "";
            }
        }

        public static void CreateDirectory(string inputPath)
        {
            string directoryPath = GetDirectoryPath(inputPath);
            if (string.IsNullOrEmpty(directoryPath)) return;

            // 转换为绝对路径（基于Unity项目根目录）
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string absolutePath = Path.Combine(projectRoot, directoryPath);

            // 创建目录（如果不存在）
            if (!Directory.Exists(absolutePath))
            {
                Directory.CreateDirectory(absolutePath);
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
            }
        }
    }
}
