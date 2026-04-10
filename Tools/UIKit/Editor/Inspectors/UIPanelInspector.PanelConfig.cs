#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="UIPanelInspector"/> 的面板配置区块。
    /// 负责展示动画配置与焦点配置。
    /// </summary>
    public partial class UIPanelInspector
    {
        #region Animation Type Definitions

        /// <summary>
        /// 动画配置下拉框中支持的动画类型。
        /// </summary>
        private static readonly (string Name, Type Type)[] sAnimationTypes =
        {
            ("无", null),
            ("淡入淡出", typeof(FadeAnimationConfig)),
            ("缩放", typeof(ScaleAnimationConfig)),
            ("滑动", typeof(SlideAnimationConfig))
        };

        #endregion

        /// <summary>
        /// 创建面板配置区块，包含动画设置与焦点设置。
        /// </summary>
        private void CreatePanelConfigSection(bool hasAnimConfig, bool hasFocusConfig)
        {
            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList("uipanel-section-panelconfig");

            bool savedFoldoutState = SessionState.GetBool(KEY_PANEL_CONFIG_FOLDOUT, true);
            mPanelConfigFoldout = new Foldout { text = "面板设置", value = savedFoldoutState };
            mPanelConfigFoldout.AddToClassList("uipanel-panelconfig-foldout");

            mPanelConfigFoldout.RegisterValueChangedCallback(evt =>
            {
                SessionState.SetBool(KEY_PANEL_CONFIG_FOLDOUT, evt.newValue);
            });

            section.Add(mPanelConfigFoldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            mPanelConfigFoldout.Add(content);

            if (hasAnimConfig)
            {
                var animSubSection = CreateSubSection("动画设置", "uipanel-subsection-animation");
                var animContent = animSubSection.Q<VisualElement>(className: "uipanel-subsection-content");

                var helpBox = CreateHelpBox("配置面板显示和隐藏时播放的动画效果。");
                animContent.Add(helpBox);

                if (mShowAnimationConfigProp != null)
                {
                    var showAnimContainer = CreateAnimationConfigField(
                        "显示动画",
                        "面板显示时播放的动画。",
                        mShowAnimationConfigProp);
                    animContent.Add(showAnimContainer);
                }

                if (mHideAnimationConfigProp != null)
                {
                    var hideAnimContainer = CreateAnimationConfigField(
                        "隐藏动画",
                        "面板隐藏时播放的动画。",
                        mHideAnimationConfigProp);
                    animContent.Add(hideAnimContainer);
                }

                content.Add(animSubSection);
            }

            if (hasFocusConfig)
            {
                var focusSubSection = CreateSubSection("焦点设置", "uipanel-subsection-focus");
                var focusContent = focusSubSection.Q<VisualElement>(className: "uipanel-subsection-content");

                var helpBox = CreateHelpBox("定义面板打开时默认获得焦点的可选控件。");
                focusContent.Add(helpBox);

                var selectableRow = CreateFieldRow(
                    "默认选中对象",
                    "面板显示时默认获得焦点的 UI 元素。",
                    mDefaultSelectableProp);
                focusContent.Add(selectableRow);

                content.Add(focusSubSection);
            }

            mRoot.Add(section);
            mLastSection = section;
        }

        /// <summary>
        /// 在面板设置区块内部创建一个子区块。
        /// </summary>
        private VisualElement CreateSubSection(string title, string className)
        {
            var subSection = new VisualElement();
            subSection.AddToClassList("uipanel-subsection");
            subSection.AddToClassList(className);

            var header = new Label(title);
            header.AddToClassList("uipanel-subsection-header");
            subSection.Add(header);

            var content = new VisualElement();
            content.AddToClassList("uipanel-subsection-content");
            subSection.Add(content);

            return subSection;
        }

        /// <summary>
        /// 创建动画配置编辑区，包含类型选择与参数内联编辑。
        /// </summary>
        private VisualElement CreateAnimationConfigField(string label, string tooltip, SerializedProperty property)
        {
            var container = new VisualElement();
            container.AddToClassList("uipanel-animation-config");

            var labelRow = new VisualElement();
            labelRow.AddToClassList("uipanel-field-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("uipanel-field-label");
            labelElement.tooltip = tooltip;
            labelRow.Add(labelElement);

            var typeNames = new List<string>(sAnimationTypes.Length);
            foreach (var (name, _) in sAnimationTypes)
            {
                typeNames.Add(name);
            }

            int currentIndex = GetCurrentAnimationTypeIndex(property);
            var dropdown = new DropdownField(typeNames, currentIndex);
            dropdown.AddToClassList("uipanel-animation-dropdown");
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int newIndex = typeNames.IndexOf(evt.newValue);
                OnAnimationTypeChanged(property, newIndex, container);
            });
            labelRow.Add(dropdown);

            container.Add(labelRow);

            var paramsContainer = new VisualElement();
            paramsContainer.AddToClassList("uipanel-animation-params");
            container.Add(paramsContainer);

            RefreshAnimationParams(property, paramsContainer);
            return container;
        }

        /// <summary>
        /// 获取当前序列化属性对应的动画类型索引。
        /// </summary>
        private int GetCurrentAnimationTypeIndex(SerializedProperty property)
        {
            if (property.managedReferenceValue == null)
                return 0;

            var currentType = property.managedReferenceValue.GetType();
            for (int i = 0; i < sAnimationTypes.Length; i++)
            {
                if (sAnimationTypes[i].Type == currentType)
                    return i;
            }

            return 0;
        }

        /// <summary>
        /// 处理动画类型切换逻辑。
        /// </summary>
        private void OnAnimationTypeChanged(SerializedProperty property, int typeIndex, VisualElement container)
        {
            serializedObject.Update();

            var oldType = property.managedReferenceValue?.GetType();
            Type newType = typeIndex > 0 ? sAnimationTypes[typeIndex].Type : null;

            if (typeIndex == 0 || newType == null)
            {
                property.managedReferenceValue = null;
            }
            else
            {
                property.managedReferenceValue = Activator.CreateInstance(newType);
            }

            serializedObject.ApplyModifiedProperties();

            var paramsContainer = container.Q<VisualElement>(className: "uipanel-animation-params");
            if (paramsContainer != null)
            {
                RefreshAnimationParams(property, paramsContainer);
            }

            EditorApplication.delayCall += () =>
            {
                if (target == default)
                    return;

                ManageCanvasGroupForFadeAnimation(oldType, newType);
            };
        }

        /// <summary>
        /// 当淡入淡出动画启用状态变化时，自动补齐或移除 <see cref="CanvasGroup"/>。
        /// </summary>
        private void ManageCanvasGroupForFadeAnimation(Type oldType, Type newType)
        {
            var panel = target as UIPanel;
            if (panel == default)
                return;

            var gameObject = panel.gameObject;
            bool wasFade = oldType == typeof(FadeAnimationConfig);
            bool isFade = newType == typeof(FadeAnimationConfig);

            if (isFade && !wasFade)
            {
                EnsureCanvasGroup(gameObject);
            }
            else if (wasFade && !isFade)
            {
                TryRemoveCanvasGroupIfUnused(gameObject);
            }
        }

        /// <summary>
        /// 确保目标对象上存在 <see cref="CanvasGroup"/> 组件。
        /// </summary>
        private void EnsureCanvasGroup(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<CanvasGroup>(out _))
                return;

            Undo.AddComponent<CanvasGroup>(gameObject);
        }

        /// <summary>
        /// 当不再使用淡入淡出动画时，尝试移除未被使用的 <see cref="CanvasGroup"/>。
        /// </summary>
        private void TryRemoveCanvasGroupIfUnused(GameObject gameObject)
        {
            var so = new SerializedObject(target);
            var showProp = so.FindProperty("mShowAnimationConfig");
            var hideProp = so.FindProperty("mHideAnimationConfig");

            bool stillUsesFade = false;

            if (showProp?.managedReferenceValue is FadeAnimationConfig)
                stillUsesFade = true;
            if (hideProp?.managedReferenceValue is FadeAnimationConfig)
                stillUsesFade = true;

            so.Dispose();

            if (stillUsesFade)
                return;

            if (!gameObject.TryGetComponent<CanvasGroup>(out var canvasGroup))
                return;

            if (!IsCanvasGroupDefault(canvasGroup))
                return;

            Undo.DestroyObjectImmediate(canvasGroup);
        }

        /// <summary>
        /// 判断 <see cref="CanvasGroup"/> 是否仍保持默认值状态。
        /// </summary>
        private static bool IsCanvasGroupDefault(CanvasGroup canvasGroup)
        {
            const float EPSILON = 0.001f;

            return Mathf.Abs(canvasGroup.alpha - 1f) < EPSILON &&
                   canvasGroup.interactable &&
                   canvasGroup.blocksRaycasts &&
                   !canvasGroup.ignoreParentGroups;
        }

        /// <summary>
        /// 刷新动画参数区域中的内联字段。
        /// </summary>
        private void RefreshAnimationParams(SerializedProperty property, VisualElement paramsContainer)
        {
            paramsContainer.Clear();

            if (property.managedReferenceValue == null)
                return;

            var iterator = property.Copy();
            var endProperty = property.GetEndProperty();

            if (iterator.NextVisible(true))
            {
                do
                {
                    if (SerializedProperty.EqualContents(iterator, endProperty))
                        break;

                    var field = new PropertyField(iterator.Copy());
                    field.AddToClassList("uipanel-animation-param");
                    field.BindProperty(iterator.Copy());
                    paramsContainer.Add(field);
                }
                while (iterator.NextVisible(false));
            }
        }
    }
}
#endif
