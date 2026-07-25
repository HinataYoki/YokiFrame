#if GODOT
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// 从当前 Godot 项目的 ProjectSettings 加载 YokiFrame 运行时覆盖，不创建额外配置文件。
    /// </summary>
    internal static class GodotYokiFrameRuntimeSettingsLoader
    {
        private const string SETTING_ROOT = "yokiframe/runtime/";

        private static readonly SettingBinding[] sLogKitBindings =
        {
            new("log_kit/enabled", LogKitSettings.ENABLED_KEY),
            new("log_kit/minimum_level", LogKitSettings.MINIMUM_LEVEL_KEY),
            new("log_kit/save_log_in_player", LogKitSettings.SAVE_LOG_IN_PLAYER_KEY),
            new("log_kit/enable_imgui_in_player", LogKitSettings.ENABLE_IMGUI_IN_PLAYER_KEY),
            new("log_kit/enable_encryption", LogKitSettings.ENABLE_ENCRYPTION_KEY),
            new("log_kit/max_queue_size", LogKitSettings.MAX_QUEUE_SIZE_KEY),
            new("log_kit/max_same_log_count", LogKitSettings.MAX_SAME_LOG_COUNT_KEY),
            new("log_kit/max_retention_days", LogKitSettings.MAX_RETENTION_DAYS_KEY),
            new("log_kit/max_file_size_mb", LogKitSettings.MAX_FILE_SIZE_MB_KEY),
            new("log_kit/imgui_max_log_count", LogKitSettings.IMGUI_MAX_LOG_COUNT_KEY),
            new("log_kit/log_directory", LogKitSettings.LOG_DIRECTORY_KEY),
            new("log_kit/player_file_name", LogKitSettings.PLAYER_FILE_NAME_KEY)
        };

        private static readonly SettingBinding[] sSaveKitBindings =
        {
            new("save_kit/storage_path", "storagePath"),
            new("save_kit/file_extension", "fileExtension")
        };

        private static readonly SettingBinding[] sTableKitBindings =
        {
            new("table_kit/runtime_path_pattern", "runtimePathPattern"),
            new("table_kit/use_raw_resource_loading", "useRawResourceLoading")
        };

        /// <summary>
        /// 创建仅包含当前 Godot 项目覆盖值的 Store；未配置项继续使用 Kit 代码默认值。
        /// </summary>
        /// <returns>当前项目隔离的运行时设置 Store。</returns>
        internal static YokiFrameRuntimeSettingsStore Load()
        {
            YokiFrameRuntimeSettingsStore store = new();
            for (var index = 0; index < sLogKitBindings.Length; index++)
            {
                ApplyBinding(store, sLogKitBindings[index]);
            }

            for (var index = 0; index < sSaveKitBindings.Length; index++)
            {
                ApplyBinding(store, sSaveKitBindings[index], "SaveKit");
            }

            for (var index = 0; index < sTableKitBindings.Length; index++)
            {
                ApplyBinding(store, sTableKitBindings[index], "TableKit");
            }

            return store;
        }

        /// <summary>
        /// 把存在的 Godot ProjectSettings 值转换为 Core 使用的字符串标量。
        /// </summary>
        /// <param name="store">待填充的设置 Store。</param>
        /// <param name="binding">Godot path 与 Core Kit key 的绑定。</param>
        private static void ApplyBinding(YokiFrameRuntimeSettingsStore store, SettingBinding binding)
        {
            ApplyBinding(store, binding, LogKitSettings.KIT_NAME);
        }

        /// <summary>按指定 Kit 写入一项 Godot ProjectSettings 覆盖值。</summary>
        private static void ApplyBinding(YokiFrameRuntimeSettingsStore store, SettingBinding binding, string kitName)
        {
            string projectSettingPath = SETTING_ROOT + binding.ProjectPath;
            if (!ProjectSettings.HasSetting(projectSettingPath))
            {
                return;
            }

            Variant value = ProjectSettings.GetSetting(projectSettingPath);
            store.SetValue(kitName, binding.SettingKey, value.ToString());
        }

        /// <summary>
        /// 保存单个 Godot ProjectSettings path 与 Core Kit 设置键的稳定映射。
        /// </summary>
        private readonly struct SettingBinding
        {
            /// <summary>
            /// 创建一项设置映射；完整 Godot 路径由统一根前缀拼接。
            /// </summary>
            /// <param name="projectPath">相对 `yokiframe/runtime` 的路径。</param>
            /// <param name="settingKey">Core Kit 设置 key。</param>
            public SettingBinding(string projectPath, string settingKey)
            {
                ProjectPath = projectPath;
                SettingKey = settingKey;
            }

            /// <summary>获取 Godot ProjectSettings 相对路径。</summary>
            public string ProjectPath { get; }
            /// <summary>获取 Core Kit 设置 key。</summary>
            public string SettingKey { get; }
        }
    }
}
#endif
