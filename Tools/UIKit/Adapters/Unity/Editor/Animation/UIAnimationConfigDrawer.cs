#if UNITY_EDITOR && UNITY_2022_3_OR_NEWER
using System;
using UnityEditor;
using UnityEngine;

namespace YokiFrame.Editor
{
    /// <summary>为 SerializeReference 动画配置提供稳定的类型选择与子字段绘制。</summary>
    [CustomPropertyDrawer(typeof(UIAnimationConfig), true)]
    public sealed class UIAnimationConfigDrawer : PropertyDrawer
    {
        private const float INDENT = 16f;

        /// <inheritdoc />
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            Rect header = new(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
            Rect foldout = new(header.x, header.y, header.width - 150f, header.height);
            property.isExpanded = EditorGUI.Foldout(foldout, property.isExpanded, label, true);

            Rect typeButton = new(header.xMax - 146f, header.y, 146f, header.height);
            using (new EditorGUI.DisabledScope(property.serializedObject.isEditingMultipleObjects))
            {
                if (GUI.Button(typeButton, GetTypeLabel(property), EditorStyles.popup))
                    ShowTypeMenu(property);
            }

            if (property.isExpanded && property.managedReferenceValue != null)
                DrawChildren(position, property);
            EditorGUI.EndProperty();
        }

        /// <inheritdoc />
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float height = EditorGUIUtility.singleLineHeight;
            if (!property.isExpanded || property.managedReferenceValue == null) return height;

            SerializedProperty child = property.Copy();
            SerializedProperty end = child.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                if (child.depth == property.depth + 1 && ShouldDrawChild(property, child))
                    height += EditorGUIUtility.standardVerticalSpacing
                        + EditorGUI.GetPropertyHeight(child, true);
                enterChildren = false;
            }
            return height;
        }

        /// <summary>在标题下方依次绘制当前具体配置的直接子字段。</summary>
        private static void DrawChildren(Rect position, SerializedProperty property)
        {
            float y = position.y + EditorGUIUtility.singleLineHeight
                + EditorGUIUtility.standardVerticalSpacing;
            SerializedProperty child = property.Copy();
            SerializedProperty end = child.GetEndProperty();
            bool enterChildren = true;
            while (child.NextVisible(enterChildren) && !SerializedProperty.EqualContents(child, end))
            {
                if (child.depth == property.depth + 1 && ShouldDrawChild(property, child))
                {
                    float height = EditorGUI.GetPropertyHeight(child, true);
                    Rect childRect = new(position.x + INDENT, y, position.width - INDENT, height);
                    EditorGUI.PropertyField(childRect, child, true);
                    y += height + EditorGUIUtility.standardVerticalSpacing;
                }
                enterChildren = false;
            }
        }

        /// <summary>创建包含清除项和全部内置配置类型的选择菜单。</summary>
        private static void ShowTypeMenu(SerializedProperty property)
        {
            var menu = new GenericMenu();
            AddMenuItem(menu, property, "无", null);
            menu.AddSeparator(string.Empty);
            AddMenuItem(menu, property, "淡入淡出", typeof(FadeAnimationConfig));
            AddMenuItem(menu, property, "缩放", typeof(ScaleAnimationConfig));
            AddMenuItem(menu, property, "滑动", typeof(SlideAnimationConfig));
            AddMenuItem(menu, property, "组合", typeof(CompositeAnimationConfig));
            menu.ShowAsContext();
        }

        /// <summary>向菜单添加一个类型项，并在选择后提交 Undo 与序列化写入。</summary>
        private static void AddMenuItem(
            GenericMenu menu,
            SerializedProperty property,
            string label,
            Type configType)
        {
            Type currentType = property.managedReferenceValue?.GetType();
            menu.AddItem(new GUIContent(label), currentType == configType, () =>
            {
                SerializedObject serializedObject = property.serializedObject;
                Undo.RecordObjects(serializedObject.targetObjects, "Change UIKit Animation");
                serializedObject.Update();
                SerializedProperty current = serializedObject.FindProperty(property.propertyPath);
                current.managedReferenceValue = configType == null
                    ? null
                    : Activator.CreateInstance(configType);
                current.isExpanded = configType != null;
                serializedObject.ApplyModifiedProperties();
            });
        }

        /// <summary>获取当前配置类型的简短中文显示名。</summary>
        private static string GetTypeLabel(SerializedProperty property)
        {
            object value = property.managedReferenceValue;
            if (value == null) return "未配置";
            if (value is FadeAnimationConfig) return "淡入淡出";
            if (value is ScaleAnimationConfig) return "缩放";
            if (value is SlideAnimationConfig) return "滑动";
            if (value is CompositeAnimationConfig) return "组合";
            return value.GetType().Name;
        }

        /// <summary>组合配置的时长和曲线由子动画决定，因此不显示无效基础字段。</summary>
        private static bool ShouldDrawChild(
            SerializedProperty property,
            SerializedProperty child)
        {
            if (!(property.managedReferenceValue is CompositeAnimationConfig)) return true;
            return child.name != nameof(UIAnimationConfig.Duration)
                && child.name != nameof(UIAnimationConfig.Curve);
        }
    }
}
#endif
