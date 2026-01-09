using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 存档系统静态入口类
    /// 提供存档的读写、删除、管理等操作
    /// </summary>
    public static partial class SaveKit
    {
        #region 配置字段

        /// <summary>
        /// 序列化器，负责将数据对象转换为字节数组
        /// </summary>
        private static ISaveSerializer sSerializer = new JsonSaveSerializer();

        /// <summary>
        /// 加密器，负责对存档数据进行加密/解密，为 null 时不加密
        /// </summary>
        private static ISaveEncryptor sEncryptor;

        /// <summary>
        /// 存档文件保存路径
        /// </summary>
        private static string sSavePath;

        /// <summary>
        /// 当前数据版本号，用于版本迁移判断
        /// </summary>
        private static int sCurrentVersion = 1;

        /// <summary>
        /// 最大存档槽位数量
        /// </summary>
        private static int sMaxSlots = 10;

        /// <summary>
        /// 版本迁移器字典，key 为 fromVersion * 10000 + toVersion
        /// </summary>
        private static readonly Dictionary<int, ISaveMigrator> sMigrators = new();

        /// <summary>
        /// 存档文件前缀
        /// </summary>
        private static string sFilePrefix = "save_";

        /// <summary>
        /// 存档文件后缀
        /// </summary>
        private static string sFileExtension = ".yoki";

        #endregion

        #region 配置方法

        /// <summary>
        /// 设置序列化器
        /// </summary>
        public static void SetSerializer(ISaveSerializer serializer)
        {
            sSerializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            KitLogger.Log($"[SaveKit] 序列化器已切换为: {serializer.GetType().Name}");
        }

        /// <summary>
        /// 获取当前序列化器
        /// </summary>
        public static ISaveSerializer GetSerializer() => sSerializer;

        /// <summary>
        /// 设置加密器（设置为 null 则禁用加密）
        /// </summary>
        public static void SetEncryptor(ISaveEncryptor encryptor)
        {
            sEncryptor = encryptor;
            KitLogger.Log(encryptor != null
                ? $"[SaveKit] 加密器已切换为: {encryptor.GetType().Name}"
                : "[SaveKit] 加密已禁用");
        }

        /// <summary>
        /// 获取当前加密器
        /// </summary>
        public static ISaveEncryptor GetEncryptor() => sEncryptor;

        /// <summary>
        /// 设置存档路径
        /// </summary>
        public static void SetSavePath(string path)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            sSavePath = path;
            EnsureDirectoryExists(sSavePath);
            KitLogger.Log($"[SaveKit] 存档路径已设置为: {path}");
        }

        /// <summary>
        /// 获取存档路径
        /// </summary>
        public static string GetSavePath()
        {
            if (string.IsNullOrEmpty(sSavePath))
            {
                sSavePath = Path.Combine(Application.persistentDataPath, "Saves");
                EnsureDirectoryExists(sSavePath);
            }
            return sSavePath;
        }

        /// <summary>
        /// 设置当前数据版本
        /// </summary>
        public static void SetCurrentVersion(int version)
        {
            if (version < 1)
                throw new ArgumentOutOfRangeException(nameof(version), "Version must be >= 1");
            sCurrentVersion = version;
        }

        /// <summary>
        /// 获取当前数据版本
        /// </summary>
        public static int GetCurrentVersion() => sCurrentVersion;

        /// <summary>
        /// 设置最大槽位数
        /// </summary>
        public static void SetMaxSlots(int maxSlots)
        {
            if (maxSlots < 1)
                throw new ArgumentOutOfRangeException(nameof(maxSlots), "MaxSlots must be >= 1");
            sMaxSlots = maxSlots;
        }

        /// <summary>
        /// 获取最大槽位数
        /// </summary>
        public static int GetMaxSlots() => sMaxSlots;

        /// <summary>
        /// 设置存档文件格式（前缀和后缀）
        /// </summary>
        /// <param name="prefix">文件名前缀，默认 "save_"</param>
        /// <param name="extension">文件后缀，默认 ".yoki"</param>
        public static void SetFileFormat(string prefix = "save_", string extension = ".yoki")
        {
            sFilePrefix = prefix ?? "save_";
            sFileExtension = string.IsNullOrEmpty(extension) ? ".yoki" : extension;
            
            // 确保后缀以点开头
            if (!sFileExtension.StartsWith("."))
                sFileExtension = "." + sFileExtension;
                
            KitLogger.Log($"[SaveKit] 文件格式已设置: {sFilePrefix}{{slotId}}{sFileExtension}");
        }

        /// <summary>
        /// 获取当前文件格式配置
        /// </summary>
        public static (string prefix, string extension) GetFileFormat()
            => (sFilePrefix, sFileExtension);

        /// <summary>
        /// 注册版本迁移器
        /// </summary>
        public static void RegisterMigrator(ISaveMigrator migrator)
        {
            if (migrator == null)
                throw new ArgumentNullException(nameof(migrator));

            var key = GetMigratorKey(migrator.FromVersion, migrator.ToVersion);
            sMigrators[key] = migrator;
            KitLogger.Log($"[SaveKit] 已注册迁移器: v{migrator.FromVersion} -> v{migrator.ToVersion}");
        }

        #endregion

        #region 重置（测试用）

        /// <summary>
        /// 重置所有配置（仅用于测试）
        /// </summary>
        public static void Reset()
        {
            DisableAutoSave();
            sSerializer = new JsonSaveSerializer();
            sEncryptor = null;
            sSavePath = null;
            sCurrentVersion = 1;
            sMaxSlots = 10;
            sMigrators.Clear();
            sFilePrefix = "save_";
            sFileExtension = ".yoki";
        }

        #endregion
    }
}
