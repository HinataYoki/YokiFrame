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
            if (!TryResolvePrefabContext(out _, out GameObject prefab, out _))
                return;
            UIPanel panel = prefab.GetComponent<UIPanel>();
            if (panel != default) UIKitGeneratedOwnerCodeService.OpenScript(panel);
        }

        /// <summary>以具体 Panel 脚本恢复输出根，项目默认值变化不能隐式搬移用户代码。</summary>
        private static UIKitPanelCodeLayout CreateCodeLayout(GameObject prefab, string prefabPath)
        {
            UIPanel panel = prefab.GetComponent<UIPanel>();
            return UIKitPanelCodeLayout.FromScript(panel.GetType(),
                UIKitGeneratedOwnerCodeService.GetScriptPath(panel), prefabPath);
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
