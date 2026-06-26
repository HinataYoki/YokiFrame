#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// UIPanel 自定义 Inspector，提供面板设置、绑定树和生成代码入口。
    /// </summary>
    [CustomEditor(typeof(UIPanel), true)]
    [CanEditMultipleObjects]
    public partial class UIPanelInspector : Editor
    {
        private const string PANEL_CONFIG_FOLDOUT_KEY = "YokiFrame.UIKit.UIPanelInspector.PanelConfig";
        private const string ANIMATION_SECTION_FOLDOUT_KEY = "YokiFrame.UIKit.UIPanelInspector.AnimationSection";
        private const string FOCUS_SECTION_FOLDOUT_KEY = "YokiFrame.UIKit.UIPanelInspector.FocusSection";
        private const string CUSTOM_PROPERTIES_FOLDOUT_KEY = "YokiFrame.UIKit.UIPanelInspector.CustomProperties";
        private const string STYLE_SHEET_PATH = "Tools/UIKit/Editor/Inspectors/UIPanelInspectorStyles.uss";

        private static readonly Type[] sAnimationConfigTypes =
        {
            null,
            typeof(FadeAnimationConfig),
            typeof(ScaleAnimationConfig),
            typeof(SlideAnimationConfig)
        };

        private static readonly string[] sAnimationConfigLabels =
        {
            "无",
            "淡入淡出",
            "缩放",
            "滑动"
        };

        private SerializedProperty mShowAnimationConfigProp;
        private SerializedProperty mHideAnimationConfigProp;
        private SerializedProperty mAutoFocusOnShowProp;
        private SerializedProperty mDefaultSelectableProp;

        private void OnEnable()
        {
            mShowAnimationConfigProp = serializedObject.FindProperty("mShowAnimationConfig");
            mHideAnimationConfigProp = serializedObject.FindProperty("mHideAnimationConfig");
            mAutoFocusOnShowProp = serializedObject.FindProperty("mAutoFocusOnShow");
            mDefaultSelectableProp = serializedObject.FindProperty("mDefaultSelectable");
            LoadCollapsedBindPaths();
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("uipanel-inspector");

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UIKitEditorAssetPaths.ResolveStyleSheetPath(STYLE_SHEET_PATH));
            if (styleSheet != null)
                root.styleSheets.Add(styleSheet);

            CreatePanelSettings(root);
            CreateBindTree(root);
            CreateCustomProperties(root);
            return root;
        }

        private void CreatePanelSettings(VisualElement root)
        {
            var section = CreateSectionContainer("uipanel-section-panelconfig");

            var foldout = CreateRememberedFoldout(
                "面板设置",
                PANEL_CONFIG_FOLDOUT_KEY,
                true,
                "uipanel-panelconfig-foldout");
            section.Add(foldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            foldout.Add(content);

            var animationSection = CreateSubSection("动画设置", "uipanel-subsection-animation", ANIMATION_SECTION_FOLDOUT_KEY);
            var animationContent = animationSection.Q<VisualElement>(className: "uipanel-subsection-content");
            animationContent.Add(CreateHelpBox("配置面板显示和隐藏时播放的动画效果。"));
            animationContent.Add(CreateAnimationField("显示动画", mShowAnimationConfigProp));
            animationContent.Add(CreateAnimationField("隐藏动画", mHideAnimationConfigProp));
            content.Add(animationSection);

            var focusSection = CreateSubSection("焦点设置", "uipanel-subsection-focus", FOCUS_SECTION_FOLDOUT_KEY);
            var focusContent = focusSection.Q<VisualElement>(className: "uipanel-subsection-content");
            focusContent.Add(CreateHelpBox("定义面板打开时默认获得焦点的可选控件。"));
            if (mAutoFocusOnShowProp != null)
                focusContent.Add(CreatePropertyRow("自动聚焦", mAutoFocusOnShowProp));
            if (mDefaultSelectableProp != null)
                focusContent.Add(CreatePropertyRow("默认选中对象", mDefaultSelectableProp));
            content.Add(focusSection);

            root.Add(section);
        }

        private VisualElement CreateAnimationField(string label, SerializedProperty property)
        {
            var container = new VisualElement();
            container.AddToClassList("uipanel-animation-config");
            if (property == null)
                return container;

            var row = CreateFieldRow(label);
            var choices = new List<string>(sAnimationConfigLabels);
            var dropdown = new DropdownField(choices, GetAnimationIndex(property));
            dropdown.AddToClassList("uipanel-animation-dropdown");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = choices.IndexOf(evt.newValue);
                SetAnimationConfig(property, index);
                RebuildInspector();
            });
            row.Add(dropdown);
            container.Add(row);

            if (property.managedReferenceValue != null)
            {
                var field = new PropertyField(property, "参数");
                field.AddToClassList("uipanel-animation-params");
                container.Add(field);
            }

            return container;
        }

        private int GetAnimationIndex(SerializedProperty property)
        {
            if (property == null || property.managedReferenceValue == null)
                return 0;

            var type = property.managedReferenceValue.GetType();
            for (var i = 1; i < sAnimationConfigTypes.Length; i++)
            {
                if (sAnimationConfigTypes[i] == type)
                    return i;
            }

            return 0;
        }

        private void SetAnimationConfig(SerializedProperty property, int index)
        {
            serializedObject.Update();
            property.managedReferenceValue = index > 0 ? Activator.CreateInstance(sAnimationConfigTypes[index]) : null;
            serializedObject.ApplyModifiedProperties();

            var panel = target as UIPanel;
            if (panel != null && property.managedReferenceValue is FadeAnimationConfig && panel.GetComponent<CanvasGroup>() == null)
                Undo.AddComponent<CanvasGroup>(panel.gameObject);
        }

        private void GenerateUICode()
        {
            var prefab = ResolvePrefabAsset();
            if (prefab == null)
            {
                EditorUtility.DisplayDialog("无法生成 UI 代码", "请在 Prefab 资源或 Prefab 实例上使用生成入口。", "确定");
                return;
            }

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
                RefreshBindTree();
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("生成失败", ex.Message, "确定");
                LogKit.Exception(ex);
            }
        }

        private GameObject ResolvePrefabAsset()
        {
            var panel = target as UIPanel;
            if (panel == null)
                return null;

            var assetPath = AssetDatabase.GetAssetPath(panel.gameObject);
            if (!string.IsNullOrEmpty(assetPath))
                return AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(panel.gameObject);
            return string.IsNullOrEmpty(assetPath) ? null : AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        }

        private void OpenPanelScript()
        {
            var panel = target as UIPanel;
            if (panel == null)
                return;

            var path = UIKitPanelPrefabCreator.DEFAULT_SCRIPT_FOLDER + "/" + panel.gameObject.name + "/" + panel.gameObject.name + ".cs";
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
            else
                EditorUtility.DisplayDialog("脚本不存在", "尚未找到脚本文件：\n" + path, "确定");
        }

        private void CreateCustomProperties(VisualElement root)
        {
            var iterator = serializedObject.GetIterator();
            if (!iterator.NextVisible(true))
                return;

            var properties = new List<SerializedProperty>();
            do
            {
                if (IsBuiltInPanelProperty(iterator.name))
                    continue;

                properties.Add(iterator.Copy());
            }
            while (iterator.NextVisible(false));

            if (properties.Count == 0)
                return;

            var section = CreateSectionContainer("uipanel-section-custom");
            var foldout = CreateRememberedFoldout(
                "其他属性",
                CUSTOM_PROPERTIES_FOLDOUT_KEY,
                false,
                "uipanel-custom-foldout");
            section.Add(foldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            foldout.Add(content);

            for (var i = 0; i < properties.Count; i++)
                content.Add(new PropertyField(properties[i]));

            root.Add(section);
        }

        private static bool IsBuiltInPanelProperty(string propertyName)
        {
            return propertyName == "m_Script" ||
                   propertyName == "mShowAnimationConfig" ||
                   propertyName == "mHideAnimationConfig" ||
                   propertyName == "mAutoFocusOnShow" ||
                   propertyName == "mDefaultSelectable" ||
                   propertyName == "mData";
        }

        private static VisualElement CreateSectionContainer(string modifierClass)
        {
            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList(modifierClass);
            return section;
        }

        private static VisualElement CreateSubSection(string title, string modifierClass, string sessionStateKey)
        {
            var subSection = new VisualElement();
            subSection.AddToClassList("uipanel-subsection");
            subSection.AddToClassList(modifierClass);

            var foldout = CreateRememberedFoldout(
                title,
                sessionStateKey,
                true,
                "uipanel-subsection-foldout");
            subSection.Add(foldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-subsection-content");
            foldout.Add(content);
            return subSection;
        }

        private static VisualElement CreateHelpBox(string text)
        {
            var helpBox = new VisualElement();
            helpBox.AddToClassList("uipanel-helpbox");

            var icon = new Label("i");
            icon.AddToClassList("uipanel-helpbox-icon");
            helpBox.Add(icon);

            var label = new Label(text);
            label.AddToClassList("uipanel-helpbox-text");
            helpBox.Add(label);
            return helpBox;
        }

        private static VisualElement CreatePropertyRow(string label, SerializedProperty property)
        {
            var row = CreateFieldRow(label);
            var field = new PropertyField(property, string.Empty);
            field.AddToClassList("uipanel-field");
            row.Add(field);
            return row;
        }

        private static VisualElement CreateFieldRow(string label)
        {
            var row = new VisualElement();
            row.AddToClassList("uipanel-field-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("uipanel-field-label");
            row.Add(labelElement);
            return row;
        }

        private void RebuildInspector()
        {
            var active = ActiveEditorTracker.sharedTracker;
            if (active != null)
                active.ForceRebuild();
        }

        private static Foldout CreateRememberedFoldout(string title, string stateKey, bool defaultValue, string className)
        {
            var foldout = new Foldout
            {
                text = title,
                value = GetSavedBool(stateKey, defaultValue),
                viewDataKey = stateKey
            };

            if (!string.IsNullOrEmpty(className))
                foldout.AddToClassList(className);

            foldout.RegisterValueChangedCallback(evt => SetSavedBool(stateKey, evt.newValue));
            return foldout;
        }

        private static bool GetSavedBool(string key, bool defaultValue)
        {
            if (EditorPrefs.HasKey(key))
                return EditorPrefs.GetBool(key, defaultValue);

            return SessionState.GetBool(key, defaultValue);
        }

        private static void SetSavedBool(string key, bool value)
        {
            SessionState.SetBool(key, value);
            EditorPrefs.SetBool(key, value);
        }

        private static string GetSavedString(string key, string defaultValue)
        {
            const string MissingValue = "__YOKIFRAME_MISSING_SESSION_STATE__";
            var sessionValue = SessionState.GetString(key, MissingValue);
            if (sessionValue != MissingValue)
                return sessionValue;

            return EditorPrefs.HasKey(key) ? EditorPrefs.GetString(key, defaultValue) : defaultValue;
        }

        private static void SetSavedString(string key, string value)
        {
            SessionState.SetString(key, value);
            EditorPrefs.SetString(key, value);
        }
    }
}
#endif
