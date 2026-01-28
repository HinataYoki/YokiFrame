#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && UNITY_2022_1_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - UI 辅助方法（基础控件）
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        #region 验证提示

        /// <summary>
        /// 创建验证提示标签
        /// </summary>
        private static VisualElement CreateValidationHint()
        {
            var hint = new Label();
            hint.style.fontSize = 10;
            hint.style.marginTop = Spacing.XS;
            hint.style.whiteSpace = WhiteSpace.Normal;
            hint.style.display = DisplayStyle.None;
            return hint;
        }

        #endregion

        #region 按钮

        /// <summary>
        /// 创建重置按钮
        /// </summary>
        private static VisualElement CreateResetButton(string text, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.marginTop = Spacing.SM;
            btn.style.height = 22;
            btn.style.backgroundColor = new StyleColor(Colors.LayerElevated);
            btn.style.borderTopLeftRadius = Radius.SM;
            btn.style.borderTopRightRadius = Radius.SM;
            btn.style.borderBottomLeftRadius = Radius.SM;
            btn.style.borderBottomRightRadius = Radius.SM;
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderRightColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderTopColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderBottomColor = new StyleColor(Colors.BorderDefault);
            btn.style.color = new StyleColor(Colors.TextSecondary);
            btn.style.fontSize = 10;
            return btn;
        }

        /// <summary>
        /// 创建操作按钮
        /// </summary>
        private static Button CreateActionButton(string text, Color textColor, Action onClick)
        {
            var btn = new Button(onClick) { text = text };
            btn.style.height = 26;
            btn.style.backgroundColor = new StyleColor(Colors.LayerElevated);
            btn.style.borderTopLeftRadius = Radius.SM;
            btn.style.borderTopRightRadius = Radius.SM;
            btn.style.borderBottomLeftRadius = Radius.SM;
            btn.style.borderBottomRightRadius = Radius.SM;
            btn.style.borderLeftWidth = 1;
            btn.style.borderRightWidth = 1;
            btn.style.borderTopWidth = 1;
            btn.style.borderBottomWidth = 1;
            btn.style.borderLeftColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderRightColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderTopColor = new StyleColor(Colors.BorderDefault);
            btn.style.borderBottomColor = new StyleColor(Colors.BorderDefault);
            btn.style.color = new StyleColor(textColor);
            btn.style.fontSize = 11;
            return btn;
        }

        #endregion

        #region 下拉框

        /// <summary>
        /// 创建偏移量下拉框
        /// </summary>
        private static VisualElement CreateOffsetDropdown(UnityEditor.SerializedProperty parent)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var label = new Label("偏移量");
            label.style.minWidth = 130;
            label.style.width = 130;
            label.style.color = new StyleColor(Colors.TextSecondary);
            row.Add(label);

            var offsetProp = parent.FindPropertyRelative("FileOffset");
            int currentIndex = Array.IndexOf(sOffsetValues, offsetProp.intValue);
            if (currentIndex < 0) currentIndex = 1;

            var dropdown = new DropdownField(sOffsetChoices, currentIndex);
            dropdown.style.flexGrow = 1;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                int index = sOffsetChoices.IndexOf(evt.newValue);
                if (index >= 0 && index < sOffsetValues.Length)
                {
                    offsetProp.intValue = sOffsetValues[index];
                    parent.serializedObject.ApplyModifiedProperties();
                }
            });
            row.Add(dropdown);

            return row;
        }

        /// <summary>
        /// 创建带标签的下拉框
        /// </summary>
        private static VisualElement CreateLabeledDropdown(string label, List<string> choices, string defaultValue, Action<string> onValueChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 80;
            labelElement.style.width = 80;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.fontSize = 11;
            row.Add(labelElement);

            var dropdown = new DropdownField(choices, choices.IndexOf(defaultValue));
            dropdown.style.flexGrow = 1;
            dropdown.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
            row.Add(dropdown);

            return row;
        }

        #endregion

        #region 文本框

        /// <summary>
        /// 创建带标签的文本框
        /// </summary>
        private static VisualElement CreateLabeledTextField(string label, string defaultValue, string placeholder, Action<string> onValueChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 80;
            labelElement.style.width = 80;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.fontSize = 11;
            row.Add(labelElement);

            var textField = new TextField { value = defaultValue };
            textField.style.flexGrow = 1;
            textField.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
            row.Add(textField);

            return row;
        }

        #endregion

        #region 开关

        /// <summary>
        /// 创建带标签的开关（未使用，保留备用）
        /// </summary>
        private static VisualElement CreateLabeledToggle(string label, bool defaultValue, Action<bool> onValueChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 80;
            labelElement.style.width = 80;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.fontSize = 11;
            row.Add(labelElement);

            var toggle = new Toggle { value = defaultValue };
            toggle.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
            row.Add(toggle);

            return row;
        }

        #endregion

        #region 属性字段

        /// <summary>
        /// 创建属性字段
        /// </summary>
        private static PropertyField CreatePropertyField(UnityEditor.SerializedProperty parent, string propertyName, string label)
        {
            var prop = parent.FindPropertyRelative(propertyName);
            var field = new PropertyField(prop, label);
            field.style.marginTop = Spacing.XS;
            field.style.marginBottom = Spacing.XS;
            field.BindProperty(prop);

            field.RegisterCallback<GeometryChangedEvent>(evt =>
            {
                var labelElement = field.Q<Label>();
                if (labelElement != null)
                {
                    labelElement.style.minWidth = 130;
                    labelElement.style.width = 130;
                }
            });

            return field;
        }

        /// <summary>
        /// 创建播放模式字段
        /// </summary>
        private static VisualElement CreatePlayModeField(UnityEditor.SerializedProperty parent, string propertyName, string label, bool filterEditorMode)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = Spacing.XS;
            row.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.minWidth = 130;
            labelElement.style.width = 130;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            row.Add(labelElement);

            var prop = parent.FindPropertyRelative(propertyName);

            if (filterEditorMode)
            {
                var choices = new List<string> { "OfflinePlayMode", "HostPlayMode", "WebPlayMode", "CustomPlayMode" };
                var values = new[] { 1, 2, 3, 4 };

                int currentValue = prop.enumValueIndex;
                int currentIndex = Array.IndexOf(values, currentValue);
                if (currentIndex < 0)
                {
                    currentIndex = 0;
                    prop.enumValueIndex = values[0];
                    parent.serializedObject.ApplyModifiedProperties();
                }

                var dropdown = new DropdownField(choices, currentIndex);
                dropdown.style.flexGrow = 1;
                dropdown.RegisterValueChangedCallback(evt =>
                {
                    int index = choices.IndexOf(evt.newValue);
                    if (index >= 0 && index < values.Length)
                    {
                        prop.enumValueIndex = values[index];
                        parent.serializedObject.ApplyModifiedProperties();
                    }
                });
                row.Add(dropdown);
            }
            else
            {
                var field = new PropertyField(prop, "");
                field.style.flexGrow = 1;
                field.BindProperty(prop);
                row.Add(field);
            }

            return row;
        }

        #endregion
    }
}
#endif
