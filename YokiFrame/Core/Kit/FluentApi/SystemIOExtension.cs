using System.IO;

namespace YokiFrame
{
    public static class SystemIOExtension
    {
        /// <summary>
        /// 创建文件夹,如果存在则不创建
        /// </summary>
        public static string CreateDirIfNotExists(this string dirFullPath)
        {
            if (!Directory.Exists(dirFullPath))
            {
                Directory.CreateDirectory(dirFullPath);
            }

            return dirFullPath;
        }

        /// <summary>
        /// 删除文件夹，如果存在
        /// </summary>
        public static void DeleteDirIfExists(this string dirFullPath)
        {
            if (Directory.Exists(dirFullPath))
            {
                Directory.Delete(dirFullPath, true);
            }
        }

        /// <summary>
        /// 清空 Dir（保留目录),如果存在
        /// </summary>
        public static void EmptyDirIfExists(this string dirFullPath)
        {
            if (Directory.Exists(dirFullPath))
            {
                Directory.Delete(dirFullPath, true);
            }

            Directory.CreateDirectory(dirFullPath);
        }

        /// <summary>
        /// 删除文件 如果存在
        /// </summary>
        public static bool DeleteFileIfExists(this string fileFullPath)
        {
            if (File.Exists(fileFullPath))
            {
                File.Delete(fileFullPath);
                return true;
            }

            return false;
        }

        /// <summary>
        /// 合并路径
        /// </summary>
        public static string CombinePath(this string selfPath, string toCombinePath)
        {
            return Path.Combine(selfPath, toCombinePath);
        }

        /// <summary>
        /// 根据路径获取文件名
        /// </summary>
        public static string GetFileName(this string filePath)
        {
            return Path.GetFileName(filePath);
        }

        /// <summary>
        /// 根据路径获取文件名，不包含文件扩展名
        /// </summary>
        public static string GetFileNameWithoutExtend(this string filePath)
        {
            return Path.GetFileNameWithoutExtension(filePath);
        }

        /// <summary>
        /// 根据路径获取文件扩展名
        /// </summary>
        public static string GetFileExtendName(this string filePath)
        {
            return Path.GetExtension(filePath);
        }

        /// <summary>
        /// 获取文件夹路径
        /// </summary>
        public static string GetFolderPath(this string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return Path.GetDirectoryName(path);
        }
    }
}
