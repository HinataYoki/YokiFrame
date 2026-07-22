using System;
using System.Collections.Generic;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 基于本地文件系统的槽位和 Global 文档存储后端。
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage, ISaveMetadataStorage
    {
        private const string DEFAULT_EXTENSION = ".yoki";
        private const int MAX_EXTENSION_LENGTH = 32;
        private const string INVALID_EXTENSION_CHARACTERS = "<>:\"/\\|?*";
        private readonly object mSyncRoot = new();
        private readonly string mRootPath;
        private readonly string mFileExtension;

        /// <summary>使用默认 `.yoki` 扩展名创建文件后端。</summary>
        /// <param name="rootPath">存档根目录。</param>
        public FileSaveStorage(string rootPath)
            : this(rootPath, DEFAULT_EXTENSION)
        {
        }

        /// <summary>使用自定义扩展名创建文件后端。</summary>
        /// <param name="rootPath">存档根目录。</param>
        /// <param name="fileExtension">文件扩展名。</param>
        public FileSaveStorage(string rootPath, string fileExtension)
        {
            if (string.IsNullOrEmpty(rootPath))
            {
                throw new ArgumentException("Root path cannot be empty.", nameof(rootPath));
            }

            mRootPath = Path.GetFullPath(rootPath);
            mFileExtension = NormalizeExtension(fileExtension);
            Directory.CreateDirectory(mRootPath);
        }

        /// <summary>获取规范化后的存档根目录。</summary>
        public string RootPath
        {
            get { return mRootPath; }
        }

        /// <summary>获取文件扩展名。</summary>
        public string FileExtension
        {
            get { return mFileExtension; }
        }

        /// <inheritdoc />
        public bool Exists(SaveTarget target)
        {
            lock (mSyncRoot)
            {
                return File.Exists(GetPath(target));
            }
        }

        /// <inheritdoc />
        public void Write(SaveTarget target, byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            lock (mSyncRoot)
            {
                var targetPath = GetPath(target);
                Directory.CreateDirectory(Path.GetDirectoryName(targetPath));
                var temporaryPath = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
                try
                {
                    using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        stream.Write(bytes, 0, bytes.Length);
                        stream.Flush(true);
                    }

                    if (File.Exists(targetPath))
                    {
                        File.Replace(temporaryPath, targetPath, null);
                    }
                    else
                    {
                        File.Move(temporaryPath, targetPath);
                    }
                }
                finally
                {
                    if (File.Exists(temporaryPath))
                    {
                        File.Delete(temporaryPath);
                    }
                }
            }
        }

        /// <inheritdoc />
        public byte[] Read(SaveTarget target)
        {
            lock (mSyncRoot)
            {
                var path = GetPath(target);
                return File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
        }

        /// <inheritdoc />
        public bool TryReadMetadata(SaveTarget target, out SaveMeta meta)
        {
            lock (mSyncRoot)
            {
                meta = default(SaveMeta);
                var path = GetPath(target);
                if (!File.Exists(path))
                {
                    return false;
                }

                try
                {
                    using (var stream = new FileStream(
                               path,
                               FileMode.Open,
                               FileAccess.Read,
                               FileShare.ReadWrite | FileShare.Delete))
                    {
                        if (!SaveMeta.TryDeserializeHeader(stream, stream.Length, out meta, out _, out _)
                            || meta.Target != target)
                        {
                            meta = default(SaveMeta);
                            return false;
                        }

                        return true;
                    }
                }
                catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
                {
                    meta = default(SaveMeta);
                    return false;
                }
            }
        }

        /// <inheritdoc />
        public bool Delete(SaveTarget target)
        {
            lock (mSyncRoot)
            {
                var path = GetPath(target);
                if (!File.Exists(path))
                {
                    return false;
                }

                File.Delete(path);
                return true;
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<SaveTarget> GetTargets(SaveTargetKind kind)
        {
            lock (mSyncRoot)
            {
                var targets = new List<SaveTarget>();
                var directory = GetDirectory(kind);
                if (!Directory.Exists(directory))
                {
                    return targets;
                }

                var files = Directory.GetFiles(directory, "*" + mFileExtension, SearchOption.TopDirectoryOnly);
                for (var i = 0; i < files.Length; i++)
                {
                    if (TryParseTarget(files[i], kind, out var target))
                    {
                        targets.Add(target);
                    }
                }

                targets.Sort(CompareTargets);
                return targets;
            }
        }

        /// <inheritdoc />
        public void Clear(SaveTargetKind kind)
        {
            lock (mSyncRoot)
            {
                var targets = GetTargets(kind);
                for (var i = 0; i < targets.Count; i++)
                {
                    Delete(targets[i]);
                }
            }
        }

        /// <summary>获取目标文件绝对路径。</summary>
        private string GetPath(SaveTarget target)
        {
            var directory = GetDirectory(target.Kind);
            var fileName = target.IsSlot ? "save_" + target.SlotId : target.GlobalKey;
            return Path.Combine(directory, fileName + mFileExtension);
        }

        /// <summary>获取目标类型目录。</summary>
        private string GetDirectory(SaveTargetKind kind)
        {
            return Path.Combine(mRootPath, kind == SaveTargetKind.Slot ? "slots" : "global");
        }

        /// <summary>从文件名恢复目标，并再次通过 SaveTarget 校验。</summary>
        private bool TryParseTarget(string path, SaveTargetKind kind, out SaveTarget target)
        {
            target = default(SaveTarget);
            var fullFileName = Path.GetFileName(path);
            if (!fullFileName.EndsWith(mFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var fileName = fullFileName.Substring(0, fullFileName.Length - mFileExtension.Length);
            try
            {
                if (kind == SaveTargetKind.Slot && fileName.StartsWith("save_", StringComparison.Ordinal) &&
                    int.TryParse(fileName.Substring(5), out var slotId))
                {
                    target = SaveTarget.Slot(slotId);
                    return true;
                }

                if (kind == SaveTargetKind.Global)
                {
                    target = SaveTarget.Global(fileName);
                    return true;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }

            return false;
        }

        /// <summary>按目标名称排序。</summary>
        private static int CompareTargets(SaveTarget left, SaveTarget right)
        {
            return string.CompareOrdinal(left.Name, right.Name);
        }

        /// <summary>规范化并验证扩展名，避免配置值改变目录结构或文件搜索语义。</summary>
        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return DEFAULT_EXTENSION;
            }

            var normalized = extension.Trim();
            if (!normalized.StartsWith(".", StringComparison.Ordinal))
            {
                normalized = "." + normalized;
            }

            if (normalized.Length < 2 || normalized.Length > MAX_EXTENSION_LENGTH ||
                normalized[normalized.Length - 1] == '.')
            {
                throw new ArgumentException("File extension must contain 1 to 31 valid characters.", nameof(extension));
            }

            for (var index = 1; index < normalized.Length; index++)
            {
                var character = normalized[index];
                if (char.IsControl(character) || char.IsWhiteSpace(character) ||
                    INVALID_EXTENSION_CHARACTERS.IndexOf(character) >= 0)
                {
                    throw new ArgumentException("File extension contains an unsupported character.", nameof(extension));
                }
            }

            return normalized;
        }
    }
}
