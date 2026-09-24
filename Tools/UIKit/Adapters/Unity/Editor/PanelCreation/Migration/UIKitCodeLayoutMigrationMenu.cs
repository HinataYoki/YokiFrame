#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>提供 UIKit 旧代码目录迁移的用户主动入口；生成和转换仍可独立自动处理可确认遗留文件。</summary>
    internal static class UIKitCodeLayoutMigrationMenu
    {
        private const string MENU_PATH = "Edit/UIKit/Migrate Code Layout";

        /// <summary>显示迁移预览，并在用户确认后启动可回滚的目录迁移事务。</summary>
        [MenuItem(MENU_PATH, false, 120)]
        private static void MigrateCodeLayout()
        {
            string root = ResolveRootForMigration();
            if (string.IsNullOrEmpty(root)) return;
            try
            {
                UIKitCodeLayoutMigration.RequireIdle();
                UIKitCodeLayoutMigrationPlan plan = UIKitCodeLayoutMigration.Build(root);
                if (plan.Moves.Count == 0)
                {
                    EditorUtility.DisplayDialog(
                        "UIKit 代码目录迁移",
                        "选定的共同脚本根目录已经符合当前布局，没有需要迁移的文件。",
                        "确定");
                    return;
                }

                if (!EditorUtility.DisplayDialog(
                        "UIKit 代码目录迁移",
                        plan.Describe() + "\n\n迁移会保留源码字节、脚本 GUID、命名空间和程序集；是否继续？",
                        "执行迁移",
                        "取消"))
                    return;

                UIKitCodeLayoutMigrationTransaction.Execute(plan);
                Debug.Log("UIKit 代码目录迁移已启动，编译和脚本身份验证完成后会报告最终结果。\n" + plan.Describe());
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("UIKit 代码目录迁移失败", exception.Message, "确定");
                LogKit.Exception(exception);
            }
        }

        /// <summary>保持菜单在编辑器忙或已有事务时禁用，避免两个文件事务互相覆盖。</summary>
        [MenuItem(MENU_PATH, true)]
        private static bool ValidateMigrateCodeLayout()
        {
            return !EditorApplication.isCompiling
                && !EditorApplication.isUpdating
                && !EditorUtility.scriptCompilationFailed
                && !UIKitConversionTransaction.IsPending
                && !UIKitCodeLayoutMigrationTransaction.IsPending;
        }

        /// <summary>优先使用 Project 窗口选中的文件夹；未选中时让用户选择共同脚本根目录。</summary>
        private static string ResolveRootForMigration()
        {
            UnityEngine.Object selected = Selection.activeObject;
            string selectedPath = selected == null ? string.Empty : AssetDatabase.GetAssetPath(selected);
            if (!string.IsNullOrEmpty(selectedPath) && AssetDatabase.IsValidFolder(selectedPath))
                return selectedPath.Replace('\\', '/').TrimEnd('/');

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string selectedAbsolutePath = EditorUtility.OpenFolderPanel(
                "选择 UIKit 共同脚本根目录",
                projectRoot,
                string.Empty);
            if (string.IsNullOrEmpty(selectedAbsolutePath)) return string.Empty;
            try
            {
                string assetPath = UIKitPanelCodeLayout.ToAssetPath(selectedAbsolutePath);
                if (!UIKitPanelCodeLayout.IsUnityAssetPath(assetPath)
                    || !AssetDatabase.IsValidFolder(assetPath))
                    throw new InvalidOperationException("请选择当前项目 Assets 或 Packages 内的共同脚本根目录。");
                return assetPath;
            }
            catch (Exception exception)
            {
                EditorUtility.DisplayDialog("UIKit 代码目录迁移", exception.Message, "确定");
                return string.Empty;
            }
        }
    }
}
#endif
