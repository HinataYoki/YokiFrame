namespace YokiFrame
{
    /// <summary>
    /// JSON 模块 payload 的单步迁移器。迁移器只接触 JSON UTF-8 字节，不依赖引擎对象。
    /// </summary>
    public interface IJsonSaveMigrator
    {
        /// <summary>获取迁移器对应的稳定模块 ID。</summary>
        string ModuleId { get; }

        /// <summary>获取源 schema 版本。</summary>
        int FromVersion { get; }

        /// <summary>获取目标 schema 版本。</summary>
        int ToVersion { get; }

        /// <summary>将 JSON UTF-8 payload 迁移到下一版本。</summary>
        /// <param name="jsonUtf8">源 JSON UTF-8 字节。</param>
        /// <returns>目标 JSON UTF-8 字节。</returns>
        byte[] Migrate(byte[] jsonUtf8);
    }
}
