using System;
using System.IO;
using System.Threading;

namespace YokiFrame
{
    /// <summary>
    /// SaveKit 1.x 门面兼容入口。
    /// </summary>
    public static partial class SaveKit
    {
        private const string DEFAULT_FILE_PREFIX = "save_";
        private const string DEFAULT_FILE_EXTENSION = ".yoki";

        private static string sSavePath;
        private static string sFilePrefix = DEFAULT_FILE_PREFIX;
        private static string sFileExtension = DEFAULT_FILE_EXTENSION;

        /// <summary>
        /// 设置存档文件保存路径，并切换到文件存储后端。
        /// </summary>
        /// <param name="path">存档文件根目录。</param>
        public static void SetSavePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentNullException(nameof(path));
            }

            UseFileStorage(path, sFilePrefix, sFileExtension);
        }

        /// <summary>
        /// 获取当前存档文件保存路径。
        /// </summary>
        /// <returns>当前文件存储根目录。</returns>
        public static string GetSavePath()
        {
            RefreshFileSettingsFromStorage();
            if (string.IsNullOrEmpty(sSavePath))
            {
                UseFileStorage(GetDefaultSavePath(), sFilePrefix, sFileExtension);
            }

            return sSavePath;
        }

        /// <summary>
        /// 设置存档文件名前缀和扩展名。
        /// </summary>
        /// <param name="prefix">文件名前缀。</param>
        /// <param name="extension">文件扩展名。</param>
        public static void SetFileFormat(string prefix = DEFAULT_FILE_PREFIX, string extension = DEFAULT_FILE_EXTENSION)
        {
            RefreshFileSettingsFromStorage();

            sFilePrefix = prefix ?? DEFAULT_FILE_PREFIX;
            sFileExtension = NormalizeFileExtension(extension);

            if (!string.IsNullOrEmpty(sSavePath))
            {
                UseFileStorage(sSavePath, sFilePrefix, sFileExtension);
            }
        }

        /// <summary>
        /// 获取当前存档文件格式。
        /// </summary>
        /// <returns>当前文件名前缀和扩展名。</returns>
        public static (string prefix, string extension) GetFileFormat()
        {
            RefreshFileSettingsFromStorage();
            return (sFilePrefix, sFileExtension);
        }

        /// <summary>
        /// 以 1.x 回调风格异步保存指定槽位。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="data">需要保存的数据。</param>
        /// <param name="onComplete">保存完成回调，参数表示是否成功。</param>
        /// <param name="displayName">可选显示名称。</param>
        public static void SaveAsync(int slotId, SaveData data, Action<bool> onComplete = null, string displayName = null)
        {
            ValidateSlotId(slotId);
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                var saved = false;
                try
                {
                    saved = Save(slotId, data, displayName);
                }
                catch
                {
                    saved = false;
                }

                onComplete?.Invoke(saved);
            });
        }

        /// <summary>
        /// 以 1.x 回调风格异步读取指定槽位。
        /// </summary>
        /// <param name="slotId">保存槽位编号。</param>
        /// <param name="onComplete">读取完成回调，失败或不存在时传入空。</param>
        public static void LoadAsync(int slotId, Action<SaveData> onComplete)
        {
            ValidateSlotId(slotId);
            if (onComplete == null)
            {
                throw new ArgumentNullException(nameof(onComplete));
            }

            if (!Exists(slotId))
            {
                onComplete(null);
                return;
            }

            ThreadPool.QueueUserWorkItem(_ =>
            {
                SaveData data = null;
                try
                {
                    data = Load(slotId);
                }
                catch
                {
                    data = null;
                }

                onComplete(data);
            });
        }

        private static void ResetCompatibilitySettings()
        {
            sSavePath = null;
            sFilePrefix = DEFAULT_FILE_PREFIX;
            sFileExtension = DEFAULT_FILE_EXTENSION;
        }

        private static void UseFileStorage(string path, string prefix, string extension)
        {
            var storage = new FileSaveStorage(path, prefix, extension);
            sStorage = storage;
            sSavePath = storage.RootPath;
            sFilePrefix = storage.FilePrefix;
            sFileExtension = storage.FileExtension;
        }

        private static void RefreshFileSettingsFromStorage()
        {
            var storage = sStorage as FileSaveStorage;
            if (storage == null)
            {
                return;
            }

            sSavePath = storage.RootPath;
            sFilePrefix = storage.FilePrefix;
            sFileExtension = storage.FileExtension;
        }

        private static string NormalizeFileExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return DEFAULT_FILE_EXTENSION;
            }

            return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
        }

        private static string GetDefaultSavePath()
        {
            var basePath = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = Path.GetTempPath();
            }

            return Path.Combine(basePath, "YokiFrame", "Saves");
        }
    }
}
