namespace YokiFrame
{
    /// <summary>
    /// 保存数据版本迁移器接口。
    /// </summary>
    public interface ISaveMigrator
    {
        /// <summary>
        /// 源版本号。
        /// </summary>
        int FromVersion { get; }

        /// <summary>
        /// 目标版本号。
        /// </summary>
        int ToVersion { get; }

        /// <summary>
        /// 将旧版本保存数据迁移到目标版本。
        /// </summary>
        /// <param name="oldData">旧版本保存数据。</param>
        /// <returns>迁移后的保存数据。</returns>
        SaveData Migrate(SaveData oldData);
    }

    /// <summary>
    /// 原始字节级保存数据迁移器接口。
    /// </summary>
    public interface IRawByteMigrator : ISaveMigrator
    {
        /// <summary>
        /// 迁移某个模块的原始字节数据。
        /// </summary>
        /// <param name="oldTypeKey">旧模块类型键。</param>
        /// <param name="rawBytes">旧模块原始字节。</param>
        /// <param name="newTypeKey">迁移后的模块类型键。</param>
        /// <returns>迁移后的模块字节。</returns>
        byte[] MigrateBytes(int oldTypeKey, byte[] rawBytes, out int newTypeKey);
    }
}
