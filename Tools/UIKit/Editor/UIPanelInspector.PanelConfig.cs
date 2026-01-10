#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// UIPanelInspector - 面板配置区块
    /// </summary>
    public partial class UIPanelInspector
    {
        #region 动画类型定义

        /// <summary>
        /// 可用的动画类型
        /// </summary>
        private static readonly (string Name, System.Type Type)[] sAnimationTypes =
        {
            ("无", null),
            ("淡入淡出", typeof(FadeAnimationConfig)),
            ("缩放", typeof(ScaleAnimationConfig)),
            ("滑动", typeof(SlideAnimationConfig))
        };

        #endregion

        /// <summary>
        /// 创建面板配置区块（包含动画和焦点配置）
        /// </summary>
        private void CreatePanelConfigSection(bool hasAnimConfig, bool hasFocusConfig)
        {
            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList("uipanel-section-panelconfig");
            
            // 可折叠标题
            var foldout = new Foldout { text = "面板配置", value = true };
            foldout.AddToClassList("uipanel-panelconfig-foldout");
            section.Add(foldout);
            
            // 内容容器
            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            foldout.Add(content);
            
            // 动画配置子区块
            if (hasAnimConfig)
            {
                var animSubSection = CreateSubSection("动画配置", "uipanel-subsection-animation");
                var animContent = animSubSection.Q<VisualElement>(className: "uipanel-subsection-content");
                
                // 帮助提示
                var helpBox = CreateHelpBox(
                    "配置面板的显示/隐藏动画。支持淡入淡出、缩放、滑动等多种动画类型。"
                );
                animContent.Add(helpBox);
                
                // 显示动画
                if (mShowAnimationConfigProp != null)
                {
                    var showAnimContainer = CreateAnimationConfigField(
                        "显示动画",
                        "面板显示时播放的动画",
                        mShowAnimationConfigProp
                    );
                    animContent.Add(showAnimContainer);
                }
                
                // 隐藏动画
                if (mHideAnimationConfigProp != null)
                {
                    var hideAnimContainer = CreateAnimationConfigField(
                        "隐藏动画",
                        "面板隐藏时播放的动画",
                        mHideAnimationConfigProp
                    );
                    animContent.Add(hideAnimContainer);
                }
                
                content.Add(animSubSection);
            }
            
            // 焦点配置子区块
            if (hasFocusConfig)
            {
                var focusSubSection = CreateSubSection("焦点配置", "uipanel-subsection-focus");
                var focusContent = focusSubSection.Q<VisualElement>(className: "uipanel-subsection-content");
                
                // 帮助提示
                var helpBox = CreateHelpBox(
                    "手柄/键盘导航支持。当面板显示时，焦点系统会自动选中此元素。"
                );
                focusContent.Add(helpBox);
                
                // 默认焦点元素
                var selectableRow = CreateFieldRow(
                    "默认焦点元素",
                    "面板显示时自动获得焦点的 UI 元素（Button、Toggle 等）",
                    mDefaultSelectableProp
                );
                focusContent.Add(selectableRow);
                
                content.Add(focusSubSection);
            }
            
            mRoot.Add(section);
            mLastSection = section;
        }

        /// <summary>
        /// 创建子区块（内嵌的小卡片）
        /// </summary>
        private VisualElement CreateSubSection(string title, string className)
        {
            var subSection = new VisualElement();
            subSection.AddToClassList("uipanel-subsection");
            subSection.AddToClassList(className);
            
            // 标题
            var header = new Label(title);
            header.AddToClassList("uipanel-subsection-header");
            subSection.Add(header);
            
            // 内容容器
            var content = new VisualElement();
            content.AddToClassList("uipanel-subsection-content");
            subSection.Add(content);
            
            return subSection;
        }

        /// <summary>
        /// 创建动画配置字段（带类型选择）
        /// </summary>
        private VisualElement CreateAnimationConfigField(string label, string tooltip, SerializedProperty property)
        {
            var container = new VisualElement();
            container.AddToClassList("uipanel-animation-config");
            
            // 标签行
            var labelRow = new VisualElement();
            labelRow.AddToClassList("uipanel-field-row");
            
            var labelElement = new Label(label);
            labelElement.AddToClassList("uipanel-field-label");
            labelElement.tooltip = tooltip;
            labelRow.Add(labelElement);
            
            // 类型选择下拉框
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
            
            // 参数容器
            var paramsContainer = new VisualElement();
            paramsContainer.AddToClassList("uipanel-animation-params");
            container.Add(paramsContainer);
            
            // 初始化参数显示
            RefreshAnimationParams(property, paramsContainer);
            
            return container;
        }

        /// <summary>
        /// 获取当前动画类型索引
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
        /// 动画类型变更处理
        /// </summary>
        private void OnAnimationTypeChanged(SerializedProperty property, int typeIndex, VisualElement container)
        {
            serializedObject.Update();
            
            // 记录旧类型用于判断是否需要管理 CanvasGroup
            var oldType = property.managedReferenceValue?.GetType();
            System.Type newType = typeIndex > 0 ? sAnimationTypes[typeIndex].Type : null;
            
            if (typeIndex == 0 || newType == null)
            {
                property.managedReferenceValue = null;
            }
            else
            {
                property.managedReferenceValue = System.Activator.CreateInstance(newType);
            }
            
            serializedObject.ApplyModifiedProperties();
            
            // 刷新参数显示（在 CanvasGroup 操作之前，避免 SerializedObject 失效）
            var paramsContainer = container.Q<VisualElement>(className: "uipanel-animation-params");
            if (paramsContainer != null)
            {
                RefreshAnimationParams(property, paramsContainer);
            }
            
            // 延迟执行 CanvasGroup 管理，避免 Undo 操作导致 SerializedObject 失效
            EditorApplication.delayCall += () =>
            {
                if (target == default) return;
                ManageCanvasGroupForFadeAnimation(oldType, newType);
            };
        }
        
        /// <summary>
        /// 根据 Fade 动画配置管理 CanvasGroup 组件
        /// </summary>
        private void ManageCanvasGroupForFadeAnimation(System.Type oldType, System.Type newType)
        {
            var panel = target as UIPanel;
            if (panel == default) return;
            
            var gameObject = panel.gameObject;
            bool wasFade = oldType == typeof(FadeAnimationConfig);
            bool isFade = newType == typeof(FadeAnimationConfig);
            
            // 如果新选择了 Fade 动画，确保有 CanvasGroup
            if (isFade && !wasFade)
            {
                EnsureCanvasGroup(gameObject);
            }
            // 如果取消了 Fade 动画，检查是否需要移除 CanvasGroup
            else if (wasFade && !isFade)
            {
                TryRemoveCanvasGroupIfUnused(gameObject);
            }
        }
        
        /// <summary>
        /// 确保 GameObject 有 CanvasGroup 组件
        /// </summary>
        private void EnsureCanvasGroup(GameObject gameObject)
        {
            if (gameObject.TryGetComponent<CanvasGroup>(out _))
                return;
            
            Undo.AddComponent<CanvasGroup>(gameObject);
            Debug.Log($"[UIKit] 已为 '{gameObject.name}' 添加 CanvasGroup 组件（Fade 动画需要）");
        }
        
        /// <summary>
        /// 尝试移除未使用的 CanvasGroup 组件
        /// </summary>
        private void TryRemoveCanvasGroupIfUnused(GameObject gameObject)
        {
            // 重新获取 SerializedObject 以确保数据有效
            var so = new SerializedObject(target);
            var showProp = so.FindProperty("mShowAnimationConfig");
            var hideProp = so.FindProperty("mHideAnimationConfig");
            
            // 检查是否还有其他 Fade 动画在使用
            bool stillUsesFade = false;
            
            if (showProp?.managedReferenceValue is FadeAnimationConfig)
                stillUsesFade = true;
            if (hideProp?.managedReferenceValue is FadeAnimationConfig)
                stillUsesFade = true;
            
            so.Dispose();
            
            if (stillUsesFade)
                return;
            
            // 检查 CanvasGroup 是否有自定义设置（非默认值）
            if (!gameObject.TryGetComponent<CanvasGroup>(out var canvasGroup))
                return;
            
            // 如果 CanvasGroup 有非默认设置，不自动移除
            if (!IsCanvasGroupDefault(canvasGroup))
            {
                Debug.Log($"[UIKit] '{gameObject.name}' 的 CanvasGroup 有自定义设置，保留组件");
                return;
            }
            
            Undo.DestroyObjectImmediate(canvasGroup);
            Debug.Log($"[UIKit] 已移除 '{gameObject.name}' 的 CanvasGroup 组件（不再使用 Fade 动画）");
        }
        
        /// <summary>
        /// 检查 CanvasGroup 是否为默认设置
        /// </summary>
        private static bool IsCanvasGroupDefault(CanvasGroup canvasGroup)
        {
            const float EPSILON = 0.001f;
            
            // 检查是否为默认值
            return Mathf.Abs(canvasGroup.alpha - 1f) < EPSILON &&
                   canvasGroup.interactable &&
                   canvasGroup.blocksRaycasts &&
                   !canvasGroup.ignoreParentGroups;
        }

        /// <summary>
        /// 刷新动画参数显示
        /// </summary>
        private void RefreshAnimationParams(SerializedProperty property, VisualElement paramsContainer)
        {
            paramsContainer.Clear();
            
            if (property.managedReferenceValue == null)
                return;
            
            // 遍历所有子属性
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
