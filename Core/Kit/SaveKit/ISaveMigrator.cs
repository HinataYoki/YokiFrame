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
        /// 执行数据迁移
        /// </summary>
        /// <param name="oldData">旧版本数据</param>
        /// <returns>迁移后的数据</returns>
        SaveData Migrate(SaveData oldData);
    }
}
