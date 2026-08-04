#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;

namespace YokiFrame
{
    /// <summary>
    /// 使用 InspectorKit 恢复 UIPanel 的动画、焦点、绑定树、生成操作和派生字段界面。
    /// </summary>
    [CustomEditor(typeof(UIPanel), true)]
    [CanEditMultipleObjects]
    internal sealed partial class UIKitPanelInspector : UnityEditor.Editor
    {
        private const string PANEL_SETTINGS_KEY = "UIKit.Panel.Settings";
        private const string ANIMATION_SETTINGS_KEY = "UIKit.Panel.Animation";
        private const string FOCUS_SETTINGS_KEY = "UIKit.Panel.Focus";
        private const string OTHER_PROPERTIES_KEY = "UIKit.Panel.OtherProperties";

        private static readonly Type[] sAnimationTypes =
        {
            default,
            typeof(FadeAnimationConfig),
            typeof(ScaleAnimationConfig),
            typeof(SlideAnimationConfig),
            typeof(CompositeAnimationConfig)
        };

        private static readonly string[] sAnimationLabels =
        {
            "无",
            "淡入淡出",
            "缩放",
            "滑动",
            "组合"
        };

        private SerializedProperty mShowAnimation;
        private SerializedProperty mHideAnimation;
        private SerializedProperty mAutoFocus;
        private SerializedProperty mDefaultSelectable;

        /// <summary>缓存 UIPanel 框架字段，避免重建视觉树时重复按名称查找。</summary>
        private void OnEnable()
        {
            if (TryGetSerializedObject(out SerializedObject currentSerializedObject))
            {
                mShowAnimation = currentSerializedObject.FindProperty("mShowAnimationConfig");
                mHideAnimation = currentSerializedObject.FindProperty("mHideAnimationConfig");
                mAutoFocus = currentSerializedObject.FindProperty("mAutoFocusOnShow");
                mDefaultSelectable = currentSerializedObject.FindProperty("mDefaultSelectable");
            }
            InitializeBindingTree();
        }

        /// <summary>创建由 InspectorKit 统一样式化的完整 UIPanel Inspector。</summary>
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = InspectorKitUi.CreateRoot();
            if (!HasValidTargets())
            {
                root.Add(InspectorKitUi.CreateInfoBox(
                    "Inspector 目标已失效，请重新选择面板。",
                    InspectorInfoBoxType.Warning));
                return root;
            }

            root.Add(CreatePanelSettings());
            root.Add(CreateBindingTree());
            VisualElement otherProperties = CreateOtherProperties();
            if (otherProperties != null)
                root.Add(otherProperties);
            return root;
        }

        /// <summary>创建外层面板设置卡片，并组合动画和焦点折叠区。</summary>
        private VisualElement CreatePanelSettings()
        {
            return InspectorKitUi.CreateCard(
                "面板设置",
                PANEL_SETTINGS_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(CreateAnimationSettings());
                    body.Add(CreateFocusSettings());
                });
        }

        /// <summary>创建显示和隐藏动画的多态配置区。</summary>
        private VisualElement CreateAnimationSettings()
        {
            VisualElement section = InspectorKitUi.CreateFoldoutSection(
                "动画设置",
                ANIMATION_SETTINGS_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(InspectorKitUi.CreateInfoBox(
                        "配置面板显示和隐藏时播放的动画效果。",
                        InspectorInfoBoxType.Info));
                    body.Add(CreateAnimationField("显示动画", mShowAnimation));
                    body.Add(CreateAnimationField("隐藏动画", mHideAnimation));
                });
            section.AddToClassList("yoki-editor-inspector__foldout--animation");
            return section;
        }

        /// <summary>创建默认焦点、自动聚焦和说明信息。</summary>
        private VisualElement CreateFocusSettings()
        {
            VisualElement section = InspectorKitUi.CreateFoldoutSection(
                "焦点设置",
                FOCUS_SETTINGS_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(InspectorKitUi.CreateInfoBox(
                        "定义面板打开时默认获得焦点的可选控件。",
                        InspectorInfoBoxType.Info));
                    body.Add(InspectorKitUi.CreateSwitchRow(mAutoFocus, "自动聚焦"));
                    body.Add(InspectorKitUi.CreatePropertyRow(mDefaultSelectable, "默认选中对象"));
                });
            section.AddToClassList("yoki-editor-inspector__foldout--focus");
            return section;
        }

        /// <summary>创建可切换类型并显示具体参数的动画字段。</summary>
        private VisualElement CreateAnimationField(string label, SerializedProperty property)
        {
            VisualElement container = new();
            RefreshAnimationField(container, label, property);
            return container;
        }

        /// <summary>在动画类型变化后局部重建下拉框和参数字段。</summary>
        private void RefreshAnimationField(
            VisualElement container,
            string label,
            SerializedProperty property)
        {
            InspectorKitUi.Refresh(container, body =>
            {
                int selectedIndex = GetAnimationIndex(property);
                body.Add(InspectorKitUi.CreateDropdownRow(
                    label,
                    sAnimationLabels,
                    selectedIndex,
                    index => SetAnimationConfig(container, label, property, index)));
                if (property != null && property.managedReferenceValue != null)
                {
                    PropertyField field = new(property, "参数");
                    field.BindProperty(property);
                    field.AddToClassList("yoki-editor-inspector__managed-reference");
                    body.Add(field);
                }
            });
        }

        /// <summary>解析当前 SerializeReference 配置在类型菜单中的索引。</summary>
        private static int GetAnimationIndex(SerializedProperty property)
        {
            if (property == null || property.managedReferenceValue == null)
                return 0;
            Type currentType = property.managedReferenceValue.GetType();
            for (var index = 1; index < sAnimationTypes.Length; index++)
            {
                if (sAnimationTypes[index] == currentType)
                    return index;
            }
            return 0;
        }

        /// <summary>替换动画配置，按需补齐 Fade 所需 CanvasGroup，并刷新当前字段。</summary>
        private void SetAnimationConfig(
            VisualElement container,
            string label,
            SerializedProperty property,
            int index)
        {
            if (property == null || index < 0 || index >= sAnimationTypes.Length)
                return;
            serializedObject.Update();
            property.managedReferenceValue = index == 0
                ? default
                : Activator.CreateInstance(sAnimationTypes[index]);
            serializedObject.ApplyModifiedProperties();
            AddCanvasGroupForFade(property);
            RefreshAnimationField(container, label, property);
        }

        /// <summary>Fade 配置首次启用时为单个面板补齐 CanvasGroup。</summary>
        private void AddCanvasGroupForFade(SerializedProperty property)
        {
            UIPanel panel = target as UIPanel;
            if (panel == default || !(property.managedReferenceValue is FadeAnimationConfig))
                return;
            if (panel.GetComponent<CanvasGroup>() == default)
                Undo.AddComponent<CanvasGroup>(panel.gameObject);
        }

        /// <summary>创建仅包含派生面板业务字段的其它属性卡片。</summary>
        private VisualElement CreateOtherProperties()
        {
            VisualElement properties = CreateExternalPropertyFields();
            if (properties == null)
                return default;
            return InspectorKitUi.CreateCard(
                "其他属性",
                OTHER_PROPERTIES_KEY,
                InspectorCardInitialState.Collapsed,
                body => body.Add(properties));
        }

        /// <summary>确认全部 Inspector 目标仍是有效 Unity 对象。</summary>
        private bool HasValidTargets()
        {
            if (targets == null || targets.Length == 0)
                return false;
            for (var index = 0; index < targets.Length; index++)
            {
                if (targets[index] == default)
                    return false;
            }
            return true;
        }
    }
}
#endif
