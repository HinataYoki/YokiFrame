using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// LogKit 的跨宿主运行时设置定义。Unity/Godot/Tauri 只通过这些 key 交换配置。
    /// </summary>
    public static class LogKitSettings
    {
        /// <summary>
        /// LogKit 在通用设置存储中的 Kit 名称。
        /// </summary>
        public const string KIT_NAME = "LogKit";

        /// <summary>
        /// Unity Resources 中运行时设置资源路径。
        /// </summary>
        public const string RUNTIME_SETTINGS_RESOURCE_PATH = "YokiFrameRuntimeSettings";

        /// <summary>
        /// Unity 项目内运行时设置资源路径。
        /// </summary>
        public const string RUNTIME_SETTINGS_ASSET_PATH = "Assets/Settings/Resources/YokiFrameRuntimeSettings.asset";

        /// <summary>
        /// 日志总开关配置 key。
        /// </summary>
        public const string ENABLED_KEY = "enabled";

        /// <summary>
        /// 最小日志等级配置 key。
        /// </summary>
        public const string MINIMUM_LEVEL_KEY = "minimumLevel";

        /// <summary>
        /// 编辑器日志文件保存配置 key。
        /// </summary>
        public const string SAVE_LOG_IN_EDITOR_KEY = "saveLogInEditor";

        /// <summary>
        /// Player 日志文件保存配置 key。
        /// </summary>
        public const string SAVE_LOG_IN_PLAYER_KEY = "saveLogInPlayer";

        /// <summary>
        /// Player IMGUI 日志面板配置 key。
        /// </summary>
        public const string ENABLE_IMGUI_IN_PLAYER_KEY = "enableIMGUIInPlayer";

        /// <summary>
        /// 日志文件加密配置 key。
        /// </summary>
        public const string ENABLE_ENCRYPTION_KEY = "enableEncryption";

        /// <summary>
        /// 日志文件写入队列容量配置 key。
        /// </summary>
        public const string MAX_QUEUE_SIZE_KEY = "maxQueueSize";

        /// <summary>
        /// 相同日志连续重复上限配置 key。
        /// </summary>
        public const string MAX_SAME_LOG_COUNT_KEY = "maxSameLogCount";

        /// <summary>
        /// 日志文件保留天数配置 key。
        /// </summary>
        public const string MAX_RETENTION_DAYS_KEY = "maxRetentionDays";

        /// <summary>
        /// 单个日志文件大小上限配置 key。
        /// </summary>
        public const string MAX_FILE_SIZE_MB_KEY = "maxFileSizeMB";

        /// <summary>
        /// IMGUI 日志面板最大显示条数配置 key。
        /// </summary>
        public const string IMGUI_MAX_LOG_COUNT_KEY = "imguiMaxLogCount";

        /// <summary>
        /// 日志目录配置 key。
        /// </summary>
        public const string LOG_DIRECTORY_KEY = "logDirectory";

        /// <summary>
        /// 编辑器日志文件名配置 key。
        /// </summary>
        public const string EDITOR_FILE_NAME_KEY = "editorFileName";

        /// <summary>
        /// Player 日志文件名配置 key。
        /// </summary>
        public const string PLAYER_FILE_NAME_KEY = "playerFileName";

        /// <summary>
        /// 默认是否启用日志。
        /// </summary>
        public const bool DEFAULT_ENABLED = true;

        /// <summary>
        /// 默认最小日志等级。
        /// </summary>
        public const LogLevel DEFAULT_MINIMUM_LEVEL = LogLevel.Debug;

        /// <summary>
        /// 默认是否在编辑器保存日志文件。
        /// </summary>
        public const bool DEFAULT_SAVE_LOG_IN_EDITOR = false;

        /// <summary>
        /// 默认是否在 Player 保存日志文件。
        /// </summary>
        public const bool DEFAULT_SAVE_LOG_IN_PLAYER = true;

        /// <summary>
        /// 默认是否在 Player 启用 IMGUI 日志面板。
        /// </summary>
        public const bool DEFAULT_ENABLE_IMGUI_IN_PLAYER = false;

        /// <summary>
        /// 默认是否启用日志文件加密。
        /// </summary>
        public const bool DEFAULT_ENABLE_ENCRYPTION = true;

        /// <summary>
        /// 默认日志文件写入队列容量。
        /// </summary>
        public const int DEFAULT_MAX_QUEUE_SIZE = 20000;

        /// <summary>
        /// 默认相同日志连续重复上限。
        /// </summary>
        public const int DEFAULT_MAX_SAME_LOG_COUNT = 50;

        /// <summary>
        /// 默认日志文件保留天数。
        /// </summary>
        public const int DEFAULT_MAX_RETENTION_DAYS = 15;

        /// <summary>
        /// 默认单个日志文件大小上限，单位 MB。
        /// </summary>
        public const int DEFAULT_MAX_FILE_SIZE_MB = 100;

        /// <summary>
        /// 默认 IMGUI 日志面板最大显示条数。
        /// </summary>
        public const int DEFAULT_IMGUI_MAX_LOG_COUNT = 200;

        /// <summary>
        /// 默认日志目录，空字符串表示由 Adapter 选择持久化目录。
        /// </summary>
        public const string DEFAULT_LOG_DIRECTORY = "";

        /// <summary>
        /// 默认编辑器日志文件名。
        /// </summary>
        public const string DEFAULT_EDITOR_FILE_NAME = "yoki_editor.log";

        /// <summary>
        /// 默认 Player 日志文件名。
        /// </summary>
        public const string DEFAULT_PLAYER_FILE_NAME = "yoki_player.log";

        /// <summary>
        /// 获取当前日志总开关。
        /// </summary>
        public static bool Enabled
        {
            get { return KitSettings.GetBool(KIT_NAME, ENABLED_KEY, DEFAULT_ENABLED); }
        }

        /// <summary>
        /// 获取当前最小日志等级。
        /// </summary>
        public static LogLevel MinimumLevel
        {
            get
            {
                var value = KitSettings.GetString(KIT_NAME, MINIMUM_LEVEL_KEY, DEFAULT_MINIMUM_LEVEL.ToString());
                LogLevel parsed;
                return Enum.TryParse(value, true, out parsed) ? NormalizeLevel(parsed) : DEFAULT_MINIMUM_LEVEL;
            }
        }

        /// <summary>
        /// 构建 LogKit 设置 JSON。
        /// </summary>
        /// <returns>当前设置 JSON。</returns>
        public static string BuildJson()
        {
            var sb = new StringBuilder(320);
            AppendJson(sb);
            return sb.ToString();
        }

        /// <summary>
        /// 将当前 LogKit 设置追加为 JSON 对象。
        /// </summary>
        /// <param name="sb">用于接收 JSON 的字符串构建器。</param>
        public static void AppendJson(StringBuilder sb)
        {
            sb.Append('{');
            sb.Append('"');
            sb.Append(ENABLED_KEY);
            sb.Append("\":");
            sb.Append(Enabled ? "true" : "false");
            sb.Append(",\"");
            sb.Append(MINIMUM_LEVEL_KEY);
            sb.Append("\":\"");
            sb.Append(JsonHelper.EscapeString(MinimumLevel.ToString()));
            sb.Append('"');
            AppendBool(sb, SAVE_LOG_IN_EDITOR_KEY, GetBool(SAVE_LOG_IN_EDITOR_KEY, DEFAULT_SAVE_LOG_IN_EDITOR));
            AppendBool(sb, SAVE_LOG_IN_PLAYER_KEY, GetBool(SAVE_LOG_IN_PLAYER_KEY, DEFAULT_SAVE_LOG_IN_PLAYER));
            AppendBool(sb, ENABLE_IMGUI_IN_PLAYER_KEY, GetBool(ENABLE_IMGUI_IN_PLAYER_KEY, DEFAULT_ENABLE_IMGUI_IN_PLAYER));
            AppendBool(sb, ENABLE_ENCRYPTION_KEY, GetBool(ENABLE_ENCRYPTION_KEY, DEFAULT_ENABLE_ENCRYPTION));
            AppendInt(sb, MAX_QUEUE_SIZE_KEY, GetInt(MAX_QUEUE_SIZE_KEY, DEFAULT_MAX_QUEUE_SIZE));
            AppendInt(sb, MAX_SAME_LOG_COUNT_KEY, GetInt(MAX_SAME_LOG_COUNT_KEY, DEFAULT_MAX_SAME_LOG_COUNT));
            AppendInt(sb, MAX_RETENTION_DAYS_KEY, GetInt(MAX_RETENTION_DAYS_KEY, DEFAULT_MAX_RETENTION_DAYS));
            AppendInt(sb, MAX_FILE_SIZE_MB_KEY, GetInt(MAX_FILE_SIZE_MB_KEY, DEFAULT_MAX_FILE_SIZE_MB));
            AppendInt(sb, IMGUI_MAX_LOG_COUNT_KEY, GetInt(IMGUI_MAX_LOG_COUNT_KEY, DEFAULT_IMGUI_MAX_LOG_COUNT));
            AppendString(sb, LOG_DIRECTORY_KEY, GetString(LOG_DIRECTORY_KEY, DEFAULT_LOG_DIRECTORY));
            AppendString(sb, EDITOR_FILE_NAME_KEY, GetString(EDITOR_FILE_NAME_KEY, DEFAULT_EDITOR_FILE_NAME));
            AppendString(sb, PLAYER_FILE_NAME_KEY, GetString(PLAYER_FILE_NAME_KEY, DEFAULT_PLAYER_FILE_NAME));
            AppendString(sb, "assetResourcePath", RUNTIME_SETTINGS_RESOURCE_PATH);
            AppendString(sb, "assetPath", RUNTIME_SETTINGS_ASSET_PATH);
            sb.Append('}');
        }

        /// <summary>
        /// 从命令桥 payload JSON 应用 LogKit 设置。
        /// </summary>
        /// <param name="payloadJson">设置 payload JSON。</param>
        public static void ApplyPayload(string payloadJson)
        {
            if (string.IsNullOrEmpty(payloadJson))
                return;

            SetBoolIfPresent(payloadJson, ENABLED_KEY);
            SetLevelIfPresent(payloadJson);
            SetBoolIfPresent(payloadJson, SAVE_LOG_IN_EDITOR_KEY);
            SetBoolIfPresent(payloadJson, SAVE_LOG_IN_PLAYER_KEY);
            SetBoolIfPresent(payloadJson, ENABLE_IMGUI_IN_PLAYER_KEY);
            SetBoolIfPresent(payloadJson, ENABLE_ENCRYPTION_KEY);
            SetIntIfPresent(payloadJson, MAX_QUEUE_SIZE_KEY, 1, int.MaxValue);
            SetIntIfPresent(payloadJson, MAX_SAME_LOG_COUNT_KEY, 0, int.MaxValue);
            SetIntIfPresent(payloadJson, MAX_RETENTION_DAYS_KEY, 1, int.MaxValue);
            SetIntIfPresent(payloadJson, MAX_FILE_SIZE_MB_KEY, 1, int.MaxValue);
            SetIntIfPresent(payloadJson, IMGUI_MAX_LOG_COUNT_KEY, 1, int.MaxValue);
            SetStringIfPresent(payloadJson, LOG_DIRECTORY_KEY);
            SetStringIfPresent(payloadJson, EDITOR_FILE_NAME_KEY);
            SetStringIfPresent(payloadJson, PLAYER_FILE_NAME_KEY);
            ApplyBaseRuntimeSettings();
        }

        /// <summary>
        /// 将 LogKit 设置重置为默认值。
        /// </summary>
        public static void ResetToDefaults()
        {
            KitSettings.SetBool(KIT_NAME, ENABLED_KEY, DEFAULT_ENABLED);
            KitSettings.SetString(KIT_NAME, MINIMUM_LEVEL_KEY, DEFAULT_MINIMUM_LEVEL.ToString());
            KitSettings.SetBool(KIT_NAME, SAVE_LOG_IN_EDITOR_KEY, DEFAULT_SAVE_LOG_IN_EDITOR);
            KitSettings.SetBool(KIT_NAME, SAVE_LOG_IN_PLAYER_KEY, DEFAULT_SAVE_LOG_IN_PLAYER);
            KitSettings.SetBool(KIT_NAME, ENABLE_IMGUI_IN_PLAYER_KEY, DEFAULT_ENABLE_IMGUI_IN_PLAYER);
            KitSettings.SetBool(KIT_NAME, ENABLE_ENCRYPTION_KEY, DEFAULT_ENABLE_ENCRYPTION);
            KitSettings.SetInt(KIT_NAME, MAX_QUEUE_SIZE_KEY, DEFAULT_MAX_QUEUE_SIZE);
            KitSettings.SetInt(KIT_NAME, MAX_SAME_LOG_COUNT_KEY, DEFAULT_MAX_SAME_LOG_COUNT);
            KitSettings.SetInt(KIT_NAME, MAX_RETENTION_DAYS_KEY, DEFAULT_MAX_RETENTION_DAYS);
            KitSettings.SetInt(KIT_NAME, MAX_FILE_SIZE_MB_KEY, DEFAULT_MAX_FILE_SIZE_MB);
            KitSettings.SetInt(KIT_NAME, IMGUI_MAX_LOG_COUNT_KEY, DEFAULT_IMGUI_MAX_LOG_COUNT);
            KitSettings.SetString(KIT_NAME, LOG_DIRECTORY_KEY, DEFAULT_LOG_DIRECTORY);
            KitSettings.SetString(KIT_NAME, EDITOR_FILE_NAME_KEY, DEFAULT_EDITOR_FILE_NAME);
            KitSettings.SetString(KIT_NAME, PLAYER_FILE_NAME_KEY, DEFAULT_PLAYER_FILE_NAME);
            ApplyBaseRuntimeSettings();
        }

        /// <summary>
        /// 将通用设置中的 LogKit 开关和等级同步到 LogKit 运行状态。
        /// </summary>
        public static void ApplyBaseRuntimeSettings()
        {
            LogKit.Enabled = Enabled;
            LogKit.MinimumLevel = MinimumLevel;
        }

        /// <summary>
        /// 读取指定布尔配置。
        /// </summary>
        /// <param name="key">配置 key。</param>
        /// <param name="defaultValue">配置不存在时的默认值。</param>
        /// <returns>配置值。</returns>
        public static bool GetBool(string key, bool defaultValue)
        {
            return KitSettings.GetBool(KIT_NAME, key, defaultValue);
        }

        /// <summary>
        /// 读取指定整数配置。
        /// </summary>
        /// <param name="key">配置 key。</param>
        /// <param name="defaultValue">配置不存在时的默认值。</param>
        /// <returns>配置值。</returns>
        public static int GetInt(string key, int defaultValue)
        {
            return KitSettings.GetInt(KIT_NAME, key, defaultValue);
        }

        /// <summary>
        /// 读取指定字符串配置。
        /// </summary>
        /// <param name="key">配置 key。</param>
        /// <param name="defaultValue">配置不存在时的默认值。</param>
        /// <returns>配置值。</returns>
        public static string GetString(string key, string defaultValue)
        {
            return KitSettings.GetString(KIT_NAME, key, defaultValue);
        }

        private static void SetBoolIfPresent(string payloadJson, string key)
        {
            bool value;
            if (JsonHelper.TryExtractBool(payloadJson, key, out value))
                KitSettings.SetBool(KIT_NAME, key, value);
        }

        private static void SetIntIfPresent(string payloadJson, string key, int min, int max)
        {
            int value;
            if (!JsonHelper.TryExtractInt(payloadJson, key, out value))
                return;

            if (value < min)
                value = min;
            if (value > max)
                value = max;
            KitSettings.SetInt(KIT_NAME, key, value);
        }

        private static void SetStringIfPresent(string payloadJson, string key)
        {
            var value = JsonHelper.ExtractString(payloadJson, key);
            if (value != null)
                KitSettings.SetString(KIT_NAME, key, value);
        }

        private static void SetLevelIfPresent(string payloadJson)
        {
            var value = JsonHelper.ExtractString(payloadJson, MINIMUM_LEVEL_KEY);
            if (string.IsNullOrEmpty(value))
                return;

            LogLevel parsed;
            if (Enum.TryParse(value, true, out parsed))
                KitSettings.SetString(KIT_NAME, MINIMUM_LEVEL_KEY, NormalizeLevel(parsed).ToString());
        }

        private static LogLevel NormalizeLevel(LogLevel level)
        {
            if (level == LogLevel.Debug ||
                level == LogLevel.Info ||
                level == LogLevel.Warning ||
                level == LogLevel.Error)
            {
                return level;
            }

            return DEFAULT_MINIMUM_LEVEL;
        }

        private static void AppendBool(StringBuilder sb, string key, bool value)
        {
            sb.Append(",\"");
            sb.Append(key);
            sb.Append("\":");
            sb.Append(value ? "true" : "false");
        }

        private static void AppendInt(StringBuilder sb, string key, int value)
        {
            sb.Append(",\"");
            sb.Append(key);
            sb.Append("\":");
            sb.Append(value);
        }

        private static void AppendString(StringBuilder sb, string key, string value)
        {
            sb.Append(",\"");
            sb.Append(key);
            sb.Append("\":\"");
            sb.Append(JsonHelper.EscapeString(value));
            sb.Append('"');
        }
    }
}
