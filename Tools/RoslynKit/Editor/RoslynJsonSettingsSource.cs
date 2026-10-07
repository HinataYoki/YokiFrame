#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.IO;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// 从 JSON 文件读取 RoslynKit 执行开关；文件缺失或解析失败一律按关闭处理，不抛异常。
    /// </summary>
    /// <remarks>
    /// 兼容两种文档形态：
    /// ① 专用形态 <c>{ "roslynOperations": { "enabled": true } }</c>（enabled 必须是 JSON 布尔）；
    /// ② 工程既有设置文件形态 <c>{ "formatVersion": 1, "settings": [ { "kit": "RoslynKit", "key": "operations.enabled", "value": "true" } ] }</c>，
    /// 与 Workbench 的工程设置存储共用同一份文件，避免出现第二个开关来源。
    /// </remarks>
    public sealed class RoslynJsonSettingsSource : IRoslynSettingsSource
    {
        /// <summary>专用开关小节名。</summary>
        public const string SETTINGS_OBJECT_NAME = "roslynOperations";

        /// <summary>专用开关布尔字段名。</summary>
        public const string ENABLED_PROPERTY_NAME = "enabled";

        /// <summary>工程设置数组字段名。</summary>
        public const string PROJECT_SETTINGS_ARRAY_NAME = "settings";

        /// <summary>工程设置中 RoslynKit 开关的 Kit 名。不再接受旧名 Engine。</summary>
        public const string ENGINE_KIT_NAME = "RoslynKit";

        /// <summary>判断设置条目是否属于 RoslynKit。</summary>
        /// <param name="kit">设置条目的 kit 字段。</param>
        /// <returns>kit 等于 RoslynKit 时返回 true。</returns>
        private static bool IsRoslynKitName(string kit)
        {
            return string.Equals(kit, ENGINE_KIT_NAME, StringComparison.Ordinal);
        }

        /// <summary>工程设置中 Engine 开关的主键。</summary>
        public const string ENGINE_ENABLED_KEY = "operations.enabled";

        /// <summary>工程设置中 Engine 开关的兼容键。</summary>
        public const string ENGINE_ENABLED_LEGACY_KEY = "enabled";

        private readonly string mSettingsPath;
        private readonly string mEnabledKey;
        private readonly string mEnabledProperty;
        private RoslynSettingsSnapshot mCachedSnapshot;
        private bool mCachedExists;
        private DateTime mCachedLastWriteUtc = DateTime.MinValue;
        private long mCachedLength = -1L;

        /// <summary>
        /// 创建基于文件的开关读取器。
        /// </summary>
        /// <param name="settingsPath">配置文件绝对路径。</param>
        public RoslynJsonSettingsSource(
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
        public RoslynSettingsSnapshot Read()
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

            RoslynSettingsSnapshot snapshot = ReadUncached();
            mCachedSnapshot = snapshot;
            mCachedExists = exists;
            mCachedLastWriteUtc = lastWriteUtc;
            mCachedLength = length;
            return snapshot;
        }

        /// <summary>
        /// 不经缓存读取开关。文件缺失、无法读取或 JSON 无效时返回关闭快照，不把 IO 异常抛给调用方。
        /// </summary>
        /// <returns>开关快照。文档对象保留到读取结束，避免 JsonElement 提前失效。</returns>
        private RoslynSettingsSnapshot ReadUncached()
        {
            if (!TryReadSettingsText(out string json, out RoslynSettingsSnapshot failure))
            {
                return failure;
            }

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException exception)
            {
                return RoslynSettingsSnapshot.InvalidConfig(
                    "Engine settings file is not valid JSON: " + exception.Message);
            }

            return ReadSettingsDocument(document);
        }

        /// <summary>
        /// 读取设置文件正文。文件不存在、为空或没有读取权限时返回 false。
        /// </summary>
        /// <param name="json">成功时的文件正文。</param>
        /// <param name="failure">失败时的关闭快照。</param>
        /// <returns>读到非空正文时返回 true。</returns>
        private bool TryReadSettingsText(out string json, out RoslynSettingsSnapshot failure)
        {
            json = string.Empty;
            failure = null;
            try
            {
                if (!File.Exists(mSettingsPath))
                {
                    failure = RoslynSettingsSnapshot.MissingConfig(
                        "Engine settings file was not found: " + mSettingsPath);
                    return false;
                }

                json = File.ReadAllText(mSettingsPath);
            }
            catch (IOException exception)
            {
                failure = RoslynSettingsSnapshot.InvalidConfig(
                    "Engine settings file could not be read: " + exception.Message);
                return false;
            }
            catch (UnauthorizedAccessException exception)
            {
                failure = RoslynSettingsSnapshot.InvalidConfig(
                    "Engine settings file could not be read: " + exception.Message);
                return false;
            }

            if (string.IsNullOrEmpty(json))
            {
                failure = RoslynSettingsSnapshot.InvalidConfig(
                    "Engine settings file is empty: " + mSettingsPath);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 按专用小节或工程设置数组解释根对象。两种字段都没有时按关闭处理。
        /// </summary>
        /// <param name="document">已解析的设置文档。本方法不释放它。</param>
        /// <returns>开关快照。</returns>
        private RoslynSettingsSnapshot ReadSettingsDocument(JsonDocument document)
        {
            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return RoslynSettingsSnapshot.InvalidConfig(
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

            return RoslynSettingsSnapshot.Disabled(
                "RoslynKit settings file has neither " + SETTINGS_OBJECT_NAME + " nor " + PROJECT_SETTINGS_ARRAY_NAME + ".");
        }

        /// <summary>
        /// 读取专用小节形态。
        /// </summary>
        /// <param name="section">roslynOperations 值。</param>
        /// <returns>开关快照。</returns>
        private RoslynSettingsSnapshot ReadDedicatedSection(JsonElement section)
        {
            if (section.ValueKind != JsonValueKind.Object)
            {
                return RoslynSettingsSnapshot.InvalidConfig(
                    SETTINGS_OBJECT_NAME + " section is not a JSON object.");
            }

            if (!section.TryGetProperty(mEnabledProperty, out JsonElement enabled))
            {
                return RoslynSettingsSnapshot.Disabled(
                    SETTINGS_OBJECT_NAME + "." + mEnabledProperty + " is not declared.");
            }

            if (enabled.ValueKind == JsonValueKind.True)
            {
                return RoslynSettingsSnapshot.Enabled();
            }

            if (enabled.ValueKind == JsonValueKind.False)
            {
                return RoslynSettingsSnapshot.Disabled(
                    SETTINGS_OBJECT_NAME + "." + ENABLED_PROPERTY_NAME + " is false.");
            }

            return RoslynSettingsSnapshot.Disabled(
                SETTINGS_OBJECT_NAME + "." + ENABLED_PROPERTY_NAME + " is not a JSON boolean.");
        }

        /// <summary>
        /// 读取工程设置数组形态；只认 RoslynKit 的开关键，值按字符串比较。
        /// </summary>
        /// <param name="entries">settings 数组。</param>
        /// <returns>开关快照。</returns>
        private RoslynSettingsSnapshot ReadProjectSettings(JsonElement entries)
        {
            if (entries.ValueKind != JsonValueKind.Array)
            {
                return RoslynSettingsSnapshot.InvalidConfig(
                    PROJECT_SETTINGS_ARRAY_NAME + " is not a JSON array.");
            }

            System.Collections.Generic.IReadOnlyList<JsonElement> items = entries.EnumerateArray();
            for (var index = 0; index < items.Count; index++)
            {
                if (TryTakeEnabledSnapshot(items[index], out RoslynSettingsSnapshot snapshot))
                {
                    return snapshot;
                }
            }

            return RoslynSettingsSnapshot.Disabled(
                "Engine settings file has no " + ENGINE_KIT_NAME + " '" + mEnabledKey + "' entry.");
        }

        /// <summary>
        /// 命中 RoslynKit 当前或旧开关键时写出快照。其他条目返回 false，由调用方继续扫描。
        /// </summary>
        /// <param name="item">settings 数组中的一项。</param>
        /// <param name="snapshot">命中时的开关快照。</param>
        /// <returns>这一项决定了开关结果时返回 true。</returns>
        private bool TryTakeEnabledSnapshot(JsonElement item, out RoslynSettingsSnapshot snapshot)
        {
            snapshot = null;
            if (!TryReadEnabledKey(item, out string keyName))
            {
                return false;
            }

            if (!item.TryGetProperty("value", out JsonElement value) || value.ValueKind != JsonValueKind.String)
            {
                snapshot = RoslynSettingsSnapshot.Disabled(
                    "Engine settings entry '" + keyName + "' has no string value.");
                return true;
            }

            snapshot = InterpretEnabledValue(keyName, value.GetString());
            return true;
        }

        /// <summary>确认这一项是 RoslynKit 的当前开关键或受支持的旧键。</summary>
        /// <param name="item">settings 数组中的一项。</param>
        /// <param name="keyName">命中的键名。</param>
        /// <returns>应解释该项的 value 时返回 true。</returns>
        private bool TryReadEnabledKey(JsonElement item, out string keyName)
        {
            keyName = string.Empty;
            if (item.ValueKind != JsonValueKind.Object
                || !item.TryGetProperty("kit", out JsonElement kit)
                || kit.ValueKind != JsonValueKind.String
                || !IsRoslynKitName(kit.GetString())
                || !item.TryGetProperty("key", out JsonElement key)
                || key.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            keyName = key.GetString();
            return string.Equals(keyName, mEnabledKey, StringComparison.Ordinal)
                || (mEnabledKey == ENGINE_ENABLED_KEY
                    && string.Equals(keyName, ENGINE_ENABLED_LEGACY_KEY, StringComparison.Ordinal));
        }

        /// <summary>只接受 true 或 false 字面量，大小写不敏感。其他文本按关闭处理。</summary>
        /// <param name="keyName">命中的键名，用于诊断。</param>
        /// <param name="text">value 字符串。</param>
        /// <returns>开关快照。</returns>
        private static RoslynSettingsSnapshot InterpretEnabledValue(string keyName, string text)
        {
            if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase))
            {
                return RoslynSettingsSnapshot.Enabled();
            }

            if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase))
            {
                return RoslynSettingsSnapshot.Disabled(
                    "Engine settings entry '" + keyName + "' is false.");
            }

            return RoslynSettingsSnapshot.Disabled(
                "Engine settings entry '" + keyName + "' is not a boolean literal.");
        }
    }
}
#endif
