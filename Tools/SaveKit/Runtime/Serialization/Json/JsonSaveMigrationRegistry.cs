using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// JSON payload 迁移器注册表。缺少中间版本迁移器时显式失败，不会静默跳过。
    /// </summary>
    public sealed class JsonSaveMigrationRegistry
    {
        private readonly Dictionary<string, Dictionary<int, IJsonSaveMigrator>> mMigrators =
            new(StringComparer.Ordinal);

        /// <summary>注册一个相邻版本迁移器。</summary>
        /// <param name="migrator">迁移器。</param>
        public void Register(IJsonSaveMigrator migrator)
        {
            if (migrator == null)
            {
                throw new ArgumentNullException(nameof(migrator));
            }

            if (migrator.FromVersion < 0 || migrator.ToVersion != migrator.FromVersion + 1)
            {
                throw new ArgumentException("JSON migrators must advance exactly one version.", nameof(migrator));
            }

            SaveModuleIdentity.ValidateId(migrator.ModuleId);
            if (!mMigrators.TryGetValue(migrator.ModuleId, out var versionMigrators))
            {
                versionMigrators = new Dictionary<int, IJsonSaveMigrator>();
                mMigrators.Add(migrator.ModuleId, versionMigrators);
            }

            versionMigrators[migrator.FromVersion] = migrator;
        }

        /// <summary>迁移一个模块 JSON payload。</summary>
        /// <param name="moduleId">模块 ID。</param>
        /// <param name="fromVersion">源 schema 版本。</param>
        /// <param name="toVersion">目标 schema 版本。</param>
        /// <param name="jsonUtf8">源 JSON 字节。</param>
        /// <returns>迁移后的 JSON 字节。</returns>
        public byte[] Migrate(string moduleId, int fromVersion, int toVersion, byte[] jsonUtf8)
        {
            SaveModuleIdentity.ValidateId(moduleId);
            if (fromVersion > toVersion)
            {
                throw new ArgumentException("JSON schema cannot migrate backwards.", nameof(toVersion));
            }

            var current = CopyBytes(jsonUtf8);
            for (var version = fromVersion; version < toVersion; version++)
            {
                if (!mMigrators.TryGetValue(moduleId, out var versionMigrators)
                    || !versionMigrators.TryGetValue(version, out var migrator))
                {
                    throw new InvalidOperationException("Missing JSON migrator for " + moduleId + " from version " + version + ".");
                }

                current = migrator.Migrate(current);
                if (current == null)
                {
                    throw new InvalidOperationException("JSON migrator returned null for " + moduleId + ".");
                }
            }

            return current;
        }

        /// <summary>复制 payload，保证迁移器不能修改调用方缓存。</summary>
        private static byte[] CopyBytes(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var copy = new byte[bytes.Length];
            Buffer.BlockCopy(bytes, 0, copy, 0, bytes.Length);
            return copy;
        }
    }
}
