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
        private const string LOG_KIT_PREFIX = "log_kit/";
        private const string SAVE_KIT_PREFIX = "save_kit/";
        private const string TABLE_KIT_PREFIX = "table_kit/";

        // Godot 路径只由共享转换规则生成，camelCase 键只在所属设置类型中声明一次。
        private static readonly SettingBinding[] sLogKitBindings =
        {
            Binding(LOG_KIT_PREFIX, LogKitSettings.ENABLED_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.MINIMUM_LEVEL_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.SAVE_LOG_IN_PLAYER_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.ENABLE_IMGUI_IN_PLAYER_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.ENABLE_ENCRYPTION_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.MAX_QUEUE_SIZE_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.MAX_SAME_LOG_COUNT_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.MAX_RETENTION_DAYS_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.MAX_FILE_SIZE_MB_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.IMGUI_MAX_LOG_COUNT_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.LOG_DIRECTORY_KEY),
            Binding(LOG_KIT_PREFIX, LogKitSettings.PLAYER_FILE_NAME_KEY)
        };

        private static readonly SettingBinding[] sSaveKitBindings =
        {
            Binding(SAVE_KIT_PREFIX, SaveKitSettings.STORAGE_PATH_KEY),
            Binding(SAVE_KIT_PREFIX, SaveKitSettings.FILE_EXTENSION_KEY)
        };

        private static readonly SettingBinding[] sTableKitBindings =
        {
            Binding(TABLE_KIT_PREFIX, TableKitSettings.RUNTIME_PATH_PATTERN_KEY),
            Binding(TABLE_KIT_PREFIX, TableKitSettings.USE_RAW_RESOURCE_LOADING_KEY)
        };

        /// <summary>
        /// 创建仅包含当前 Godot 项目覆盖值的 Store；未配置项继续使用 Kit 代码默认值。
        /// </summary>
        /// <returns>当前项目隔离的运行时设置 Store。</returns>
        internal static YokiFrameRuntimeSettingsStore Load()
        {
            YokiFrameRuntimeSettingsStore store = new YokiFrameRuntimeSettingsStore();
            ApplyBindings(store, sLogKitBindings, LogKitSettings.KIT_NAME);
            ApplyBindings(store, sSaveKitBindings, SaveKitSettings.KIT_NAME);
            ApplyBindings(store, sTableKitBindings, TableKitSettings.KIT_NAME);
            return store;
        }

        /// <summary>把一组 Kit 绑定写入 Store。路径不存在时保留 Kit 默认值。</summary>
        /// <param name="store">待填充的设置 Store。</param>
        /// <param name="bindings">当前 Kit 的 Godot 路径绑定。</param>
        /// <param name="kitName">KitSettings 使用的 Kit 名称。</param>
        private static void ApplyBindings(
            YokiFrameRuntimeSettingsStore store,
            SettingBinding[] bindings,
            string kitName)
        {
            for (var index = 0; index < bindings.Length; index++)
            {
                ApplyBinding(store, bindings[index], kitName);
            }
        }

        /// <summary>按指定 Kit 写入一项 Godot ProjectSettings 覆盖值。</summary>
        /// <param name="store">待填充的设置 Store。</param>
        /// <param name="binding">Godot path 与 Kit key 的绑定。</param>
        /// <param name="kitName">KitSettings 使用的 Kit 名称。</param>
        private static void ApplyBinding(
            YokiFrameRuntimeSettingsStore store,
            SettingBinding binding,
            string kitName)
        {
            string projectSettingPath = SETTING_ROOT + binding.ProjectPath;
            if (!ProjectSettings.HasSetting(projectSettingPath))
            {
                return;
            }

            Variant value = ProjectSettings.GetSetting(projectSettingPath);
            store.SetValue(kitName, binding.SettingKey, value.ToString());
        }

        /// <summary>用唯一转换规则把 camelCase Kit 键绑定到 Godot section 相对路径。</summary>
        /// <param name="sectionPrefix">已包含结尾斜杠的 Godot section 前缀。</param>
        /// <param name="settingKey">所属 Kit 声明的 camelCase 键。</param>
        /// <returns>可直接交给加载器的绑定。</returns>
        private static SettingBinding Binding(string sectionPrefix, string settingKey)
        {
            return new SettingBinding(sectionPrefix + GodotRuntimeSettingKeys.ToSnakeCase(settingKey), settingKey);
        }

        /// <summary>保存单个 Godot ProjectSettings path 与 Kit 设置键的稳定映射。</summary>
        private readonly struct SettingBinding
        {
            /// <summary>
            /// 创建一项设置映射；完整 Godot 路径由统一根前缀拼接。
            /// </summary>
            /// <param name="projectPath">相对 yokiframe/runtime 的路径。</param>
            /// <param name="settingKey">Kit 设置 key。</param>
            public SettingBinding(string projectPath, string settingKey)
            {
                ProjectPath = projectPath;
                SettingKey = settingKey;
            }

            /// <summary>获取 Godot ProjectSettings 相对路径。</summary>
            public string ProjectPath { get; }

            /// <summary>获取 Kit 设置 key。</summary>
            public string SettingKey { get; }
        }
    }
}
#endif
