#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;

namespace YokiFrame
{
    /// <summary>使用 InspectorKit 展示预定义 UILevel，并保留自定义排序值编辑。</summary>
    [CustomPropertyDrawer(typeof(UILevel))]
    public sealed class UILevelPropertyDrawer : PropertyDrawer
    {
        /// <summary>创建 UI Toolkit 层级下拉与自定义整数输入。</summary>
        /// <param name="property">待绘制的 UILevel 序列化属性。</param>
        /// <returns>InspectorKit 样式的层级字段。</returns>
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = InspectorKitUi.CreateRoot();
            SerializedProperty order = property.FindPropertyRelative("mOrder");
            if (order == null)
            {
                root.Add(InspectorKitUi.CreateInfoBox(
                    "UILevel 缺少 mOrder 序列化字段。",
                    InspectorInfoBoxType.Error));
                return root;
            }

            void Build(VisualElement container)
            {
                container.Clear();
                IReadOnlyList<string> choices = BuildChoices(order, out int selectedIndex);
                container.Add(InspectorKitUi.CreateDropdownRow(
                    property.displayName,
                    choices,
                    selectedIndex,
                    index => ApplyPredefinedValue(order, index)));
                container.Add(InspectorKitUi.CreateIntegerRow(order, "排序值"));
            }

            Build(root);
            root.TrackPropertyValue(order, _ => Build(root));
            return root;
        }

        /// <summary>绘制不支持 UI Toolkit 时的 IMGUI 回退，并提供排序值编辑。</summary>
        /// <param name="position">字段区域。</param>
        /// <param name="property">待绘制的 UILevel 属性。</param>
        /// <param name="label">字段标签。</param>
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            SerializedProperty order = property.FindPropertyRelative("mOrder");
            if (order == null)
            {
                EditorGUI.LabelField(position, label.text, "Cannot find mOrder");
                EditorGUI.EndProperty();
                return;
            }

            Rect popupRect = new(
                position.x,
                position.y,
                position.width,
                EditorGUIUtility.singleLineHeight);
            IReadOnlyList<string> choices = BuildChoices(order, out int selectedIndex);
            string[] options = new string[choices.Count];
            for (var index = 0; index < choices.Count; index++) options[index] = choices[index];
            EditorGUI.BeginChangeCheck();
            int nextIndex = EditorGUI.Popup(popupRect, label.text, selectedIndex, options);
            if (EditorGUI.EndChangeCheck()) ApplyPredefinedValue(order, nextIndex);

            Rect orderRect = new(
                position.x,
                popupRect.yMax + EditorGUIUtility.standardVerticalSpacing,
                position.width,
                EditorGUIUtility.singleLineHeight);
            EditorGUI.PropertyField(orderRect, order, new GUIContent("排序值"));
            EditorGUI.EndProperty();
        }

        /// <summary>为 UI Toolkit 和 IMGUI 构造一致的预定义/自定义选项。</summary>
        /// <param name="order">排序值属性。</param>
        /// <param name="selectedIndex">当前选择索引。</param>
        /// <returns>显示选项。</returns>
        private static IReadOnlyList<string> BuildChoices(
            SerializedProperty order,
            out int selectedIndex)
        {
            IReadOnlyList<string> names = UILevel.PredefinedLevelNames;
            IReadOnlyList<UILevel> levels = UILevel.PredefinedLevels;
            List<string> choices = new(names.Count + 1);
            selectedIndex = -1;
            for (var index = 0; index < names.Count; index++)
            {
                choices.Add(names[index]);
                if (levels[index].Order == order.intValue) selectedIndex = index;
            }

            if (selectedIndex < 0)
            {
                choices.Add("自定义 (" + order.intValue + ")");
                selectedIndex = choices.Count - 1;
            }

            return choices;
        }

        /// <summary>把下拉选择写回排序值并提交当前 SerializedObject。</summary>
        /// <param name="order">排序值属性。</param>
        /// <param name="index">下拉选项索引。</param>
        private static void ApplyPredefinedValue(SerializedProperty order, int index)
        {
            IReadOnlyList<UILevel> levels = UILevel.PredefinedLevels;
            if (index < 0 || index >= levels.Count) return;
            order.intValue = levels[index].Order;
            order.serializedObject.ApplyModifiedProperties();
        }

        /// <summary>提供两行 IMGUI 高度，避免自定义排序值覆盖后续字段。</summary>
        /// <param name="property">待绘制属性。</param>
        /// <param name="label">字段标签。</param>
        /// <returns>两行字段高度。</returns>
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUIUtility.singleLineHeight * 2f + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
#endif
