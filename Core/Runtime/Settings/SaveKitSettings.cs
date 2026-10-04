namespace YokiFrame
{
    /// <summary>
    /// SaveKit Runtime Settings 键的唯一声明。
    /// 放在 Core 是因为 Godot Player 加载设置时不能依赖 Tools 程序集是否已经加载。
    /// </summary>
    public static class SaveKitSettings
    {
        /// <summary>SaveKit 在通用设置存储中的 Kit 名称。</summary>
        public const string KIT_NAME = "SaveKit";

        /// <summary>存档根目录配置 key；可包含宿主运行时路径变量。</summary>
        public const string STORAGE_PATH_KEY = "storagePath";

        /// <summary>存档文件扩展名配置 key。</summary>
        public const string FILE_EXTENSION_KEY = "fileExtension";

        /// <summary>未配置扩展名时使用的默认值。</summary>
        public const string DEFAULT_FILE_EXTENSION = ".yoki";
    }
}
