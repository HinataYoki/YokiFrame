#if UNITY_EDITOR
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 面板创建命令载荷。
    /// </summary>
    [Serializable]
    internal sealed class UIKitPanelCreateRequest
    {
        /// <summary>面板类型名。</summary>
        public string PanelName;

        /// <summary>生成脚本命名空间。</summary>
        public string ScriptNamespace;

        /// <summary>生成脚本根目录。</summary>
        public string ScriptFolder;

        /// <summary>Prefab 输出目录。</summary>
        public string PrefabFolder;

        /// <summary>可选的已有 Prefab 路径。</summary>
        public string PrefabPath;

        /// <summary>用户脚本所在程序集。</summary>
        public string AssemblyName;

        /// <summary>代码模板名称。</summary>
        public string CodeTemplate;

        /// <summary>是否覆盖已有 Prefab。</summary>
        public bool Overwrite;

        /// <summary>
        /// 从命令 JSON 构造请求。
        /// </summary>
        public static UIKitPanelCreateRequest FromJson(string payloadJson)
        {
            UIKitPanelCreateRequest request = new();
            if (!string.IsNullOrEmpty(payloadJson) && payloadJson.Trim() != "{}")
                JsonUtility.FromJsonOverwrite(payloadJson, request);

            request.ApplyDefaults();
            return request;
        }

        /// <summary>
        /// 应用命令未显式提供时的默认值。
        /// </summary>
        public void ApplyDefaults()
        {
            if (string.IsNullOrEmpty(ScriptNamespace))
                ScriptNamespace = UIKitPanelPrefabCreator.DEFAULT_SCRIPT_NAMESPACE;
            if (string.IsNullOrEmpty(ScriptFolder))
                ScriptFolder = UIKitPanelPrefabCreator.DEFAULT_SCRIPT_FOLDER;
            if (string.IsNullOrEmpty(PrefabFolder))
                PrefabFolder = UIKitPanelPrefabCreator.DEFAULT_PREFAB_FOLDER;
            if (string.IsNullOrEmpty(AssemblyName))
                AssemblyName = UIKitPanelPrefabCreator.DEFAULT_ASSEMBLY_NAME;
            CodeTemplate = UIKitPanelPrefabCreator.NormalizeCodeTemplateName(CodeTemplate);
        }

        /// <summary>
        /// 用已有 Prefab 名补齐请求默认值。
        /// </summary>
        public void ApplyDefaultsFromPrefab(string prefabName)
        {
            if (string.IsNullOrEmpty(PanelName))
                PanelName = prefabName;
            ApplyDefaults();
        }
    }
}
#endif
