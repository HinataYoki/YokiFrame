using System;
using System.Diagnostics;
#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Globalization;
using System.Text;
using System.Threading;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 定义 LogKit 跨宿主运行时设置；Editor/Tools 编译额外提供 Workbench 设置交换能力。
    /// </summary>
    public static partial class LogKitSettings
    {
#if UNITY_EDITOR || (GODOT && TOOLS)
        private static long sSettingsVersion;
#endif

        /// <summary>LogKit 在通用设置存储中的 Kit 名称。</summary>
        public const string KIT_NAME = "LogKit";
        /// <summary>日志总开关配置 key。</summary>
        public const string ENABLED_KEY = "enabled";
        /// <summary>最小日志等级配置 key。</summary>
        public const string MINIMUM_LEVEL_KEY = "minimumLevel";
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>Editor/Tools 日志文件保存配置 key。</summary>
        public const string SAVE_LOG_IN_EDITOR_KEY = "saveLogInEditor";
#endif
        /// <summary>Player 日志文件保存配置 key。</summary>
        public const string SAVE_LOG_IN_PLAYER_KEY = "saveLogInPlayer";
        /// <summary>Player IMGUI 日志面板配置 key。</summary>
        public const string ENABLE_IMGUI_IN_PLAYER_KEY = "enableIMGUIInPlayer";
        /// <summary>日志文件加密配置 key。</summary>
        public const string ENABLE_ENCRYPTION_KEY = "enableEncryption";
        /// <summary>日志文件写入队列容量配置 key。</summary>
        public const string MAX_QUEUE_SIZE_KEY = "maxQueueSize";
        /// <summary>相同日志连续重复上限配置 key。</summary>
        public const string MAX_SAME_LOG_COUNT_KEY = "maxSameLogCount";
        /// <summary>日志文件保留天数配置 key。</summary>
        public const string MAX_RETENTION_DAYS_KEY = "maxRetentionDays";
        /// <summary>单个日志文件大小上限配置 key。</summary>
        public const string MAX_FILE_SIZE_MB_KEY = "maxFileSizeMB";
        /// <summary>IMGUI 日志面板最大显示条数配置 key。</summary>
        public const string IMGUI_MAX_LOG_COUNT_KEY = "imguiMaxLogCount";
        /// <summary>日志目录配置 key。</summary>
        public const string LOG_DIRECTORY_KEY = "logDirectory";
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>Editor/Tools 日志文件名配置 key。</summary>
        public const string EDITOR_FILE_NAME_KEY = "editorFileName";
#endif
        /// <summary>Player 日志文件名配置 key。</summary>
        public const string PLAYER_FILE_NAME_KEY = "playerFileName";

        /// <summary>默认是否启用日志。</summary>
        public const bool DEFAULT_ENABLED = true;
        /// <summary>默认最小日志等级。</summary>
        public const LogLevel DEFAULT_MINIMUM_LEVEL = LogLevel.Debug;
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>默认是否在 Editor/Tools 保存日志文件。</summary>
        public const bool DEFAULT_SAVE_LOG_IN_EDITOR = false;
#endif
        /// <summary>默认是否在 Player 保存日志文件。</summary>
        public const bool DEFAULT_SAVE_LOG_IN_PLAYER = true;
        /// <summary>默认是否在 Player 启用 IMGUI 日志面板。</summary>
        public const bool DEFAULT_ENABLE_IMGUI_IN_PLAYER = false;
        /// <summary>默认是否启用日志文件加密。</summary>
        public const bool DEFAULT_ENABLE_ENCRYPTION = true;
        /// <summary>默认日志文件写入队列容量。</summary>
        public const int DEFAULT_MAX_QUEUE_SIZE = 20000;
        /// <summary>默认相同日志连续重复上限。</summary>
        public const int DEFAULT_MAX_SAME_LOG_COUNT = 50;
        /// <summary>默认日志文件保留天数。</summary>
        public const int DEFAULT_MAX_RETENTION_DAYS = 15;
        /// <summary>默认单个日志文件大小上限，单位 MB。</summary>
        public const int DEFAULT_MAX_FILE_SIZE_MB = 100;
        /// <summary>默认 IMGUI 日志面板最大显示条数。</summary>
        public const int DEFAULT_IMGUI_MAX_LOG_COUNT = 200;
        /// <summary>默认日志目录，空字符串表示由宿主 Adapter 选择持久化目录。</summary>
        public const string DEFAULT_LOG_DIRECTORY = "";
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>默认 Editor/Tools 日志文件名。</summary>
        public const string DEFAULT_EDITOR_FILE_NAME = "yoki_editor.log";
#endif
        /// <summary>默认 Player 日志文件名。</summary>
        public const string DEFAULT_PLAYER_FILE_NAME = "yoki_player.log";

        /// <summary>
        /// 在完整 Runtime 设置已同步到 LogKit 后触发，供宿主 Adapter 刷新自身的非 Core 行为。
        /// 订阅方不得阻塞、递归应用设置或引用其它宿主的实现。
        /// </summary>
        public static event Action RuntimeSettingsApplied;

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 获取 Editor/Tools 的 LogKit 设置版本号；命令桥设置变更时递增。
        /// </summary>
        public static long SettingsVersion
        {
            get { return Interlocked.Read(ref sSettingsVersion); }
        }
#endif

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
                string value = KitSettings.GetString(KIT_NAME, MINIMUM_LEVEL_KEY, DEFAULT_MINIMUM_LEVEL.ToString());
                LogLevel parsed;
                return Enum.TryParse(value, true, out parsed) ? LogKit.NormalizeLevel(parsed, DEFAULT_MINIMUM_LEVEL) : DEFAULT_MINIMUM_LEVEL;
            }
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 构建当前 LogKit 设置 JSON。
        /// </summary>
        /// <returns>当前设置 JSON。</returns>
        public static string BuildJson()
        {
            var builder = new StringBuilder(320);
            AppendJson(builder);
            return builder.ToString();
        }

        /// <summary>
        /// 将当前 LogKit 设置追加为 JSON 对象。
        /// </summary>
        /// <param name="builder">用于接收 JSON 的字符串构建器。</param>
        public static void AppendJson(StringBuilder builder)
        {
            builder.Append('{');
            AppendHead(builder);
            AppendBool(builder, SAVE_LOG_IN_EDITOR_KEY, GetBool(SAVE_LOG_IN_EDITOR_KEY, DEFAULT_SAVE_LOG_IN_EDITOR));
            AppendBool(builder, SAVE_LOG_IN_PLAYER_KEY, GetBool(SAVE_LOG_IN_PLAYER_KEY, DEFAULT_SAVE_LOG_IN_PLAYER));
            AppendBool(builder, ENABLE_IMGUI_IN_PLAYER_KEY, GetBool(ENABLE_IMGUI_IN_PLAYER_KEY, DEFAULT_ENABLE_IMGUI_IN_PLAYER));
            AppendBool(builder, ENABLE_ENCRYPTION_KEY, GetBool(ENABLE_ENCRYPTION_KEY, DEFAULT_ENABLE_ENCRYPTION));
            AppendNumericAndPathSettings(builder);
            builder.Append('}');
        }

        /// <summary>
        /// 从命令桥 payload JSON 原子应用完整 LogKit 设置。
        /// </summary>
        /// <param name="payloadJson">包含全部固定字段的顶层扁平设置对象。</param>
        /// <exception cref="ArgumentException">payload 不是完整有效设置对象时抛出。</exception>
        public static void ApplyPayload(string payloadJson)
        {
            if (!TryApplyCompletePayload(payloadJson, out var errorMessage))
            {
                throw new ArgumentException(errorMessage, nameof(payloadJson));
            }
        }

        /// <summary>
        /// 将 LogKit 设置恢复为默认值。
        /// </summary>
        public static void ResetToDefaults()
        {
            SetDefaultBaseSettings();
            SetDefaultStorageSettings();
            BumpSettingsVersion();
            ApplyBaseRuntimeSettings();
        }
