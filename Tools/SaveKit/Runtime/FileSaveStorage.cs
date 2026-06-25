using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 基于本地文件系统的保存槽位存储后端。
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private const string DEFAULT_FILE_PREFIX = "save_";
        private const string DEFAULT_FILE_EXTENSION = ".yoki";

        private readonly string rootPath;
        private readonly string filePrefix;
        private readonly string fileExtension;

        /// <summary>
        /// 使用默认文件名前缀和扩展名创建文件存储后端。
        /// </summary>
        /// <param name="rootPath">保存文件根目录。</param>
        public FileSaveStorage(string rootPath)
            : this(rootPath, DEFAULT_FILE_PREFIX, DEFAULT_FILE_EXTENSION)
        {
        }

        /// <summary>
        /// 使用自定义文件名前缀和扩展名创建文件存储后端。
        /// </summary>
        /// <param name="rootPath">保存文件根目录。</param>
        /// <param name="filePrefix">保存文件名前缀。</param>
        /// <param name="fileExtension">保存文件扩展名。</param>
        public FileSaveStorage(string rootPath, string filePrefix, string fileExtension)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                throw new ArgumentException("Root path cannot be null or empty.", nameof(rootPath));
            }

            this.rootPath = Path.GetFullPath(rootPath);
            this.filePrefix = filePrefix ?? string.Empty;
            this.fileExtension = NormalizeExtension(fileExtension);
            Directory.CreateDirectory(this.rootPath);
        }

        /// <summary>
        /// 保存文件根目录。
        /// </summary>
        public string RootPath => rootPath;

        /// <summary>
        /// 保存文件名前缀。
        /// </summary>
        public string FilePrefix => filePrefix;

        /// <summary>
        /// 保存文件扩展名。
        /// </summary>
        public string FileExtension => fileExtension;

        /// <inheritdoc />
        public bool Exists(int slotId)
        {
            return File.Exists(GetSlotPath(slotId));
        }

        /// <inheritdoc />
        public void Write(int slotId, byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            Directory.CreateDirectory(rootPath);
            var targetPath = GetSlotPath(slotId);
            var tempPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";

            try
            {
                using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

                if (File.Exists(targetPath))
                {
                    File.Replace(tempPath, targetPath, null);
                }
                else
                {
                    File.Move(tempPath, targetPath);
                }
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }
        }

        /// <inheritdoc />
        public byte[] Read(int slotId)
        {
            var path = GetSlotPath(slotId);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        /// <inheritdoc />
        public bool Delete(int slotId)
        {
            var path = GetSlotPath(slotId);
            if (!File.Exists(path))
            {
                return false;
            }

            File.Delete(path);
            return true;
        }

        /// <inheritdoc />
        public IReadOnlyList<int> GetSlotIds()
        {
            var ids = new List<int>();
            if (!Directory.Exists(rootPath))
            {
                return ids;
            }

            var searchPattern = filePrefix + "*" + fileExtension;
            var files = Directory.GetFiles(rootPath, searchPattern, SearchOption.TopDirectoryOnly);
            for (var i = 0; i < files.Length; i++)
            {
                int slotId;
                if (TryParseSlotId(files[i], out slotId))
                {
                    ids.Add(slotId);
                }
            }

            ids.Sort();
            return ids;
        }

        /// <inheritdoc />
        public void Clear()
        {
            if (!Directory.Exists(rootPath))
            {
                return;
            }

            var ids = GetSlotIds();
            for (var i = ids.Count - 1; i >= 0; i--)
            {
                Delete(ids[i]);
            }
        }

        private string GetSlotPath(int slotId)
        {
            return Path.Combine(rootPath, filePrefix + slotId + fileExtension);
        }

        private bool TryParseSlotId(string filePath, out int slotId)
        {
            slotId = 0;
            var fileName = Path.GetFileName(filePath);
            if (!fileName.StartsWith(filePrefix, StringComparison.Ordinal) ||
                !fileName.EndsWith(fileExtension, StringComparison.Ordinal))
            {
                return false;
            }

            var start = filePrefix.Length;
            var length = fileName.Length - filePrefix.Length - fileExtension.Length;
            if (length <= 0)
            {
                return false;
            }

            var slotText = fileName.Substring(start, length);
            return int.TryParse(slotText, out slotId);
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrEmpty(extension))
            {
                return DEFAULT_FILE_EXTENSION;
            }

            return extension.StartsWith(".", StringComparison.Ordinal) ? extension : "." + extension;
        }
    }
}
