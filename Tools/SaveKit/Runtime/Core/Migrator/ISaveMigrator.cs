namespace YokiFrame
{
    /// <summary>
    /// 存档数据迁移器接口
    /// 处理不同版本存档数据的升级
    /// </summary>
    public interface ISaveMigrator
    {
        /// <summary>
        /// 源版本号
        /// </summary>
        int FromVersion { get; }

        /// <summary>
        /// 目标版本号
        /// </summary>
        int ToVersion { get; }

        /// <summary>
        /// 执行数据迁移（操作整个 SaveData）
        /// </summary>
        /// <param name="oldData">旧版本数据</param>
        /// <returns>迁移后的数据</returns>
        SaveData Migrate(SaveData oldData);
    }

    /// <summary>
    /// 原始字节迁移器接口
    /// 直接操作原始字节数组，适用于任何序列化格式（JSON、Nino、MessagePack等）
    /// 迁移器自己负责反序列化旧格式和序列化新格式
    /// </summary>
    public interface IRawByteMigrator : ISaveMigrator
    {
        /// <summary>
        /// 迁移原始字节数据
        /// </summary>
        /// <param name="oldTypeKey">旧类型哈希 key</param>
        /// <param name="rawBytes">原始字节数组</param>
        /// <param name="newTypeKey">输出：新类型哈希 key（如果类型改变）</param>
        /// <returns>迁移后的字节数组，返回 null 表示不处理此类型（保持原样）</returns>
        byte[] MigrateBytes(int oldTypeKey, byte[] rawBytes, out int newTypeKey);
    }
}
