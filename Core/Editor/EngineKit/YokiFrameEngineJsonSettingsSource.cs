#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// 从 JSON 文件读取 Engine Kit 执行开关；文件缺失或解析失败一律按关闭处理，不抛异常。
    /// </summary>
    /// <remarks>
    /// 兼容两种文档形态：
    /// ① 专用形态 <c>{ "engineOperations": { "enabled": true } }</c>（enabled 必须是 JSON 布尔）；
    /// ② 工程既有设置文件形态 <c>{ "formatVersion": 1, "settings": [ { "kit": "Engine", "key": "operations.enabled", "value": "true" } ] }</c>，
    /// 与 Workbench 的工程设置存储共用同一份文件，避免出现第二个开关来源。
    /// </remarks>
    public sealed class YokiFrameEngineJsonSettingsSource : IYokiFrameEngineSettingsSource
    {
        /// <summary>专用开关小节名。</summary>
        public const string SETTINGS_OBJECT_NAME = "engineOperations";

        /// <summary>专用开关布尔字段名。</summary>
        public const string ENABLED_PROPERTY_NAME = "enabled";

        /// <summary>工程设置数组字段名。</summary>
        public const string PROJECT_SETTINGS_ARRAY_NAME = "settings";

        /// <summary>工程设置中 Engine 开关的 Kit 名。</summary>
        public const string ENGINE_KIT_NAME = "Engine";

        /// <summary>工程设置中 Engine 开关的主键。</summary>
        public const string ENGINE_ENABLED_KEY = "operations.enabled";

        /// <summary>工程设置中 Engine 开关的兼容键。</summary>
        public const string ENGINE_ENABLED_LEGACY_KEY = "enabled";

        private readonly string mSettingsPath;
        private readonly string mEnabledKey;
        private readonly string mEnabledProperty;
        private YokiFrameEngineSettingsSnapshot mCachedSnapshot;
        private bool mCachedExists;
        private DateTime mCachedLastWriteUtc = DateTime.MinValue;
        private long mCachedLength = -1L;

        /// <summary>
        /// 创建基于文件的开关读取器。
        /// </summary>
        /// <param name="settingsPath">配置文件绝对路径。</param>
        public YokiFrameEngineJsonSettingsSource(
            string settingsPath, string enabledKey = ENGINE_ENABLED_KEY,
            string enabledProperty = ENABLED_PROPERTY_NAME)
        {
            if (string.IsNullOrEmpty(settingsPath))
            {
                throw new ArgumentNullException(nameof(settingsPath));
            }

            mSettingsPath = settingsPath;
            mEnabledKey = enabledKey;
            mEnabledProperty = enabledProperty;
        }

        /// <summary>获取配置文件路径，供诊断输出使用。</summary>
        public string SettingsPath
        {
            get { return mSettingsPath; }
        }

        /// <summary>
        /// 读取并解析开关；任何 IO 或格式问题都转换为关闭快照。
        /// </summary>
        /// <returns>当前开关快照。</returns>
        /// <remarks>
        /// 按 (存在性, mtime, 长度) 缓存（§9 决议 B）：宿主每帧 tick 都要判定开关，
        /// 不能每帧都读文件；文件一变缓存立即失效，因此开与关依旧即时生效。
        /// </remarks>
        public YokiFrameEngineSettingsSnapshot Read()
        {
            var info = new FileInfo(mSettingsPath);
            bool exists = info.Exists;
            DateTime lastWriteUtc = exists ? info.LastWriteTimeUtc : DateTime.MinValue;
            long length = exists ? info.Length : -1L;
            if (mCachedSnapshot != null
                && exists == mCachedExists
                && lastWriteUtc == mCachedLastWriteUtc
                && length == mCachedLength)
            {
                return mCachedSnapshot;
            }

            YokiFrameEngineSettingsSnapshot snapshot = ReadUncached();
            mCachedSnapshot = snapshot;
            mCachedExists = exists;
            mCachedLastWriteUtc = lastWriteUtc;
            mCachedLength = length;
            return snapshot;
        }

        private YokiFrameEngineSettingsSnapshot ReadUncached()
        {
            string json;
            try
            {
                if (!File.Exists(mSettingsPath))
                {
                    return YokiFrameEngineSettingsSnapshot.MissingConfig(
                        "Engine settings file was not found: " + mSettingsPath);
                }

                json = File.ReadAllText(mSettingsPath);
            }
            catch (IOException exception)
            {
                return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                    "Engine settings file could not be read: " + exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                    "Engine settings file could not be read: " + exception.Message);
            }

            if (string.IsNullOrEmpty(json))
            {
                return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                    "Engine settings file is empty: " + mSettingsPath);
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                    "Engine settings file is not valid JSON: " + exception.Message);
            }

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                    "Engine settings file root must be a JSON object.");
            }

            if (root.TryGetProperty(SETTINGS_OBJECT_NAME, out JsonElement section))
            {
                return ReadDedicatedSection(section);
            }

            if (root.TryGetProperty(PROJECT_SETTINGS_ARRAY_NAME, out JsonElement entries))
            {
                return ReadProjectSettings(entries);
            }

            return YokiFrameEngineSettingsSnapshot.Disabled(
                "Engine settings file has neither " + SETTINGS_OBJECT_NAME + " nor " + PROJECT_SETTINGS_ARRAY_NAME + ".");
        }

        /// <summary>
        /// 读取专用小节形态。
        /// </summary>
        /// <param name="section">engineOperations 值。</param>
        /// <returns>开关快照。</returns>
        private YokiFrameEngineSettingsSnapshot ReadDedicatedSection(JsonElement section)
        {
            if (section.ValueKind != JsonValueKind.Object)
            {
                return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                    SETTINGS_OBJECT_NAME + " section is not a JSON object.");
            }

            if (!section.TryGetProperty(mEnabledProperty, out JsonElement enabled))
            {
                return YokiFrameEngineSettingsSnapshot.Disabled(
                    SETTINGS_OBJECT_NAME + "." + mEnabledProperty + " is not declared.");
            }

            if (enabled.ValueKind == JsonValueKind.True)
            {
                return YokiFrameEngineSettingsSnapshot.Enabled();
            }

            if (enabled.ValueKind == JsonValueKind.False)
            {
                return YokiFrameEngineSettingsSnapshot.Disabled(
                    SETTINGS_OBJECT_NAME + "." + ENABLED_PROPERTY_NAME + " is false.");
            }

            return YokiFrameEngineSettingsSnapshot.Disabled(
                SETTINGS_OBJECT_NAME + "." + ENABLED_PROPERTY_NAME + " is not a JSON boolean.");
        }

        /// <summary>
        /// 读取工程设置数组形态；只认 Engine Kit 的开关键，值按字符串比较。
        /// </summary>
        /// <param name="entries">settings 数组。</param>
        /// <returns>开关快照。</returns>
        private YokiFrameEngineSettingsSnapshot ReadProjectSettings(JsonElement entries)
        {
            if (entries.ValueKind != JsonValueKind.Array)
            {
                return YokiFrameEngineSettingsSnapshot.InvalidConfig(
                    PROJECT_SETTINGS_ARRAY_NAME + " is not a JSON array.");
            }

            System.Collections.Generic.IReadOnlyList<JsonElement> items = entries.EnumerateArray();
            for (var index = 0; index < items.Count; index++)
            {
                JsonElement item = items[index];
                if (item.ValueKind != JsonValueKind.Object
                    || !item.TryGetProperty("kit", out JsonElement kit)
                    || kit.ValueKind != JsonValueKind.String
                    || !string.Equals(kit.GetString(), ENGINE_KIT_NAME, StringComparison.Ordinal)
                    || !item.TryGetProperty("key", out JsonElement key)
                    || key.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string keyName = key.GetString();
                if (!string.Equals(keyName, mEnabledKey, StringComparison.Ordinal)
                    && !(mEnabledKey == ENGINE_ENABLED_KEY
                         && string.Equals(keyName, ENGINE_ENABLED_LEGACY_KEY, StringComparison.Ordinal)))
                {
                    continue;
                }

                if (!item.TryGetProperty("value", out JsonElement value) || value.ValueKind != JsonValueKind.String)
                {
                    return YokiFrameEngineSettingsSnapshot.Disabled(
                        "Engine settings entry '" + keyName + "' has no string value.");
                }

                string text = value.GetString();
                if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
                {
                    return YokiFrameEngineSettingsSnapshot.Enabled();
                }

                if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
                {
                    return YokiFrameEngineSettingsSnapshot.Disabled(
                        "Engine settings entry '" + keyName + "' is false.");
                }

                return YokiFrameEngineSettingsSnapshot.Disabled(
                    "Engine settings entry '" + keyName + "' is not a boolean literal.");
            }

            return YokiFrameEngineSettingsSnapshot.Disabled(
                "Engine settings file has no " + ENGINE_KIT_NAME + " '" + mEnabledKey + "' entry.");
        }
    }
}
#endif
