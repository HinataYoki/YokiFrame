#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIKit Bind 快捷菜单和快捷键。
    /// </summary>
    public static class BindShortcuts
    {
        [MenuItem("Assets/UIKit - 生成 UI 代码", false, 100)]
        public static void GenerateUICode()
        {
            var prefab = Selection.activeGameObject;
            if (prefab == null)
                return;

            try
            {
                UIKitPanelPrefabCreator.GenerateCodeForPrefab(prefab, new UIKitPanelCreateRequest
                {
                    PanelName = prefab.name,
                    ScriptFolder = UIKitPanelPrefabCreator.DEFAULT_SCRIPT_FOLDER,
                    ScriptNamespace = UIKitPanelPrefabCreator.DEFAULT_SCRIPT_NAMESPACE,
                    AssemblyName = UIKitPanelPrefabCreator.DEFAULT_ASSEMBLY_NAME,
                    CodeTemplate = UIKitPanelPrefabCreator.DEFAULT_CODE_TEMPLATE
                });
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("生成失败", ex.Message, "确定");
                LogKit.Exception(ex);
            }
        }

        [MenuItem("Assets/UIKit - 生成 UI 代码", true)]
        public static bool CanGenerateUICode()
        {
            var go = Selection.activeGameObject;
            if (go == null)
                return false;

            var prefabType = PrefabUtility.GetPrefabAssetType(go);
            return prefabType == PrefabAssetType.Regular || prefabType == PrefabAssetType.Variant;
        }

        [MenuItem("Edit/UIKit/Add Bind Component &b", false, 100)]
        public static void AddBindToSelection()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                LogKit.Warning("[UIKit] 请先选中一个或多个 GameObject");
                return;
            }

            var addedCount = 0;
            var skippedCount = 0;
            for (var i = 0; i < selectedObjects.Length; i++)
            {
                var go = selectedObjects[i];
                if (go == null)
                    continue;

                if (go.GetComponent<AbstractBind>() != null)
                {
                    skippedCount++;
                    continue;
                }

                Undo.AddComponent<Bind>(go);
                addedCount++;
            }

            LogKit.Log("[UIKit] 已添加 " + addedCount + " 个 Bind 组件，跳过 " + skippedCount + " 个。");
        }

        [MenuItem("Edit/UIKit/Add Bind Component &b", true)]
        public static bool CanAddBindToSelection() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;

        [MenuItem("Edit/UIKit/Remove Bind Component &%b", false, 101)]
        public static void RemoveBindFromSelection()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                LogKit.Warning("[UIKit] 请先选中一个或多个 GameObject");
                return;
            }

            var removedCount = 0;
            for (var i = 0; i < selectedObjects.Length; i++)
            {
                var go = selectedObjects[i];
                if (go == null)
                    continue;

                var bind = go.GetComponent<AbstractBind>();
                if (bind == null)
                    continue;

                Undo.DestroyObjectImmediate(bind);
                removedCount++;
            }

            LogKit.Log("[UIKit] 已移除 " + removedCount + " 个 Bind 组件。");
        }

        [MenuItem("Edit/UIKit/Remove Bind Component &%b", true)]
        public static bool CanRemoveBindFromSelection() => Selection.gameObjects != null && Selection.gameObjects.Length > 0;
    }
}
#endif