#endif

        /// <summary>
        /// 将通用设置中的 LogKit 开关和等级同步到 LogKit 运行状态。
        /// 订阅者异常只记录诊断，不传播到日志调用方，也不再走 LogKit 以免重入。
        /// </summary>
        public static void ApplyBaseRuntimeSettings()
        {
            LogKit.Enabled = Enabled;
            LogKit.MinimumLevel = MinimumLevel;
            var handlers = RuntimeSettingsApplied;
            if (handlers == null)
            {
                return;
            }

            foreach (Delegate handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action)handler)();
                }
                catch (Exception exception)
                {
                    // 首次写日志也会走到这里；不能再调用 LogKit，否则可能重入工厂或覆盖层。
                    Debug.WriteLine("[YokiFrame][LogKit] RuntimeSettingsApplied handler failed: " + exception);
                }
            }
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

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 追加 JSON 开头的基础字段。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        private static void AppendHead(StringBuilder builder)
        {
            builder.Append('"');
            builder.Append(ENABLED_KEY);
            builder.Append("\":");
            builder.Append(Enabled ? "true" : "false");
            AppendString(builder, MINIMUM_LEVEL_KEY, MinimumLevel.ToString());
        }

        /// <summary>
        /// 追加数字和路径类设置。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        private static void AppendNumericAndPathSettings(StringBuilder builder)
        {
            AppendInt(builder, MAX_QUEUE_SIZE_KEY, GetInt(MAX_QUEUE_SIZE_KEY, DEFAULT_MAX_QUEUE_SIZE));
            AppendInt(builder, MAX_SAME_LOG_COUNT_KEY, GetInt(MAX_SAME_LOG_COUNT_KEY, DEFAULT_MAX_SAME_LOG_COUNT));
            AppendInt(builder, MAX_RETENTION_DAYS_KEY, GetInt(MAX_RETENTION_DAYS_KEY, DEFAULT_MAX_RETENTION_DAYS));
            AppendInt(builder, MAX_FILE_SIZE_MB_KEY, GetInt(MAX_FILE_SIZE_MB_KEY, DEFAULT_MAX_FILE_SIZE_MB));
            AppendInt(builder, IMGUI_MAX_LOG_COUNT_KEY, GetInt(IMGUI_MAX_LOG_COUNT_KEY, DEFAULT_IMGUI_MAX_LOG_COUNT));
            AppendString(builder, LOG_DIRECTORY_KEY, GetString(LOG_DIRECTORY_KEY, DEFAULT_LOG_DIRECTORY));
            AppendString(builder, EDITOR_FILE_NAME_KEY, GetString(EDITOR_FILE_NAME_KEY, DEFAULT_EDITOR_FILE_NAME));
            AppendString(builder, PLAYER_FILE_NAME_KEY, GetString(PLAYER_FILE_NAME_KEY, DEFAULT_PLAYER_FILE_NAME));
        }

        /// <summary>
        /// 写入默认基础设置。
        /// </summary>
        private static void SetDefaultBaseSettings()
        {
            KitSettings.SetBool(KIT_NAME, ENABLED_KEY, DEFAULT_ENABLED);
            KitSettings.SetString(KIT_NAME, MINIMUM_LEVEL_KEY, DEFAULT_MINIMUM_LEVEL.ToString());
        }

        /// <summary>
        /// 写入默认存储和展示设置。
        /// </summary>
        private static void SetDefaultStorageSettings()
        {
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
        }
