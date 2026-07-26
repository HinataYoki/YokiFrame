#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>读取 Workbench 保存的 UIKit Editor Tools 配置，供 Unity Inspector 生成入口复用。</summary>
    internal static class UIKitEditorSettingsReader
    {
        private const string SETTINGS_RELATIVE_PATH =
            "ProjectSettings/Packages/com.hinatayoki.yokiframe/editor-settings.json";
        private const string UIKIT_KIT = "UIKit";
        private const string PREFAB_FOLDER_KEY = "editor.prefabFolder";
        private const string SCRIPT_FOLDER_KEY = "editor.scriptFolder";
        private const string SCRIPT_NAMESPACE_KEY = "editor.scriptNamespace";
        private const string ASSEMBLY_NAME_KEY = "editor.assemblyName";
        private const string CODE_TEMPLATE_KEY = "editor.codeTemplate";

        private static string sCachedPath;
        private static DateTime sCachedWriteTime;
        private static SettingsDocument sCachedDocument;
        private static bool sCachedResult;

        /// <summary>读取当前项目配置并覆盖生成请求默认值；文件缺失或无效时保留代码默认值。</summary>
        /// <param name="request">待应用项目默认值的生成请求。</param>
        internal static void ApplyTo(UIKitPanelGenerationRequest request)
        {
            if (request == default || !TryRead(out SettingsDocument document)) return;
            SettingsEntry[] entries = document.settings ?? Array.Empty<SettingsEntry>();
            for (var index = 0; index < entries.Length; index++)
            {
                SettingsEntry entry = entries[index];
                if (entry == default
                    || !string.Equals(entry.kit, UIKIT_KIT, StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(entry.value)) continue;
                ApplyEntry(request, entry);
            }
        }

        /// <summary>把单个 UIKit 配置键映射到生成请求，忽略未知键以兼容后续配置扩展。</summary>
        /// <param name="request">待更新的生成请求。</param>
        /// <param name="entry">统一项目配置条目。</param>
        private static void ApplyEntry(UIKitPanelGenerationRequest request, SettingsEntry entry)
        {
            string value = entry.value.Trim();
            switch (entry.key)
            {
                case PREFAB_FOLDER_KEY:
                    request.prefabFolder = value;
                    break;
                case SCRIPT_FOLDER_KEY:
                    request.scriptFolder = value;
                    break;
                case SCRIPT_NAMESPACE_KEY:
                    request.scriptNamespace = value;
                    break;
                case ASSEMBLY_NAME_KEY:
                    request.assemblyName = value;
                    break;
                case CODE_TEMPLATE_KEY:
                    request.codeTemplate = value;
                    break;
            }
        }

        /// <summary>读取并校验当前项目的统一 Editor 配置 JSON；命中内存缓存时跳过磁盘 I/O。</summary>
        /// <param name="document">解析成功时返回配置文档。</param>
        /// <returns>文件存在且格式版本有效时返回 true。</returns>
        private static bool TryRead(out SettingsDocument document)
        {
            string path = ResolveSettingsPath();
            DateTime writeTime = File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : DateTime.MinValue;
            if (sCachedPath != null
                && string.Equals(sCachedPath, path, StringComparison.Ordinal)
                && writeTime == sCachedWriteTime)
            {
                document = sCachedDocument;
                return sCachedResult;
            }

            bool result = TryReadCore(path, out document);
            sCachedPath = path;
            sCachedWriteTime = writeTime;
            sCachedDocument = document;
            sCachedResult = result;
            return result;
        }

        /// <summary>执行实际磁盘读取与格式校验，不访问缓存。</summary>
        /// <param name="path">配置文件绝对路径。</param>
        /// <param name="document">解析成功时返回配置文档。</param>
        /// <returns>文件存在且格式版本有效时返回 true。</returns>
        private static bool TryReadCore(string path, out SettingsDocument document)
        {
            document = default;
            if (!File.Exists(path)) return false;
            try
            {
                string json = File.ReadAllText(path, Encoding.UTF8);
                document = JsonUtility.FromJson<SettingsDocument>(json);
                return document != default && document.formatVersion == 1;
            }
            catch (Exception exception) when (
                exception is IOException
                || exception is UnauthorizedAccessException
                || exception is ArgumentException)
            {
                document = default;
                return false;
            }
        }

        /// <summary>解析固定项目 Editor 配置路径，避免把配置读取到其它项目。</summary>
        /// <returns>当前 Unity 项目的 Editor 配置绝对路径。</returns>
        private static string ResolveSettingsPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(
                projectRoot,
                SETTINGS_RELATIVE_PATH.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>JsonUtility 使用的统一 Editor 配置文档。</summary>
        [Serializable]
        private sealed class SettingsDocument
        {
            public int formatVersion;
            public SettingsEntry[] settings;
        }

        /// <summary>JsonUtility 使用的稀疏配置条目。</summary>
        [Serializable]
        private sealed class SettingsEntry
        {
            public string kit;
            public string key;
            public string value;
        }
    }
}
#endif
