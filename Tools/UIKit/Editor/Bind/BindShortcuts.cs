#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 快捷菜单和快捷键
    /// </summary>
    public static class BindShortcuts
    {
        #region 右键预制体生成 UI 代码

        /// <summary>
        /// 右键预制体：生成 UI 代码
        /// </summary>
        [MenuItem("Assets/UIKit - 生成 UI 代码", false, 100)]
        private static void GenerateUICode()
        {
            var prefab = Selection.activeGameObject;
            if (prefab == default) return;

            var ns = UIKitCreateConfig.Instance.ScriptNamespace;
            UICodeGenerator.DoCreateCode(prefab, ns);
        }

        [MenuItem("Assets/UIKit - 生成 UI 代码", true)]
        private static bool GenerateUICodeValidate()
        {
            var go = Selection.activeGameObject;
            if (go == default) return false;

            var prefabType = PrefabUtility.GetPrefabAssetType(go);
            return prefabType is PrefabAssetType.Regular or PrefabAssetType.Variant;
        }

        #endregion

        /// <summary>
        /// ALT+B: 为选中的 GameObject 添加 Bind 组件
        /// </summary>
        [MenuItem("Edit/UIKit/Add Bind Component &b", false, 100)]
        private static void AddBindToSelection()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[UIKit] 请先选中一个或多个 GameObject");
                return;
            }

            int addedCount = 0;
            int skippedCount = 0;

            foreach (var go in selectedObjects)
            {
                if (go == null) continue;

                // 检查是否已有 Bind 组件
                if (go.GetComponent<AbstractBind>() != null)
                {
                    skippedCount++;
                    continue;
                }

                Undo.AddComponent<Bind>(go);
                addedCount++;
            }

            // 输出结果
            if (addedCount > 0 && skippedCount > 0)
            {
                Debug.Log($"[UIKit] 已添加 {addedCount} 个 Bind 组件，跳过 {skippedCount} 个（已存在）");
            }
            else if (addedCount > 0)
            {
                Debug.Log($"[UIKit] 已添加 {addedCount} 个 Bind 组件");
            }
            else if (skippedCount > 0)
            {
                Debug.Log($"[UIKit] 选中的 {skippedCount} 个 GameObject 已有 Bind 组件");
            }
        }

        /// <summary>
        /// 验证菜单项是否可用
        /// </summary>
        [MenuItem("Edit/UIKit/Add Bind Component &b", true)]
        private static bool AddBindToSelectionValidate() => Selection.gameObjects.Length > 0;

        /// <summary>
        /// ALT+SHIFT+B: 移除选中 GameObject 的 Bind 组件
        /// </summary>
        [MenuItem("Edit/UIKit/Remove Bind Component &%b", false, 101)]
        private static void RemoveBindFromSelection()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                Debug.LogWarning("[UIKit] 请先选中一个或多个 GameObject");
                return;
            }

            int removedCount = 0;

            foreach (var go in selectedObjects)
            {
                if (go == null) continue;

                var bind = go.GetComponent<AbstractBind>();
                if (bind != null)
                {
                    Undo.DestroyObjectImmediate(bind);
                    removedCount++;
                }
            }

            if (removedCount > 0)
            {
                Debug.Log($"[UIKit] 已移除 {removedCount} 个 Bind 组件");
            }
            else
            {
                Debug.Log("[UIKit] 选中的 GameObject 没有 Bind 组件");
            }
        }

        /// <summary>
        /// 验证移除菜单项是否可用
        /// </summary>
        [MenuItem("Edit/UIKit/Remove Bind Component &%b", true)]
        private static bool RemoveBindFromSelectionValidate() => Selection.gameObjects.Length > 0;
    }
}
#endif
