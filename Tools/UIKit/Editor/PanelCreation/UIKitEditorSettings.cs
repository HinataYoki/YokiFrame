#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 在当前 Unity 项目内共享 UIKit 面板代码生成参数。
    /// </summary>
    internal static class UIKitEditorSettings
    {
        private const string PREFS_PREFIX = "YokiFrame.UIKit.EditorSettings.";
        private const string PREFAB_FOLDER_KEY = "PrefabFolder";
        private const string SCRIPT_FOLDER_KEY = "ScriptFolder";
        private const string SCRIPT_NAMESPACE_KEY = "ScriptNamespace";
        private const string ASSEMBLY_NAME_KEY = "AssemblyName";
        private const string CODE_TEMPLATE_KEY = "CodeTemplate";

        /// <summary>
        /// 使用当前项目保存的参数创建面板生成请求。
        /// </summary>
        public static UIKitPanelCreateRequest CreateRequest(string panelName)
        {
            var request = new UIKitPanelCreateRequest
            {
                PanelName = panelName,
                PrefabFolder = GetString(PREFAB_FOLDER_KEY, UIKitPanelPrefabCreator.DEFAULT_PREFAB_FOLDER),
                ScriptFolder = GetString(SCRIPT_FOLDER_KEY, UIKitPanelPrefabCreator.DEFAULT_SCRIPT_FOLDER),
                ScriptNamespace = GetString(SCRIPT_NAMESPACE_KEY, UIKitPanelPrefabCreator.DEFAULT_SCRIPT_NAMESPACE),
                AssemblyName = GetString(ASSEMBLY_NAME_KEY, UIKitPanelPrefabCreator.DEFAULT_ASSEMBLY_NAME),
                CodeTemplate = GetString(CODE_TEMPLATE_KEY, UIKitPanelPrefabCreator.DEFAULT_CODE_TEMPLATE)
            };

            try
            {
                UIKitPanelPrefabCreator.NormalizeEditorSettings(request);
                return request;
            }
            catch (InvalidOperationException)
            {
                return CreateDefaultRequest(panelName);
            }
        }

        /// <summary>
        /// 校验并保存工作台传入的当前项目生成参数。
        /// </summary>
        public static void Save(UIKitPanelCreateRequest request)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));

            UIKitPanelPrefabCreator.NormalizeEditorSettings(request);
            SetString(PREFAB_FOLDER_KEY, request.PrefabFolder);
            SetString(SCRIPT_FOLDER_KEY, request.ScriptFolder);
            SetString(SCRIPT_NAMESPACE_KEY, request.ScriptNamespace);
            SetString(ASSEMBLY_NAME_KEY, request.AssemblyName);
            SetString(CODE_TEMPLATE_KEY, request.CodeTemplate);
        }

        /// <summary>
        /// 返回当前项目配置下的面板用户脚本路径。
        /// </summary>
        public static string GetPanelScriptPath(string panelName)
        {
            if (string.IsNullOrEmpty(panelName))
                throw new ArgumentException("Panel 名称不能为空。", nameof(panelName));

            var request = CreateRequest(panelName);
            return request.ScriptFolder.TrimEnd('/') + "/" + panelName + "/" + panelName + ".cs";
        }

        /// <summary>
        /// 创建不依赖已保存数据的默认请求。
        /// </summary>
        private static UIKitPanelCreateRequest CreateDefaultRequest(string panelName)
        {
            var request = new UIKitPanelCreateRequest { PanelName = panelName };
            request.ApplyDefaults();
            return request;
        }

        /// <summary>
        /// 读取当前项目作用域内的字符串设置。
        /// </summary>
        private static string GetString(string field, string fallback)
        {
            return EditorPrefs.GetString(GetProjectKey(field), fallback);
        }

        /// <summary>
        /// 写入当前项目作用域内的字符串设置。
        /// </summary>
        private static void SetString(string field, string value)
        {
            EditorPrefs.SetString(GetProjectKey(field), value);
        }

        /// <summary>
        /// 使用项目 Assets 绝对路径隔离不同项目的编辑器参数。
        /// </summary>
        private static string GetProjectKey(string field)
        {
            return PREFS_PREFIX + Application.dataPath.Replace('\\', '/') + "." + field;
        }
    }
}
#endif
