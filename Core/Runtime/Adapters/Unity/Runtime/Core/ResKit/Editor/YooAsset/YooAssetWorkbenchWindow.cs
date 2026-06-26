#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YokiFrame 的 YooAsset 资源工作台。
    /// </summary>
    public sealed partial class YooAssetWorkbenchWindow : EditorWindow
    {
        private const string WINDOW_TITLE = "YooAsset Workbench";
        private const string DEFAULT_PACKAGE_NAME = "DefaultPackage";
        private const string ENABLE_GROUP_RULE = "EnableGroup";
        private const string DISABLE_GROUP_RULE = "DisableGroup";

        private static readonly Dictionary<string, string> sRuleShortNames = new Dictionary<string, string>
        {
            { "MainAssetCollector", "主资源" },
            { "StaticAssetCollector", "静态" },
            { "DependAssetCollector", "依赖" },
            { "AddressDisable", "禁用寻址" },
            { "AddressByFileName", "按文件名" },
            { "AddressByFilePath", "按路径" },
            { "AddressByFolderAndFileName", "目录+文件名" },
            { "AddressByGroupAndFileName", "组+文件名" },
            { "PackDirectory", "按目录" },
            { "PackTopDirectory", "按顶层目录" },
            { "PackSeparately", "单独打包" },
            { "PackCollector", "按收集器" },
            { "PackGroup", "按分组" },
            { "PackRawFile", "原始文件" },
            { "PackShaderVariants", "着色器变体" },
            { "CollectAll", "全部" },
            { "CollectScene", "场景" },
            { "CollectPrefab", "预制体" },
            { "CollectSprite", "精灵" },
            { ENABLE_GROUP_RULE, "启用" },
            { DISABLE_GROUP_RULE, "禁用" },
            { "NormalIgnoreRule", "常规忽略" },
            { "RawFileIgnoreRule", "原始文件忽略" }
        };

        private int mSelectedPackageIndex;
        private int mSelectedGroupIndex;
        private bool mHasUnsavedChanges;
        private bool mPackageSettingsExpanded;
        private bool mGlobalSettingsExpanded;
        private int mExpandedCollectorIndex = -1;

        private VisualElement mWorkspace;
        private VisualElement mToolbar;
        private VisualElement mGlobalSettingsPanel;
        private VisualElement mPackageSettingsPanel;
        private VisualElement mGroupNav;
        private VisualElement mGroupList;
        private VisualElement mCollectorCanvas;
        private VisualElement mCollectorList;
        private DropdownField mPackageDropdown;
        private Label mUnsavedLabel;
        private Label mGroupSummaryLabel;
        private VisualElement mGroupCountBadge;
        private VisualElement mCollectorCountBadge;

        [MenuItem("YokiFrame/ResKit/YooAsset Workbench", false, 130)]
        public static void Open()
        {
            var window = GetWindow<YooAssetWorkbenchWindow>(WINDOW_TITLE);
            window.minSize = new Vector2(760f, 460f);
            window.Show();
            window.BuildWorkbench();
        }

        private void CreateGUI()
        {
            BuildWorkbench();
        }

        private void OnFocus()
        {
            if (mWorkspace != null)
            {
                RefreshAll();
            }
        }

        private void BuildWorkbench()
        {
            var root = rootVisualElement;
            root.Clear();
            root.AddToClassList("yoo-workbench-root");
            root.style.flexDirection = FlexDirection.Column;
            root.style.flexGrow = 1f;
            root.style.minHeight = 0f;
            root.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerSection);

            YokiStyleService.Apply(root, YokiStyleProfile.Full);
            YokiStyleService.ApplyKitStyleToElement(root, "ResKit");

            mWorkspace = new VisualElement();
            mWorkspace.name = "yoo-collector-workspace";
            mWorkspace.AddToClassList("yoki-kit-page");
            mWorkspace.style.flexDirection = FlexDirection.Column;
            mWorkspace.style.flexGrow = 1f;
            mWorkspace.style.minHeight = 0f;
            mWorkspace.style.paddingLeft = 12f;
            mWorkspace.style.paddingRight = 12f;
            mWorkspace.style.paddingTop = 12f;
            mWorkspace.style.paddingBottom = 12f;
            root.Add(mWorkspace);

            mWorkspace.Add(BuildHero());

            mToolbar = BuildToolbar();
            mToolbar.name = "yoo-collector-toolbar";
            mWorkspace.Add(mToolbar);

            mGlobalSettingsPanel = BuildGlobalSettingsPanel();
            mGlobalSettingsPanel.style.display = DisplayStyle.None;
            mWorkspace.Add(mGlobalSettingsPanel);

            mPackageSettingsPanel = BuildPackageSettingsPanel();
            mPackageSettingsPanel.style.display = DisplayStyle.None;
            mWorkspace.Add(mPackageSettingsPanel);

            var content = new VisualElement();
            content.AddToClassList("split-view");
            content.style.flexDirection = FlexDirection.Row;
            content.style.flexGrow = 1f;
            content.style.minHeight = 0f;
            content.style.marginTop = 10f;
            content.style.overflow = Overflow.Hidden;
            mWorkspace.Add(content);

            mGroupNav = BuildGroupNav();
            content.Add(mGroupNav);

            mCollectorCanvas = BuildCollectorCanvas();
            content.Add(mCollectorCanvas);

            EnsureDefaultPackage();
            RefreshAll();
        }

        private void RefreshAll()
        {
            EnsureDefaultPackage();
            RefreshPackageDropdown();
            RefreshPackageSettingsPanel();
            RefreshGlobalSettingsPanel();
            RefreshGroupNav();
            RefreshCollectorCanvas();
            RefreshDirtyState();
        }

        private void RefreshDirtyState()
        {
            if (mUnsavedLabel != null)
            {
                mUnsavedLabel.style.display = mHasUnsavedChanges ? DisplayStyle.Flex : DisplayStyle.None;
            }
        }

        private void MarkDirty()
        {
            mHasUnsavedChanges = true;
            RefreshDirtyState();
        }

        private void TogglePackageSettings()
        {
            mPackageSettingsExpanded = !mPackageSettingsExpanded;
            mGlobalSettingsExpanded = false;
            mPackageSettingsPanel.style.display = mPackageSettingsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            mGlobalSettingsPanel.style.display = DisplayStyle.None;
            if (mPackageSettingsExpanded)
            {
                RefreshPackageSettingsPanel();
            }
        }

        private void ToggleGlobalSettings()
        {
            mGlobalSettingsExpanded = !mGlobalSettingsExpanded;
            mPackageSettingsExpanded = false;
            mGlobalSettingsPanel.style.display = mGlobalSettingsExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            mPackageSettingsPanel.style.display = DisplayStyle.None;
            if (mGlobalSettingsExpanded)
            {
                RefreshGlobalSettingsPanel();
            }
        }

        private void OnPackageChanged(ChangeEvent<string> evt)
        {
            var names = GetPackageNames();
            var index = names.IndexOf(evt.newValue);
            if (index < 0 || index == mSelectedPackageIndex)
            {
                return;
            }

            mSelectedPackageIndex = index;
            mSelectedGroupIndex = 0;
            mExpandedCollectorIndex = -1;
            RefreshPackageSettingsPanel();
            RefreshGroupNav();
            RefreshCollectorCanvas();
        }

        private void OpenNativeCollector()
        {
            OpenOrWarn(YooAssetEditorMenuBridge.OpenCollector, YooAssetEditorMenuBridge.COLLECTOR_MENU_PATH);
        }

        private void OpenNativeBuilder()
        {
            OpenOrWarn(YooAssetEditorMenuBridge.OpenBuilder, YooAssetEditorMenuBridge.BUILDER_MENU_PATH);
        }

        private static void OpenOrWarn(Func<bool> open, string menuPath)
        {
            if (open())
            {
                return;
            }

            EditorUtility.DisplayDialog("YooAsset", "未找到 YooAsset 菜单：" + menuPath + "\n请确认 YooAsset 包已正确导入。", "OK");
        }

        private static string ShortRuleName(string ruleName)
        {
            if (string.IsNullOrEmpty(ruleName))
            {
                return "-";
            }

            string shortName;
            return sRuleShortNames.TryGetValue(ruleName, out shortName) ? shortName : ruleName;
        }

        private static string NormalizeAssetPath(string fullPath)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return string.Empty;
            }

            var normalized = fullPath.Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (normalized.StartsWith(dataPath, StringComparison.Ordinal))
            {
                return "Assets" + normalized.Substring(dataPath.Length);
            }

            return normalized;
        }

        private static void UpdateCountBadge(VisualElement badge, int count)
        {
            if (badge == null)
            {
                return;
            }

            var label = badge.Q<Label>();
            if (label != null)
            {
                label.text = count.ToString();
            }
        }

        private static VisualElement CreateRuleBadge(string text, Color color)
        {
            return YokiFrameUIComponents.CreateStatusBadge(text, new Color(color.r, color.g, color.b, 0.28f), Color.white);
        }

        private static Color CollectorTypeColor()
        {
            return new Color(0.25f, 0.45f, 0.70f, 1f);
        }

        private static Color AddressRuleColor()
        {
            return new Color(0.55f, 0.35f, 0.70f, 1f);
        }

        private static Color PackRuleColor()
        {
            return new Color(0.30f, 0.60f, 0.40f, 1f);
        }

        private static Color FilterRuleColor()
        {
            return new Color(0.75f, 0.50f, 0.25f, 1f);
        }

        private static Color TagColor()
        {
            return new Color(0.40f, 0.40f, 0.45f, 1f);
        }

    }
}
#endif
#endif
