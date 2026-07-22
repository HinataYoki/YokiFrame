#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 从当前 Unity 项目的 Resources JSON 加载运行时配置，并转换为宿主无关 Store。
    /// </summary>
    internal static class UnityYokiFrameRuntimeSettingsLoader
    {
        internal const int FORMAT_VERSION = 1;
        internal const string RESOURCE_PATH = "YokiFrame/runtime-settings";

        /// <summary>
        /// 加载当前项目的运行时配置；文件不存在时返回空 Store，使各 Kit 使用代码默认值。
        /// </summary>
        /// <param name="store">加载成功或回退时返回的项目隔离 Store。</param>
        /// <param name="errorMessage">配置损坏时返回诊断；缺失文件不视为错误。</param>
        /// <returns>文件缺失或配置有效时返回 true。</returns>
        internal static bool TryLoad(out YokiFrameRuntimeSettingsStore store, out string errorMessage)
        {
            TextAsset settingsAsset = Resources.Load<TextAsset>(RESOURCE_PATH);
            if (settingsAsset == null)
            {
                store = new YokiFrameRuntimeSettingsStore();
                errorMessage = string.Empty;
            }
            else
            {
                string json = settingsAsset.text;
                Resources.UnloadAsset(settingsAsset);
                if (!TryParse(json, out store, out errorMessage)) return false;
            }

#if UNITY_EDITOR
            return UnityYokiFrameRuntimeSettingsEditorOverlay.TryApply(store, out errorMessage);
#else
            return true;
#endif
        }

        /// <summary>
        /// 解析统一 Unity Runtime Settings JSON；全部条目验证通过后才提交 Store，避免半应用配置。
        /// </summary>
        /// <param name="json">待解析 JSON。</param>
        /// <param name="store">解析成功时返回完整 Store，失败时返回空 Store。</param>
        /// <param name="errorMessage">解析或校验失败诊断。</param>
        /// <returns>配置可以安全应用时返回 true。</returns>
        internal static bool TryParse(
            string json,
            out YokiFrameRuntimeSettingsStore store,
            out string errorMessage)
        {
            store = new YokiFrameRuntimeSettingsStore();
            if (string.IsNullOrWhiteSpace(json))
            {
                errorMessage = "YokiFrame runtime settings JSON 不能为空。";
                return false;
            }

            UnityYokiFrameRuntimeSettingsDocument document;
            try
            {
                document = JsonUtility.FromJson<UnityYokiFrameRuntimeSettingsDocument>(json);
            }
            catch (ArgumentException exception)
            {
                errorMessage = "YokiFrame runtime settings JSON 解析失败: " + exception.Message;
                return false;
            }

            return TryBuildStore(document, out store, out errorMessage);
        }

        /// <summary>
        /// 校验格式版本和全部稀疏设置，成功后一次性返回可注入 Core 的 Store。
        /// </summary>
        /// <param name="document">Unity JsonUtility 解析出的宿主 DTO。</param>
        /// <param name="store">校验成功时返回设置 Store。</param>
        /// <param name="errorMessage">校验失败诊断。</param>
        /// <returns>全部字段有效时返回 true。</returns>
        private static bool TryBuildStore(
            UnityYokiFrameRuntimeSettingsDocument document,
            out YokiFrameRuntimeSettingsStore store,
            out string errorMessage)
        {
            store = new YokiFrameRuntimeSettingsStore();
            if (document == null || document.formatVersion != FORMAT_VERSION)
            {
                errorMessage = "YokiFrame runtime settings formatVersion 必须为 1。";
                return false;
            }

            UnityYokiFrameRuntimeSettingEntry[] settings = document.settings ?? Array.Empty<UnityYokiFrameRuntimeSettingEntry>();
            YokiFrameRuntimeSettingsStore candidate = new();
            for (var index = 0; index < settings.Length; index++)
            {
                if (!TryApplyEntry(candidate, settings[index], index, out errorMessage))
                {
                    return false;
                }
            }

            store = candidate;
            errorMessage = string.Empty;
            return true;
        }

        /// <summary>
        /// 校验并应用单个条目；重复 Kit/key 按文件顺序由后值覆盖前值。
        /// </summary>
        /// <param name="store">尚未对外发布的候选 Store。</param>
        /// <param name="entry">当前 JSON 条目。</param>
        /// <param name="index">条目索引，用于错误定位。</param>
        /// <param name="errorMessage">失败诊断。</param>
        /// <returns>条目有效并已写入时返回 true。</returns>
        private static bool TryApplyEntry(
            YokiFrameRuntimeSettingsStore store,
            UnityYokiFrameRuntimeSettingEntry entry,
            int index,
            out string errorMessage)
        {
            if (entry == null)
            {
                errorMessage = "YokiFrame runtime settings 包含空条目，索引: " + index;
                return false;
            }

            try
            {
                store.SetValue(entry.kit, entry.key, entry.value);
                errorMessage = string.Empty;
                return true;
            }
            catch (ArgumentException exception)
            {
                errorMessage = "YokiFrame runtime settings 条目标识无效，索引 " + index + ": " + exception.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// Unity JsonUtility 使用的运行时配置文档 DTO；只存在于 Unity Adapter。
    /// </summary>
    [Serializable]
    internal sealed class UnityYokiFrameRuntimeSettingsDocument
    {
        public int formatVersion;
        public UnityYokiFrameRuntimeSettingEntry[] settings;
    }

    /// <summary>
    /// Unity JsonUtility 使用的稀疏设置条目 DTO。
    /// </summary>
    [Serializable]
    internal sealed class UnityYokiFrameRuntimeSettingEntry
    {
        public string kit;
        public string key;
        public string value;
    }
}
#endif
