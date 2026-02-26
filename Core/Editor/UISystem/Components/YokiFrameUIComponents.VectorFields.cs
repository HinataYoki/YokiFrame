#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

#if UNITY_2023_1_OR_NEWER
using FloatField = UnityEngine.UIElements.FloatField;
using IntegerField = UnityEngine.UIElements.IntegerField;
#else
using FloatField = UnityEditor.UIElements.FloatField;
using IntegerField = UnityEditor.UIElements.IntegerField;
#endif

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrameUIComponents - Vector 输入组件
    /// 提供现代化的 Vector2/Vector3/Vector4 输入框（完全自定义设计）
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region Vector2 输入框

        /// <summary>
        /// 创建 Vector2 输入框（现代化样式）
        /// </summary>
        public static VisualElement CreateVector2Input(Vector2 value, Action<Vector2> onChanged, bool showLabels = true)
        {
            var container = new VisualElement();
            container.AddToClassList("yoki-vector-input");
            container.AddToClassList("yoki-vector-input--vec2");

            var currentValue = value;
            
            var xField = CreateCustomAxisInput("X", value.x, v => 
            {
                currentValue.x = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            var yField = CreateCustomAxisInput("Y", value.y, v => 
            {
                currentValue.y = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            container.Add(xField);
            container.Add(yField);

            return container;
        }

        /// <summary>
        /// 创建 Vector2Int 输入框（现代化样式）
        /// </summary>
        public static VisualElement CreateVector2IntInput(Vector2Int value, Action<Vector2Int> onChanged, bool showLabels = true)
        {
            var container = new VisualElement();
            container.AddToClassList("yoki-vector-input");
            container.AddToClassList("yoki-vector-input--vec2");

            var currentValue = value;
            
            var xField = CreateCustomAxisIntInput("X", value.x, v => 
            {
                currentValue.x = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            var yField = CreateCustomAxisIntInput("Y", value.y, v => 
            {
                currentValue.y = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            container.Add(xField);
            container.Add(yField);

            return container;
        }

        #endregion

        #region Vector3 输入框

        /// <summary>
        /// 创建 Vector3 输入框（现代化样式）
        /// </summary>
        public static VisualElement CreateVector3Input(Vector3 value, Action<Vector3> onChanged, bool showLabels = true)
        {
            var container = new VisualElement();
            container.AddToClassList("yoki-vector-input");
            container.AddToClassList("yoki-vector-input--vec3");

            var currentValue = value;
            
            var xField = CreateCustomAxisInput("X", value.x, v => 
            {
                currentValue.x = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            var yField = CreateCustomAxisInput("Y", value.y, v => 
            {
                currentValue.y = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            var zField = CreateCustomAxisInput("Z", value.z, v => 
            {
                currentValue.z = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            container.Add(xField);
            container.Add(yField);
            container.Add(zField);

            return container;
        }

        /// <summary>
        /// 创建 Vector3Int 输入框（现代化样式）
        /// </summary>
        public static VisualElement CreateVector3IntInput(Vector3Int value, Action<Vector3Int> onChanged, bool showLabels = true)
        {
            var container = new VisualElement();
            container.AddToClassList("yoki-vector-input");
            container.AddToClassList("yoki-vector-input--vec3");

            var currentValue = value;
            
            var xField = CreateCustomAxisIntInput("X", value.x, v => 
            {
                currentValue.x = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            var yField = CreateCustomAxisIntInput("Y", value.y, v => 
            {
                currentValue.y = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            var zField = CreateCustomAxisIntInput("Z", value.z, v => 
            {
                currentValue.z = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            container.Add(xField);
            container.Add(yField);
            container.Add(zField);

            return container;
        }

        #endregion

        #region Vector4 输入框

        /// <summary>
        /// 创建 Vector4 输入框（现代化样式）
        /// </summary>
        public static VisualElement CreateVector4Input(Vector4 value, Action<Vector4> onChanged, bool showLabels = true)
        {
            var container = new VisualElement();
            container.AddToClassList("yoki-vector-input");
            container.AddToClassList("yoki-vector-input--vec4");

            var currentValue = value;
            
            var xField = CreateCustomAxisInput("X", value.x, v => 
            {
                currentValue.x = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            var yField = CreateCustomAxisInput("Y", value.y, v => 
            {
                currentValue.y = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            var zField = CreateCustomAxisInput("Z", value.z, v => 
            {
                currentValue.z = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            var wField = CreateCustomAxisInput("W", value.w, v => 
            {
                currentValue.w = v;
                onChanged?.Invoke(currentValue);
            }, showLabels);

            container.Add(xField);
            container.Add(yField);
            container.Add(zField);
            container.Add(wField);

            return container;
        }

        #endregion

        #region 内部辅助方法 - 完全自定义设计

        /// <summary>
        /// 创建自定义轴输入框（Float）- 完全自定义设计
        /// </summary>
        private static VisualElement CreateCustomAxisInput(string axisLabel, float value, Action<float> onChanged, bool showLabel)
        {
            var axisContainer = new VisualElement();
            axisContainer.AddToClassList("yoki-vector-axis");

            if (showLabel)
            {
                var label = new Label(axisLabel);
                label.AddToClassList("yoki-vector-axis__label");
                axisContainer.Add(label);
            }

            var inputField = new TextField { value = value.ToString("F2") };
            inputField.AddToClassList("yoki-vector-axis__input");
            
            inputField.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (float.TryParse(inputField.value, out var newValue))
                {
                    inputField.value = newValue.ToString("F2");
                    onChanged?.Invoke(newValue);
                }
                else
                {
                    inputField.value = value.ToString("F2");
                }
            });

            inputField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (float.TryParse(inputField.value, out var newValue))
                    {
                        inputField.value = newValue.ToString("F2");
                        onChanged?.Invoke(newValue);
                    }
                    else
                    {
                        inputField.value = value.ToString("F2");
                    }
                    inputField.Blur();
                }
            });

            axisContainer.Add(inputField);

            return axisContainer;
        }

        /// <summary>
        /// 创建自定义轴输入框（Int）- 完全自定义设计
        /// </summary>
        private static VisualElement CreateCustomAxisIntInput(string axisLabel, int value, Action<int> onChanged, bool showLabel)
        {
            var axisContainer = new VisualElement();
            axisContainer.AddToClassList("yoki-vector-axis");

            if (showLabel)
            {
                var label = new Label(axisLabel);
                label.AddToClassList("yoki-vector-axis__label");
                axisContainer.Add(label);
            }

            var inputField = new TextField { value = value.ToString() };
            inputField.AddToClassList("yoki-vector-axis__input");
            
            inputField.RegisterCallback<FocusOutEvent>(evt =>
            {
                if (int.TryParse(inputField.value, out var newValue))
                {
                    inputField.value = newValue.ToString();
                    onChanged?.Invoke(newValue);
                }
                else
                {
                    inputField.value = value.ToString();
                }
            });

            inputField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    if (int.TryParse(inputField.value, out var newValue))
                    {
                        inputField.value = newValue.ToString();
                        onChanged?.Invoke(newValue);
                    }
                    else
                    {
                        inputField.value = value.ToString();
                    }
                    inputField.Blur();
                }
            });

            axisContainer.Add(inputField);

            return axisContainer;
        }

        #endregion

        #region Figma 风格单行紧凑型 Vector2 输入框

        /// <summary>
        /// 创建 Figma 风格单行紧凑型 Vector2 输入框
        /// 严格单行水平布局：主标签 + 输入组（X轴 + Y轴 + 对调按钮）
        /// </summary>
        /// <param name="label">主标签文本（如"参考分辨率"）</param>
        /// <param name="value">初始值</param>
        /// <param name="onChanged">值变化回调</param>
        /// <param name="onSwap">对调按钮回调（可选）</param>
        /// <returns>单行紧凑型 Vector2 输入容器</returns>
        public static VisualElement CreateFigmaVector2Input(
            string label, 
            Vector2 value, 
            Action<Vector2> onChanged, 
            Action onSwap = null)
        {
            // 整行容器：横向排列
            var row = new VisualElement();
            row.AddToClassList("yoki-res-row");
            // 强制内联样式确保横向布局
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;

            // 左侧主标签
            var mainLabel = new Label(label);
            mainLabel.AddToClassList("unity-base-field__label");
            mainLabel.style.minWidth = 120;
            mainLabel.style.flexShrink = 0;
            row.Add(mainLabel);

            // 右侧输入组容器：横向排列，占据剩余空间
            var inputGroup = new VisualElement();
            inputGroup.AddToClassList("yoki-res-input-group");
            // 强制内联样式确保横向布局
            inputGroup.style.flexDirection = FlexDirection.Row;
            inputGroup.style.alignItems = Align.Center;
            inputGroup.style.flexGrow = 1;

            var currentValue = value;

            // X 轴容器：横向排列，平分空间
            var xAxis = new VisualElement();
            xAxis.AddToClassList("yoki-res-axis");
            // 强制内联样式确保横向布局
            xAxis.style.flexDirection = FlexDirection.Row;
            xAxis.style.alignItems = Align.Center;
            xAxis.style.flexGrow = 1;

            var xPrefix = new Label("X");
            xPrefix.AddToClassList("yoki-res-prefix");
            xPrefix.style.flexShrink = 0;
            xAxis.Add(xPrefix);

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

            // Y 轴容器：横向排列，平分空间
            var yAxis = new VisualElement();
            yAxis.AddToClassList("yoki-res-axis");
            // 强制内联样式确保横向布局
            yAxis.style.flexDirection = FlexDirection.Row;
            yAxis.style.alignItems = Align.Center;
            yAxis.style.flexGrow = 1;

            var yPrefix = new Label("Y");
            yPrefix.AddToClassList("yoki-res-prefix");
            yPrefix.style.flexShrink = 0;
            yAxis.Add(yPrefix);

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

            // 对调按钮：固定 24x24
            if (onSwap != null)
            {
                var swapBtn = new Button(() =>
                {
                    // 对调 X/Y 值并更新 UI
                    var temp = xField.value;
                    xField.value = yField.value;
                    yField.value = temp;
                    
                    currentValue = new Vector2(xField.value, yField.value);
                    onChanged?.Invoke(currentValue);
                    onSwap?.Invoke();
                }) { text = "⇄", name = "BtnSwapRes" };
                swapBtn.AddToClassList("yoki-res-btn");
                // 强制内联样式确保固定尺寸
                swapBtn.style.minWidth = 24;
                swapBtn.style.maxWidth = 24;
                swapBtn.style.minHeight = 24;
                swapBtn.style.maxHeight = 24;
                swapBtn.style.flexShrink = 0;
                inputGroup.Add(swapBtn);
            }

            row.Add(inputGroup);

            return row;
        }

        /// <summary>
        /// 创建 Figma 风格单行紧凑型 Vector2Int 输入框
        /// </summary>
        public static VisualElement CreateFigmaVector2IntInput(
            string label, 
            Vector2Int value, 
            Action<Vector2Int> onChanged, 
            Action onSwap = null)
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

            var xAxis = new VisualElement();
            xAxis.AddToClassList("yoki-res-axis");
            xAxis.style.flexDirection = FlexDirection.Row;
            xAxis.style.alignItems = Align.Center;
            xAxis.style.flexGrow = 1;

            var xPrefix = new Label("X");
            xPrefix.AddToClassList("yoki-res-prefix");
            xPrefix.style.flexShrink = 0;
            xAxis.Add(xPrefix);

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

            var yAxis = new VisualElement();
            yAxis.AddToClassList("yoki-res-axis");
            yAxis.style.flexDirection = FlexDirection.Row;
            yAxis.style.alignItems = Align.Center;
            yAxis.style.flexGrow = 1;

            var yPrefix = new Label("Y");
            yPrefix.AddToClassList("yoki-res-prefix");
            yPrefix.style.flexShrink = 0;
            yAxis.Add(yPrefix);

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

            if (onSwap != null)
            {
                var swapBtn = new Button(() =>
                {
                    var temp = xField.value;
                    xField.value = yField.value;
                    yField.value = temp;
                    
                    currentValue = new Vector2Int(xField.value, yField.value);
                    onChanged?.Invoke(currentValue);
                    onSwap?.Invoke();
                }) { text = "⇄", name = "BtnSwapRes" };
                swapBtn.AddToClassList("yoki-res-btn");
                swapBtn.style.minWidth = 24;
                swapBtn.style.maxWidth = 24;
                swapBtn.style.minHeight = 24;
                swapBtn.style.maxHeight = 24;
                swapBtn.style.flexShrink = 0;
                inputGroup.Add(swapBtn);
            }

            row.Add(inputGroup);

            return row;
        }

        #endregion
    }
}
#endif
