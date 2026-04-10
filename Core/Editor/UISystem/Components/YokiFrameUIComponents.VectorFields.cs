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
    /// Standard vector field factories used by the editor UI system.
    /// </summary>
    public static partial class YokiFrameUIComponents
    {
        #region Standard Vector Inputs

        /// <summary>
        /// Creates a styled <see cref="Vector2"/> input.
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
        /// Creates a styled <see cref="Vector2Int"/> input.
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

        /// <summary>
        /// Creates a styled <see cref="Vector3"/> input.
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
        /// Creates a styled <see cref="Vector3Int"/> input.
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

        /// <summary>
        /// Creates a styled <see cref="Vector4"/> input.
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

        #region Helpers

        /// <summary>
        /// Creates a custom float axis input.
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

            inputField.RegisterCallback<FocusOutEvent>(_ =>
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
        /// Creates a custom integer axis input.
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

            inputField.RegisterCallback<FocusOutEvent>(_ =>
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
    }
}
#endif
