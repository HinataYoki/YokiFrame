#if UNITY_EDITOR
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 描述一次 Panel Prefab 与代码生成请求；只在 Unity Editor 中使用。
    /// </summary>
    [Serializable]
    internal sealed class UIKitPanelGenerationRequest
    {
        internal const string DEFAULT_PREFAB_FOLDER = "Assets/Resources/Art/UIPrefab";
        internal const string DEFAULT_SCRIPT_FOLDER = "Assets/Scripts/UI";
        internal const string DEFAULT_NAMESPACE = "GameUI";
        internal const string DEFAULT_ASSEMBLY = "Assembly-CSharp";
        internal const string DEFAULT_TEMPLATE = UIKitCodeTemplateRegistry.DEFAULT_TEMPLATE_NAME;
        internal const string MINIMAL_TEMPLATE = UIKitCodeTemplateRegistry.MINIMAL_TEMPLATE_NAME;

        public string panelName;
        public string prefabFolder;
        public string scriptFolder;
        public string scriptNamespace;
        public string assemblyName;
        public string codeTemplate;
        public string prefabPath;
        public long expectedContextRevision;
        public string targetGlobalObjectId;

        /// <summary>创建包含稳定默认值的新请求。</summary>
        internal static UIKitPanelGenerationRequest CreateDefault(string panelName = null)
        {
            UIKitPanelGenerationRequest request = new()
            {
                panelName = panelName ?? string.Empty,
                prefabFolder = DEFAULT_PREFAB_FOLDER,
                scriptFolder = DEFAULT_SCRIPT_FOLDER,
                scriptNamespace = DEFAULT_NAMESPACE,
                assemblyName = DEFAULT_ASSEMBLY,
                codeTemplate = DEFAULT_TEMPLATE,
                prefabPath = string.Empty,
                expectedContextRevision = 0L,
                targetGlobalObjectId = string.Empty,
            };
            UIKitEditorSettingsReader.ApplyTo(request);
            return request;
        }

        /// <summary>使用 JsonUtility 解析命令 payload，并补齐缺失默认值。</summary>
        internal static UIKitPanelGenerationRequest FromJson(string payloadJson)
        {
            UIKitPanelGenerationRequest request = string.IsNullOrWhiteSpace(payloadJson)
                ? CreateDefault()
                : JsonUtility.FromJson<UIKitPanelGenerationRequest>(payloadJson);
            if (request == null) throw new ArgumentException("UIKit Editor payload 不是有效 JSON 对象。");
            request.ApplyDefaults();
            return request;
        }

        /// <summary>补齐空字段，不覆盖调用方显式值。</summary>
        internal void ApplyDefaults()
        {
            prefabFolder = DefaultIfEmpty(prefabFolder, DEFAULT_PREFAB_FOLDER);
            scriptFolder = DefaultIfEmpty(scriptFolder, DEFAULT_SCRIPT_FOLDER);
            scriptNamespace = DefaultIfEmpty(scriptNamespace, DEFAULT_NAMESPACE);
            assemblyName = DefaultIfEmpty(assemblyName, DEFAULT_ASSEMBLY);
            codeTemplate = DefaultIfEmpty(codeTemplate, DEFAULT_TEMPLATE);
            panelName = panelName == null ? string.Empty : panelName.Trim();
            prefabPath = prefabPath == null ? string.Empty : prefabPath.Trim();
            targetGlobalObjectId = targetGlobalObjectId == null ? string.Empty : targetGlobalObjectId.Trim();
        }

        /// <summary>返回首个非空值并去除首尾空白。</summary>
        private static string DefaultIfEmpty(string value, string fallback)
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }
    }
}
#endif
