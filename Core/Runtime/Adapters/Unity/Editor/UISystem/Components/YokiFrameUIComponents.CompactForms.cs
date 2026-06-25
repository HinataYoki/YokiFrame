#if !GODOT
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace YokiFrame.Unity
{
    /// <summary>
    /// UI 组件 - 紧凑型表单控件。
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region 紧凑型表单行

        /// <summary>
        /// 创建紧凑型表单行（标签 + 控件）。
        /// </summary>
        /// <param name="label">标签文本。</param>
        /// <param name="control">控件元素。</param>
        /// <param name="tooltip">提示文本（可选）。</param>
        /// <returns>表单行元素。</returns>
        public static VisualElement CreateCompactFormRow(string label, VisualElement control, string tooltip = null)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = Spacing.SM;

            var labelElement = new Label(label);
            labelElement.style.width = 120;
            labelElement.style.minWidth = 80;
            labelElement.style.fontSize = 12;
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            if (!string.IsNullOrEmpty(tooltip))
                labelElement.tooltip = tooltip;
            row.Add(labelElement);

            if (control != null)
            {
                control.style.flexGrow = 1;
                row.Add(control);
            }

            return row;
        }

        /// <summary>
        /// 创建紧凑型文本输入行。
        /// </summary>
        /// <param name="label">标签文本。</param>
        /// <param name="initialValue">初始值。</param>
        /// <param name="onValueChanged">值变更回调。</param>
        /// <param name="tooltip">提示文本（可选）。</param>
        /// <returns>表单行和文本框的元组。</returns>
        public static (VisualElement row, TextField field) CreateCompactTextField(
            string label,
            string initialValue,
            Action<string> onValueChanged,
            string tooltip = null)
        {
            var field = new TextField { value = initialValue ?? "" };
            field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));

            var row = CreateCompactFormRow(label, field, tooltip);
            return (row, field);
        }

        /// <summary>
        /// 创建紧凑型下拉框行。
        /// </summary>
        /// <param name="label">标签文本。</param>
        /// <param name="choices">选项列表。</param>
        /// <param name="initialIndex">初始选中索引。</param>
        /// <param name="onValueChanged">值变更回调。</param>
        /// <param name="tooltip">提示文本（可选）。</param>
        /// <returns>表单行和下拉框的元组。</returns>
        public static (VisualElement row, DropdownField dropdown) CreateCompactDropdown(
            string label,
            List<string> choices,
            int initialIndex,
            Action<string> onValueChanged,
            string tooltip = null)
        {
            var dropdown = new DropdownField();
            dropdown.choices = choices;
            dropdown.index = initialIndex >= 0 && initialIndex < choices.Count ? initialIndex : 0;
            dropdown.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));

            var row = CreateCompactFormRow(label, dropdown, tooltip);
            return (row, dropdown);
        }

        #endregion

        #region 带验证的表单控件

        /// <summary>
        /// 创建带验证的文本输入行。
        /// </summary>
        /// <param name="label">标签文本。</param>
        /// <param name="initialValue">初始值。</param>
        /// <param name="validator">验证函数（返回 true 表示有效）。</param>
        /// <param name="onValueChanged">值变更回调。</param>
        /// <param name="tooltip">提示文本（可选）。</param>
        /// <returns>表单行和文本框的元组。</returns>
        public static (VisualElement row, TextField field) CreateValidatedTextField(
            string label,
            string initialValue,
            Func<string, bool> validator,
            Action<string> onValueChanged,
            string tooltip = null)
        {
            var field = new TextField { value = initialValue ?? "" };

            void Validate(string value)
            {
                bool isValid = validator?.Invoke(value) ?? true;
                if (isValid)
                {
                    field.style.borderBottomColor = StyleKeyword.Null;
                    field.style.borderBottomWidth = StyleKeyword.Null;
                }
                else
                {
                    field.style.borderBottomColor = new StyleColor(Colors.StatusError);
                    field.style.borderBottomWidth = 2;
                }
            }

            field.RegisterValueChangedCallback(evt =>
            {
                Validate(evt.newValue);
                onValueChanged?.Invoke(evt.newValue);
            });

            Validate(initialValue);

            var row = CreateCompactFormRow(label, field, tooltip);
            return (row, field);
        }

        #endregion

        #region 枚举下拉框

        /// <summary>
        /// 创建枚举下拉框行。
        /// </summary>
        /// <typeparam name="T">枚举类型。</typeparam>
        /// <param name="label">标签文本。</param>
        /// <param name="initialValue">初始值。</param>
        /// <param name="onValueChanged">值变更回调。</param>
        /// <param name="tooltip">提示文本（可选）。</param>
        /// <returns>表单行和枚举字段的元组。</returns>
        public static (VisualElement row, EnumField field) CreateEnumFieldRow<T>(
            string label,
            T initialValue,
            Action<T> onValueChanged,
            string tooltip = null) where T : Enum
        {
            var field = new EnumField(initialValue);
            field.RegisterValueChangedCallback(evt =>
            {
                if (evt.newValue is T typedValue)
                    onValueChanged?.Invoke(typedValue);
            });

            var row = CreateCompactFormRow(label, field, tooltip);
            return (row, field);
        }

        #endregion

        #region Toggle 行

        /// <summary>
        /// 创建 Toggle 行。
        /// </summary>
        /// <param name="label">标签文本。</param>
        /// <param name="initialValue">初始值。</param>
        /// <param name="onValueChanged">值变更回调。</param>
        /// <param name="tooltip">提示文本（可选）。</param>
        /// <returns>表单行和 Toggle 的元组。</returns>
        public static (VisualElement row, Toggle toggle) CreateToggleRow(
            string label,
            bool initialValue,
            Action<bool> onValueChanged,
            string tooltip = null)
        {
            var toggle = new Toggle { value = initialValue };
            toggle.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));

            var row = CreateCompactFormRow(label, toggle, tooltip);
            return (row, toggle);
        }

        #endregion
    }
}
#endif
#endif
