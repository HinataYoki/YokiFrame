#if !GODOT
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

#if UNITY_EDITOR
using System.IO;
using UnityEditor;
#endif

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity Adapter 的 YokiFrame 运行时设置资产。
    /// </summary>
    [CreateAssetMenu(fileName = RESOURCES_PATH, menuName = "YokiFrame/Runtime Settings")]
    public sealed class YokiFrameRuntimeSettings : ScriptableObject
    {
        /// <summary>
        /// Runtime Settings 在 Unity Resources 中的加载路径。
        /// </summary>
        public const string RESOURCES_PATH = "YokiFrameRuntimeSettings";

        /// <summary>
        /// Runtime Settings 资产路径。
        /// </summary>
        public const string ASSET_PATH = "Assets/Settings/Resources/YokiFrameRuntimeSettings.asset";

        /// <summary>
        /// Runtime Settings 资产所在目录。
        /// </summary>
        public const string ASSET_DIRECTORY = "Assets/Settings/Resources";

        [SerializeField] private UnityLogKitOptions mLogKit = UnityLogKitOptions.CreateDefault();
        [SerializeField] private List<YokiFrameRuntimeSettingEntry> mEntries = new();

        /// <summary>
        /// 获取 Unity LogKit 运行时配置。
        /// </summary>
        public UnityLogKitOptions LogKit
        {
            get
            {
                EnsureLogKit();
                return mLogKit;
            }
        }

        /// <summary>
        /// 尝试从 Resources 或编辑器资产路径加载 Runtime Settings。
        /// </summary>
        /// <param name="settings">加载到的 Runtime Settings。</param>
        /// <returns>加载成功时返回 true。</returns>
        public static bool TryLoad(out YokiFrameRuntimeSettings settings)
        {
            settings = Resources.Load<YokiFrameRuntimeSettings>(RESOURCES_PATH);
            if (settings != null)
            {
                settings.Normalize();
                return true;
            }

#if UNITY_EDITOR
            settings = AssetDatabase.LoadAssetAtPath<YokiFrameRuntimeSettings>(ASSET_PATH);
            if (settings != null)
            {
                settings.Normalize();
                return true;
            }
#endif
            return false;
        }

        /// <summary>
        /// 加载 Runtime Settings；编辑器中不存在时会创建资产。
        /// </summary>
        /// <returns>可用的 Runtime Settings。</returns>
        public static YokiFrameRuntimeSettings LoadOrCreate()
        {
#if UNITY_EDITOR
            return LoadOrCreateInEditor();
#else
            YokiFrameRuntimeSettings settings;
            if (TryLoad(out settings))
                return settings;

            settings = CreateInstance<YokiFrameRuntimeSettings>();
            settings.Normalize();
            return settings;
#endif
        }

        /// <summary>
        /// 加载运行时 Runtime Settings；不存在时返回临时默认实例。
        /// </summary>
        /// <returns>可用的 Runtime Settings。</returns>
        public static YokiFrameRuntimeSettings LoadRuntimeOrDefault()
        {
            YokiFrameRuntimeSettings settings;
            if (TryLoad(out settings))
                return settings;

            settings = CreateInstance<YokiFrameRuntimeSettings>();
            settings.Normalize();
            return settings;
        }

        /// <summary>
        /// 创建当前 LogKit 配置副本。
        /// </summary>
        /// <returns>规范化后的 Unity LogKit 配置。</returns>
        public UnityLogKitOptions CreateLogKitOptions()
        {
            EnsureLogKit();
            var options = mLogKit.Clone();
            options.Normalize();
            return options;
        }

        /// <summary>
        /// 尝试读取指定 Kit 配置值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">配置 key。</param>
        /// <param name="value">读取到的配置值。</param>
        /// <returns>存在配置值时返回 true。</returns>
        public bool TryGetValue(string kit, string key, out string value)
        {
            value = null;
            if (string.Equals(kit, LogKitSettings.KIT_NAME, StringComparison.Ordinal) &&
                TryGetLogKitValue(key, out value))
            {
                return true;
            }

            var entry = FindEntry(kit, key);
            if (entry == null)
                return false;

            value = entry.Value ?? string.Empty;
            return true;
        }

        /// <summary>
        /// 设置指定 Kit 配置值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">配置 key。</param>
        /// <param name="value">配置值。</param>
        public void SetValue(string kit, string key, string value)
        {
            if (string.Equals(kit, LogKitSettings.KIT_NAME, StringComparison.Ordinal) &&
                TrySetLogKitValue(key, value))
            {
                Normalize();
                return;
            }

            var entry = FindEntry(kit, key);
            if (entry == null)
            {
                entry = new()
                {
                    Kit = kit,
                    Key = key
                };
                mEntries.Add(entry);
            }

            entry.Value = value ?? string.Empty;
        }

        /// <summary>
        /// 移除指定 Kit 配置值。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="key">配置 key。</param>
        public void RemoveValue(string kit, string key)
        {
            for (var i = mEntries.Count - 1; i >= 0; i--)
            {
                var entry = mEntries[i];
                if (entry != null &&
                    string.Equals(entry.Kit, kit, StringComparison.Ordinal) &&
                    string.Equals(entry.Key, key, StringComparison.Ordinal))
                {
                    mEntries.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// 修正设置资产中的非法或缺失值。
        /// </summary>
        public void Normalize()
        {
            EnsureLogKit();
            mLogKit.Normalize();
            if (mEntries == null)
                mEntries = new();
        }

        private void EnsureLogKit()
        {
            if (mLogKit == null)
                mLogKit = UnityLogKitOptions.CreateDefault();
        }

        private YokiFrameRuntimeSettingEntry FindEntry(string kit, string key)
        {
            if (mEntries == null)
                mEntries = new();

            for (var i = 0; i < mEntries.Count; i++)
            {
                var entry = mEntries[i];
                if (entry == null)
                    continue;

                if (string.Equals(entry.Kit, kit, StringComparison.Ordinal) &&
                    string.Equals(entry.Key, key, StringComparison.Ordinal))
                {
                    return entry;
                }
            }

            return null;
        }

        private bool TryGetLogKitValue(string key, out string value)
        {
            EnsureLogKit();
            value = null;
            switch (key)
            {
                case LogKitSettings.SAVE_LOG_IN_EDITOR_KEY:
                    value = BoolText(mLogKit.SaveLogInEditor);
                    return true;
                case LogKitSettings.SAVE_LOG_IN_PLAYER_KEY:
                    value = BoolText(mLogKit.SaveLogInPlayer);
                    return true;
                case LogKitSettings.ENABLE_IMGUI_IN_PLAYER_KEY:
                    value = BoolText(mLogKit.EnableIMGUIInPlayer);
                    return true;
                case LogKitSettings.ENABLE_ENCRYPTION_KEY:
                    value = BoolText(mLogKit.EnableEncryption);
                    return true;
                case LogKitSettings.MAX_QUEUE_SIZE_KEY:
                    value = IntText(mLogKit.MaxQueueSize);
                    return true;
                case LogKitSettings.MAX_SAME_LOG_COUNT_KEY:
                    value = IntText(mLogKit.MaxSameLogCount);
                    return true;
                case LogKitSettings.MAX_RETENTION_DAYS_KEY:
                    value = IntText(mLogKit.MaxRetentionDays);
                    return true;
                case LogKitSettings.MAX_FILE_SIZE_MB_KEY:
                    value = IntText(mLogKit.MaxFileSizeMB);
                    return true;
                case LogKitSettings.IMGUI_MAX_LOG_COUNT_KEY:
                    value = IntText(mLogKit.IMGUIMaxLogCount);
                    return true;
                case LogKitSettings.LOG_DIRECTORY_KEY:
                    value = mLogKit.LogDirectory ?? string.Empty;
                    return true;
                case LogKitSettings.EDITOR_FILE_NAME_KEY:
                    value = mLogKit.EditorFileName ?? string.Empty;
                    return true;
                case LogKitSettings.PLAYER_FILE_NAME_KEY:
                    value = mLogKit.PlayerFileName ?? string.Empty;
                    return true;
                default:
                    return false;
            }
        }

        private bool TrySetLogKitValue(string key, string value)
        {
            EnsureLogKit();
            switch (key)
            {
                case LogKitSettings.SAVE_LOG_IN_EDITOR_KEY:
                    mLogKit.SaveLogInEditor = ParseBool(value, mLogKit.SaveLogInEditor);
                    return true;
                case LogKitSettings.SAVE_LOG_IN_PLAYER_KEY:
                    mLogKit.SaveLogInPlayer = ParseBool(value, mLogKit.SaveLogInPlayer);
                    return true;
                case LogKitSettings.ENABLE_IMGUI_IN_PLAYER_KEY:
                    mLogKit.EnableIMGUIInPlayer = ParseBool(value, mLogKit.EnableIMGUIInPlayer);
                    return true;
                case LogKitSettings.ENABLE_ENCRYPTION_KEY:
                    mLogKit.EnableEncryption = ParseBool(value, mLogKit.EnableEncryption);
                    return true;
                case LogKitSettings.MAX_QUEUE_SIZE_KEY:
                    mLogKit.MaxQueueSize = ParseInt(value, mLogKit.MaxQueueSize, 1, int.MaxValue);
                    return true;
                case LogKitSettings.MAX_SAME_LOG_COUNT_KEY:
                    mLogKit.MaxSameLogCount = ParseInt(value, mLogKit.MaxSameLogCount, 0, int.MaxValue);
                    return true;
                case LogKitSettings.MAX_RETENTION_DAYS_KEY:
                    mLogKit.MaxRetentionDays = ParseInt(value, mLogKit.MaxRetentionDays, 1, int.MaxValue);
                    return true;
                case LogKitSettings.MAX_FILE_SIZE_MB_KEY:
                    mLogKit.MaxFileSizeMB = ParseInt(value, mLogKit.MaxFileSizeMB, 1, int.MaxValue);
                    return true;
                case LogKitSettings.IMGUI_MAX_LOG_COUNT_KEY:
                    mLogKit.IMGUIMaxLogCount = ParseInt(value, mLogKit.IMGUIMaxLogCount, 1, int.MaxValue);
                    return true;
                case LogKitSettings.LOG_DIRECTORY_KEY:
                    mLogKit.LogDirectory = value ?? string.Empty;
                    return true;
                case LogKitSettings.EDITOR_FILE_NAME_KEY:
                    mLogKit.EditorFileName = string.IsNullOrEmpty(value) ? LogKitSettings.DEFAULT_EDITOR_FILE_NAME : value;
                    return true;
                case LogKitSettings.PLAYER_FILE_NAME_KEY:
                    mLogKit.PlayerFileName = string.IsNullOrEmpty(value) ? LogKitSettings.DEFAULT_PLAYER_FILE_NAME : value;
                    return true;
                default:
                    return false;
            }
        }

        private static bool ParseBool(string value, bool fallback)
        {
            bool parsed;
            return bool.TryParse(value, out parsed) ? parsed : fallback;
        }

        private static int ParseInt(string value, int fallback, int min, int max)
        {
            int parsed;
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out parsed))
                return fallback;

            if (parsed < min)
                return min;
            if (parsed > max)
                return max;
            return parsed;
        }

        private static string BoolText(bool value)
        {
            return value ? "true" : "false";
        }

        private static string IntText(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

#if UNITY_EDITOR
        private static YokiFrameRuntimeSettings LoadOrCreateInEditor()
        {
            var settings = AssetDatabase.LoadAssetAtPath<YokiFrameRuntimeSettings>(ASSET_PATH);
            if (settings == null)
            {
                settings = CreateInstance<YokiFrameRuntimeSettings>();
                settings.Normalize();

                if (!Directory.Exists(ASSET_DIRECTORY))
                {
                    Directory.CreateDirectory(ASSET_DIRECTORY);
                    AssetDatabase.Refresh();
                }

                AssetDatabase.CreateAsset(settings, ASSET_PATH);
                AssetDatabase.SaveAssets();
            }

            settings.Normalize();
            return settings;
        }
#endif
    }
}
#endif
