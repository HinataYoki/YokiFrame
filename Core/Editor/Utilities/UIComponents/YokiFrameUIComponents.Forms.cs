#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UI 组件 - 表单控件
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region Toggle 开关

        /// <summary>
        /// 创建现代化 Toggle 开关组件
        /// </summary>
        public static VisualElement CreateModernToggle(string label, bool initialValue, Action<bool> onValueChanged)
        {
            var container = new VisualElement();
            container.AddToClassList("modern-toggle");
            if (initialValue) container.AddToClassList("checked");
            
            // 开关轨道
            var track = new VisualElement();
            track.AddToClassList("modern-toggle-track");
            
            // 开关滑块
            var thumb = new VisualElement();
            thumb.AddToClassList("modern-toggle-thumb");
            track.Add(thumb);
            
            container.Add(track);
            
            // 标签
            var labelElement = new Label(label);
            labelElement.AddToClassList("modern-toggle-label");
            container.Add(labelElement);
            
            // 点击事件
            container.RegisterCallback<ClickEvent>(_ =>
            {
                bool isChecked = container.ClassListContains("checked");
                if (isChecked)
                    container.RemoveFromClassList("checked");
                else
                    container.AddToClassList("checked");
                onValueChanged?.Invoke(!isChecked);
            });
            
            return container;
        }

        #endregion

        #region 信息行

        /// <summary>
        /// 创建信息行（标签 + 值）
        /// </summary>
        public static (VisualElement row, Label valueLabel) CreateInfoRow(string label, string initialValue = "-")
        {
            var row = new VisualElement();
            row.AddToClassList("info-row");
            
            var labelElement = new Label(label);
            labelElement.AddToClassList("info-label");
            row.Add(labelElement);
            
            var valueElement = new Label(initialValue);
            valueElement.AddToClassList("info-value");
            row.Add(valueElement);
            
            return (row, valueElement);
        }

        /// <summary>
        /// 创建配置行（标签 + 整数输入框）
        /// 使用 TextField 实现以兼容 Unity 2021.3+
        /// </summary>
        public static (VisualElement row, TextField field) CreateIntConfigRow(
            string label, 
            int initialValue, 
            Action<int> onValueChanged,
            int minValue = int.MinValue)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 8;
            row.style.paddingTop = 4;
            row.style.paddingBottom = 4;
            
            var labelElement = new Label(label);
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.fontSize = 13;
            labelElement.style.flexGrow = 1;
            row.Add(labelElement);
            
            // 使用 TextField 替代 IntegerField 以兼容 Unity 2021.3
            var field = new TextField { value = initialValue.ToString() };
            field.style.width = 90;
            field.style.minWidth = 90;
            field.RegisterValueChangedCallback(evt =>
            {
                if (int.TryParse(evt.newValue, out int parsed))
                {
                    int newValue = minValue != int.MinValue ? Mathf.Max(minValue, parsed) : parsed;
                    onValueChanged?.Invoke(newValue);
                }
                else if (string.IsNullOrEmpty(evt.newValue))
                {
                    // 空值时使用最小值或 0
                    onValueChanged?.Invoke(minValue != int.MinValue ? minValue : 0);
                }
            });
            row.Add(field);
            
            return (row, field);
        }

        #endregion

        #region 带标签的表单控件

        /// <summary>
        /// 创建带标签的 ObjectField
        /// </summary>
        public static (VisualElement row, ObjectField field) CreateObjectFieldRow<T>(
            string label, 
            UnityEngine.Object initialValue,
            Action<T> onValueChanged) where T : UnityEngine.Object
        {
            var row = CreateRow();
            
            var labelElement = new Label(label);
            labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            labelElement.style.marginRight = Spacing.XS;
            row.Add(labelElement);

            var field = new ObjectField();
            field.objectType = typeof(T);
            field.value = initialValue;
            field.style.width = 200;
            field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue as T));
            row.Add(field);

            return (row, field);
        }

        /// <summary>
        /// 创建带标签的下拉框
        /// </summary>
        public static (VisualElement row, DropdownField dropdown) CreateDropdownRow(
            string label,
            List<string> choices,
            int initialIndex,
            Action<string> onValueChanged)
        {
            var row = CreateRow();
            
            var labelElement = new Label(label);
            labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            labelElement.style.marginRight = Spacing.XS;
            row.Add(labelElement);

            var dropdown = new DropdownField();
            dropdown.choices = choices;
            dropdown.index = initialIndex;
            dropdown.style.width = 100;
            dropdown.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
            row.Add(dropdown);

            return (row, dropdown);
        }

        /// <summary>
        /// 创建带标签的搜索框
        /// </summary>
        public static (VisualElement row, TextField field) CreateSearchFieldRow(
            string label,
            string initialValue,
            Action<string> onValueChanged)
        {
            var row = CreateRow();
            
            var labelElement = new Label(label);
            labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            labelElement.style.marginRight = Spacing.XS;
            row.Add(labelElement);

            var field = new TextField();
            field.value = initialValue;
            field.style.width = 150;
            field.RegisterValueChangedCallback(evt => onValueChanged?.Invoke(evt.newValue));
            row.Add(field);

            return (row, field);
        }

        /// <summary>
        /// 创建路径选择行（文本框 + 浏览按钮）
        /// </summary>
        public static (VisualElement row, TextField field) CreatePathFieldRow(
            string label,
            string initialValue,
            Action<string> onBrowseClicked)
        {
            var row = CreateRow();
            
            var labelElement = new Label(label);
            labelElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            labelElement.style.marginRight = Spacing.XS;
            row.Add(labelElement);

            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.flexGrow = 1;
            row.Add(pathContainer);

            var field = new TextField();
            field.style.flexGrow = 1;
            field.value = initialValue;
            field.SetEnabled(false);
            pathContainer.Add(field);

            var browseBtn = new Button(() => onBrowseClicked?.Invoke(field.value)) { text = "..." };
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = Spacing.XS;
            pathContainer.Add(browseBtn);

            return (row, field);
        }

        #endregion
    }
}
#endif
