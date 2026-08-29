#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Bind 快捷键；Panel 创建统一由 Workbench 发起，代码生成由 Workbench 或 owner Inspector 发起。
    /// </summary>
    internal static class UIKitBindShortcuts
    {
        /// <summary>使用 Alt+B 为当前选择批量添加 Bind。</summary>
        [MenuItem("Edit/UIKit/Add Bind Component &b", false, 100)]
        private static void AddBind()
        {
            Execute(UIKitPanelPrefabService.AddBindToSelection);
        }

        /// <summary>有 GameObject 选择时启用添加入口。</summary>
        [MenuItem("Edit/UIKit/Add Bind Component &b", true)]
        private static bool CanAddBind()
        {
            return Selection.gameObjects != null && Selection.gameObjects.Length > 0;
        }

        /// <summary>使用 Alt+Ctrl+B 从当前选择批量移除 Bind。</summary>
        [MenuItem("Edit/UIKit/Remove Bind Component &%b", false, 101)]
        private static void RemoveBind()
        {
            Execute(UIKitPanelPrefabService.RemoveBindFromSelection);
        }

        /// <summary>任一选择包含 Bind 时启用移除入口。</summary>
        [MenuItem("Edit/UIKit/Remove Bind Component &%b", true)]
        private static bool CanRemoveBind()
        {
            GameObject[] selected = Selection.gameObjects;
            if (selected == null) return false;
            for (var index = 0; index < selected.Length; index++)
            {
                if (selected[index] != default && selected[index].GetComponent<Bind>() != default)
                    return true;
            }

            return false;
        }

        /// <summary>统一捕获菜单异常并在 Console 和对话框中报告。</summary>
        private static void Execute(Func<string> action)
        {
            try
            {
                string result = action();
                Debug.Log("[UIKit] " + result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("UIKit", exception.Message, "OK");
            }
        }
    }
}
#endif
