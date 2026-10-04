namespace YokiFrame
{
    /// <summary>
    /// TableKit Runtime Settings 键的唯一声明。
    /// 放在 Core 是因为 Godot Player 必须在 TableKit 生成代码加载前就知道这些键。
    /// </summary>
    public static class TableKitSettings
    {
        /// <summary>TableKit 在通用设置存储中的 Kit 名称。</summary>
        public const string KIT_NAME = "TableKit";

        /// <summary>传给 Loader 的资源路径模板配置 key。</summary>
        public const string RUNTIME_PATH_PATTERN_KEY = "runtimePathPattern";

        /// <summary>是否通过原始资源能力读取表数据的配置 key。</summary>
        public const string USE_RAW_RESOURCE_LOADING_KEY = "useRawResourceLoading";

        /// <summary>已废弃的异步加载配置 key。保存时仍列入替换范围，用于清掉旧项目残留。</summary>
        public const string LEGACY_USE_ASYNC_LOADING_KEY = "useAsyncLoading";

        /// <summary>已废弃的资源根配置 key。保存时仍列入替换范围，用于清掉旧项目残留。</summary>
        public const string LEGACY_RESOURCE_ROOT_KEY = "resourceRoot";

        /// <summary>已废弃的数据扩展名配置 key。保存时仍列入替换范围，用于清掉旧项目残留。</summary>
        public const string LEGACY_DATA_EXTENSION_KEY = "dataExtension";
    }
}
