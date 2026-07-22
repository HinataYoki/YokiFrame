#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 提供一次确认即可创建 Panel Prefab 与代码的 Unity Editor 窗口。
    /// </summary>
    internal sealed class UIKitPanelCreatorWindow : EditorWindow
    {
        private UIKitPanelGenerationRequest mRequest;
        private string mStatus = string.Empty;
        private IReadOnlyList<string> mTemplateNames;
        private string[] mTemplateOptions = Array.Empty<string>();

        /// <summary>打开或聚焦固定类型的 Panel 创建窗口。</summary>
        internal static void Open()
        {
            UIKitPanelCreatorWindow window = GetWindow<UIKitPanelCreatorWindow>(true, "UIKit Panel", true);
            window.minSize = new Vector2(430f, 260f);
            window.Show();
        }

        /// <summary>初始化当前窗口的稳定默认请求。</summary>
        private void OnEnable()
        {
            if (mRequest == null) mRequest = UIKitPanelGenerationRequest.CreateDefault();
        }

        /// <summary>绘制创建参数和唯一提交按钮。</summary>
        private void OnGUI()
        {
            if (mRequest == null) mRequest = UIKitPanelGenerationRequest.CreateDefault();
            mRequest.panelName = EditorGUILayout.TextField("Panel Name", mRequest.panelName);
            mRequest.prefabFolder = EditorGUILayout.TextField("Prefab Folder", mRequest.prefabFolder);
            mRequest.scriptFolder = EditorGUILayout.TextField("Script Folder", mRequest.scriptFolder);
            mRequest.scriptNamespace = EditorGUILayout.TextField("Namespace", mRequest.scriptNamespace);
            mRequest.assemblyName = EditorGUILayout.TextField("Assembly", mRequest.assemblyName);
            IReadOnlyList<string> templates = GetTemplateNames();
            int templateIndex = 0;
            for (var index = 0; index < templates.Count; index++)
            {
                if (string.Equals(templates[index], mRequest.codeTemplate, StringComparison.Ordinal))
                    templateIndex = index;
            }

            templateIndex = EditorGUILayout.Popup("Template", templateIndex, mTemplateOptions);
            if (templateIndex >= 0 && templateIndex < templates.Count)
                mRequest.codeTemplate = templates[templateIndex];
            GUILayout.Space(10f);
            if (GUILayout.Button("Create Panel Prefab", GUILayout.Height(30f))) CreatePanel();
            if (!string.IsNullOrWhiteSpace(mStatus)) EditorGUILayout.HelpBox(mStatus, MessageType.Info);
        }

        /// <summary>仅在模板注册表快照变化时重建 Popup 数组，避免每次 OnGUI 产生临时分配。</summary>
        private IReadOnlyList<string> GetTemplateNames()
        {
            IReadOnlyList<string> templates = UIKitCodeTemplateRegistry.GetTemplateNames();
            if (ReferenceEquals(templates, mTemplateNames)) return templates;
            mTemplateNames = templates;
            mTemplateOptions = new string[templates.Count];
            for (var index = 0; index < templates.Count; index++)
                mTemplateOptions[index] = templates[index];
            return templates;
        }

        /// <summary>提交当前请求并报告结构化成功或失败状态。</summary>
        private void CreatePanel()
        {
            try
            {
                string payload = JsonUtility.ToJson(mRequest);
                string result = UIKitPanelPrefabService.CreatePanelPrefab(payload);
                mStatus = "Created: " + mRequest.panelName;
                Debug.Log("[UIKit] " + result);
            }
            catch (Exception exception)
            {
                mStatus = exception.Message;
                Debug.LogException(exception);
            }
        }
    }
}
#endif
