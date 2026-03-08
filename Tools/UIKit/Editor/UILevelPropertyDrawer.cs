#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// UILevel 的 Inspector 自定义绘制器
    /// <para>支持预定义层级下拉 + 自定义 Order 值显示</para>
    /// </summary>
    [CustomPropertyDrawer(typeof(UILevel))]
    public class UILevelPropertyDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;

            var orderProp = property.FindPropertyRelative("mOrder");
            if (orderProp == null) return container;

            // 构建选项列表
            var predefined = UILevel.PredefinedLevels;
            var predefinedNames = UILevel.PredefinedLevelNames;
            var choices = new List<string>(predefined.Count + 1);
            for (int i = 0; i < predefinedNames.Count; i++)
            {
                choices.Add(predefinedNames[i]);
            }

            // 判断当前值是否为预定义层级
            int currentOrder = orderProp.intValue;
            int selectedIndex = -1;
            for (int i = 0; i < predefined.Count; i++)
            {
                if (predefined[i].Order == currentOrder)
                {
                    selectedIndex = i;
                    break;
                }
            }

            // 如果不是预定义值，添加一个 Custom 选项
            string customLabel = $"Custom ({currentOrder})";
            if (selectedIndex < 0)
            {
                choices.Add(customLabel);
                selectedIndex = choices.Count - 1;
            }

            var dropdown = new DropdownField(property.displayName, choices, selectedIndex);
            dropdown.style.flexGrow = 1;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                // 尝试解析为预定义层级
                if (UILevel.TryParse(evt.newValue, out var parsed))
                {
                    orderProp.intValue = parsed.Order;
                    orderProp.serializedObject.ApplyModifiedProperties();
                }
            });

            container.Add(dropdown);
            return container;
        }

        /// <summary>
        /// IMGUI 回退（兼容不支持 UIToolkit 的场景）
        /// </summary>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var orderProp = property.FindPropertyRelative("mOrder");
            if (orderProp == null)
            {
                EditorGUI.LabelField(position, label.text, "Cannot find mOrder");
                EditorGUI.EndProperty();
                return;
            }

            int currentOrder = orderProp.intValue;
            var predefined = UILevel.PredefinedLevels;
            var predefinedNames = UILevel.PredefinedLevelNames;

            // 构建 GUIContent 选项
            var options = new List<string>(predefined.Count);
            int selectedIndex = -1;
            for (int i = 0; i < predefined.Count; i++)
            {
                options.Add(predefinedNames[i]);
                if (predefined[i].Order == currentOrder) selectedIndex = i;
            }

            // 自定义值
            if (selectedIndex < 0)
            {
                options.Add($"Custom ({currentOrder})");
                selectedIndex = options.Count - 1;
            }

            int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, options.ToArray());
            if (newIndex != selectedIndex && newIndex >= 0 && newIndex < predefined.Count)
            {
                orderProp.intValue = predefined[newIndex].Order;
            }

            EditorGUI.EndProperty();
        }
    }
}
#endif
