#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_2022_1_OR_NEWER
using FloatField = UnityEngine.UIElements.FloatField;
using IntegerField = UnityEngine.UIElements.IntegerField;
#else
using FloatField = UnityEditor.UIElements.FloatField;
using IntegerField = UnityEditor.UIElements.IntegerField;
#endif

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Figma-style compact vector field factories.
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        /// <summary>
        /// Creates a single-row Figma-style <see cref="Vector2"/> input.
        /// </summary>
        public static VisualElement CreateFigmaVector2Input(string label, Vector2 value, Action<Vector2> onChanged, Action onSwap = null)
        {
            var row = new VisualElement();
            row.AddToClassList("yoki-res-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var mainLabel = new Label(label);
            mainLabel.AddToClassList("unity-base-field__label");
            mainLabel.style.minWidth = 120;
            mainLabel.style.flexShrink = 0;
            row.Add(mainLabel);

            var inputGroup = new VisualElement();
            inputGroup.AddToClassList("yoki-res-input-group");
            inputGroup.style.flexDirection = FlexDirection.Row;
            inputGroup.style.alignItems = Align.Center;
            inputGroup.style.flexGrow = 1;

            var currentValue = value;

            var xAxis = CreateFigmaAxis("X");
            var xField = new FloatField { value = value.x, label = string.Empty, name = "ResX" };
            xField.AddToClassList("yoki-res-float");
            xField.style.flexGrow = 1;
            xField.RegisterValueChangedCallback(evt =>
            {
                currentValue.x = evt.newValue;
                onChanged?.Invoke(currentValue);
            });
            xAxis.Add(xField);
            inputGroup.Add(xAxis);

            var yAxis = CreateFigmaAxis("Y");
            var yField = new FloatField { value = value.y, label = string.Empty, name = "ResY" };
            yField.AddToClassList("yoki-res-float");
            yField.style.flexGrow = 1;
            yField.RegisterValueChangedCallback(evt =>
            {
                currentValue.y = evt.newValue;
                onChanged?.Invoke(currentValue);
            });
            yAxis.Add(yField);
            inputGroup.Add(yAxis);

            AddSwapButton(inputGroup, onSwap, () =>
            {
                var temp = xField.value;
                xField.value = yField.value;
                yField.value = temp;
                currentValue = new Vector2(xField.value, yField.value);
                onChanged?.Invoke(currentValue);
            });

            row.Add(inputGroup);
            return row;
        }

        /// <summary>
        /// Creates a single-row Figma-style <see cref="Vector2Int"/> input.
        /// </summary>
        public static VisualElement CreateFigmaVector2IntInput(string label, Vector2Int value, Action<Vector2Int> onChanged, Action onSwap = null)
        {
            var row = new VisualElement();
            row.AddToClassList("yoki-res-row");
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            var mainLabel = new Label(label);
            mainLabel.AddToClassList("unity-base-field__label");
            mainLabel.style.minWidth = 120;
            mainLabel.style.flexShrink = 0;
            row.Add(mainLabel);

            var inputGroup = new VisualElement();
            inputGroup.AddToClassList("yoki-res-input-group");
            inputGroup.style.flexDirection = FlexDirection.Row;
            inputGroup.style.alignItems = Align.Center;
            inputGroup.style.flexGrow = 1;

            var currentValue = value;

            var xAxis = CreateFigmaAxis("X");
            var xField = new IntegerField { value = value.x, label = string.Empty, name = "ResX" };
            xField.AddToClassList("yoki-res-float");
            xField.style.flexGrow = 1;
            xField.RegisterValueChangedCallback(evt =>
            {
                currentValue.x = evt.newValue;
                onChanged?.Invoke(currentValue);
            });
            xAxis.Add(xField);
            inputGroup.Add(xAxis);

            var yAxis = CreateFigmaAxis("Y");
            var yField = new IntegerField { value = value.y, label = string.Empty, name = "ResY" };
            yField.AddToClassList("yoki-res-float");
            yField.style.flexGrow = 1;
            yField.RegisterValueChangedCallback(evt =>
            {
                currentValue.y = evt.newValue;
                onChanged?.Invoke(currentValue);
            });
            yAxis.Add(yField);
            inputGroup.Add(yAxis);

            AddSwapButton(inputGroup, onSwap, () =>
            {
                var temp = xField.value;
                xField.value = yField.value;
                yField.value = temp;
                currentValue = new Vector2Int(xField.value, yField.value);
                onChanged?.Invoke(currentValue);
            });

            row.Add(inputGroup);
            return row;
        }

        private static VisualElement CreateFigmaAxis(string axis)
        {
            var axisContainer = new VisualElement();
            axisContainer.AddToClassList("yoki-res-axis");
            axisContainer.style.flexDirection = FlexDirection.Row;
            axisContainer.style.alignItems = Align.Center;
            axisContainer.style.flexGrow = 1;

            var prefix = new Label(axis);
            prefix.AddToClassList("yoki-res-prefix");
            prefix.style.flexShrink = 0;
            axisContainer.Add(prefix);

            return axisContainer;
        }

        private static void AddSwapButton(VisualElement inputGroup, Action onSwap, Action applySwap)
        {
            if (onSwap == null)
            {
                return;
            }

            var swapBtn = new Button(() =>
            {
                applySwap?.Invoke();
                onSwap?.Invoke();
            })
            {
                text = "<>",
                name = "BtnSwapRes",
            };
            swapBtn.AddToClassList("yoki-res-btn");
            swapBtn.style.minWidth = 24;
            swapBtn.style.maxWidth = 24;
            swapBtn.style.minHeight = 24;
            swapBtn.style.maxHeight = 24;
            swapBtn.style.flexShrink = 0;
            inputGroup.Add(swapBtn);
        }
    }
}
#endif
