#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace YokiFrame
{
    internal sealed partial class UIKitPanelInspector
    {
        /// <summary>为当前 Prefab 内容生成代码并登记编译后引用回填。</summary>
        private void GenerateUICode()
        {
            if (!TryResolvePrefabContext(out GameObject scanRoot, out GameObject prefab, out string prefabPath))
            {
                EditorUtility.DisplayDialog(
                    "无法生成 UI 代码",
                    "请在 Prefab 资源、Prefab Stage 或 Prefab 实例上使用生成入口。",
                    "确定");
                return;
            }
            try
            {
                UIKitPanelCodeLayout layout = CreateCodeLayout(prefab, prefabPath);
                UIKitPanelPrefabService.GenerateForPrefab(layout, scanRoot);
                RefreshBindingTree();
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("生成失败", exception.Message, "确定");
                LogKit.Exception(exception);
            }
        }

        /// <summary>打开当前 Prefab 对应的用户 Panel 脚本。</summary>
        private void OpenPanelScript()
        {
            if (!TryResolvePrefabContext(out _, out GameObject prefab, out string prefabPath))
                return;
            UIKitPanelCodeLayout layout = CreateCodeLayout(prefab, prefabPath);
            UnityEngine.Object script = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(layout.PanelScriptPath);
            if (script != default)
            {
                AssetDatabase.OpenAsset(script);
                return;
            }
            EditorUtility.DisplayDialog(
                "脚本不存在",
                "尚未找到脚本文件：\n" + layout.PanelScriptPath,
                "确定");
        }

        /// <summary>使用当前默认设置和真实 Prefab 路径创建安全代码布局。</summary>
        private static UIKitPanelCodeLayout CreateCodeLayout(GameObject prefab, string prefabPath)
        {
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.CreateDefault(prefab.name);
            request.prefabPath = prefabPath;
            return new UIKitPanelCodeLayout(request);
        }

        /// <summary>解析 Prefab Stage、Prefab 资产或场景 Prefab 实例的扫描根和资产路径。</summary>
        private bool TryResolvePrefabContext(
            out GameObject scanRoot,
            out GameObject prefab,
            out string prefabPath)
        {
            scanRoot = default;
            prefab = default;
            prefabPath = string.Empty;
            UIPanel panel = target as UIPanel;
            if (panel == default)
                return false;
            PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (IsPanelInPrefabStage(panel, stage))
            {
                if (panel.gameObject != stage.prefabContentsRoot)
                    return false;
                scanRoot = stage.prefabContentsRoot;
                prefabPath = stage.assetPath;
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
                return prefab != default;
            }
            prefabPath = AssetDatabase.GetAssetPath(panel.gameObject);
            if (string.IsNullOrEmpty(prefabPath))
            {
                GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(panel.gameObject);
                if (instanceRoot != panel.gameObject)
                    return false;
                prefabPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject);
            }
            if (string.IsNullOrEmpty(prefabPath))
                return false;
            prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            scanRoot = prefab;
            return prefab != default && prefab.GetComponent(panel.GetType()) != default;
        }

        /// <summary>判断当前面板是否属于正在编辑的 Prefab Stage。</summary>
        private static bool IsPanelInPrefabStage(UIPanel panel, PrefabStage stage)
        {
            if (stage == null || stage.prefabContentsRoot == default)
                return false;
            if (panel.gameObject.scene != stage.prefabContentsRoot.scene)
                return false;
            Transform current = panel.transform;
            while (current != default)
            {
                if (current == stage.prefabContentsRoot.transform)
                    return true;
                current = current.parent;
            }
            return false;
        }
    }
}
#endif