#endif

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 追加布尔 JSON 字段。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="key">字段名。</param>
        /// <param name="value">字段值。</param>
        private static void AppendBool(StringBuilder builder, string key, bool value)
        {
            builder.Append(",\"");
            builder.Append(key);
            builder.Append("\":");
            builder.Append(value ? "true" : "false");
        }

        /// <summary>
        /// 追加整数 JSON 字段。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="key">字段名。</param>
        /// <param name="value">字段值。</param>
        private static void AppendInt(StringBuilder builder, string key, int value)
        {
            builder.Append(",\"");
            builder.Append(key);
            builder.Append("\":");
            builder.Append(value);
        }

        /// <summary>
        /// 追加字符串 JSON 字段。
        /// </summary>
        /// <param name="builder">字符串构建器。</param>
        /// <param name="key">字段名。</param>
        /// <param name="value">字段值。</param>
        private static void AppendString(StringBuilder builder, string key, string value)
        {
            builder.Append(",\"");
            builder.Append(key);
            builder.Append("\":\"");
            AppendEscapedString(builder, value);
            builder.Append('"');
        }

        /// <summary>
        /// 将设置字符串按 JSON 字符串规则写入现有缓冲区，避免 Runtime 反向依赖 Editor CommandBridge。
        /// </summary>
        /// <param name="builder">接收转义内容的字符串构建器。</param>
        /// <param name="value">待转义设置值。</param>
        private static void AppendEscapedString(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value)) return;
            for (var index = 0; index < value.Length; index++)
            {
                AppendEscapedCharacter(builder, value[index]);
            }
        }

        /// <summary>
        /// 写入单个 JSON 字符串字符，并对控制字符使用标准转义。
        /// </summary>
        /// <param name="builder">接收转义内容的字符串构建器。</param>
        /// <param name="value">待写入字符。</param>
        private static void AppendEscapedCharacter(StringBuilder builder, char value)
        {
            switch (value)
            {
                case '\\': builder.Append("\\\\"); break;
                case '"': builder.Append("\\\""); break;
                case '\b': builder.Append("\\b"); break;
                case '\f': builder.Append("\\f"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (value < ' ')
                    {
                        builder.Append("\\u");
                        builder.Append(((int)value).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else builder.Append(value);
                    break;
            }
        }

        /// <summary>
        /// 递增设置版本号。
        /// </summary>
        private static void BumpSettingsVersion()
        {
            Interlocked.Increment(ref sSettingsVersion);
        }
#endif
    }
}
