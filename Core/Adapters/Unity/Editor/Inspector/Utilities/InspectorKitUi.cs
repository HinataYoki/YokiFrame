#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace YokiFrame.Unity.Inspector
{
    /// <summary>
    /// InspectorKit 的 UI Toolkit 构建工具。
    /// 该类型只负责把 SerializedProperty 和 Inspector 元数据转换为视觉树。
    /// </summary>
    public static partial class InspectorKitUi
    {
        /// <summary>
        /// 创建带 InspectorKit 样式的根元素。
        /// </summary>
        /// <returns>可绑定到 SerializedObject 的根元素。</returns>
        public static VisualElement CreateRoot()
        {
            VisualElement root = new VisualElement();
            root.AddToClassList("yoki-editor-inspector");
            YokiFrameEditorStyleService.Apply(root, YokiFrameEditorStyleProfile.Inspector);
            return root;
        }

        /// <summary>
        /// 将序列化对象的顶层字段按元数据构建为 UI Toolkit 字段。
        /// </summary>
        /// <param name="serializedObject">当前 Inspector 的序列化对象。</param>
        /// <param name="targetType">目标组件类型，用于读取字段元数据。</param>
        /// <returns>字段视觉树容器。</returns>
        public static VisualElement CreatePropertyFields(SerializedObject serializedObject, Type targetType)
        {
            return CreatePropertyFields(serializedObject, targetType, default);
        }

        /// <summary>
        /// 将通过筛选的顶层序列化字段按元数据构建为 UI Toolkit 字段。
        /// </summary>
        /// <param name="serializedObject">当前 Inspector 的序列化对象。</param>
        /// <param name="targetType">目标组件类型，用于读取字段元数据。</param>
        /// <param name="includeProperty">字段筛选器；为空时包含全部可见字段。</param>
        /// <returns>字段视觉树容器。</returns>
        public static VisualElement CreatePropertyFields(
            SerializedObject serializedObject,
            Type targetType,
            Func<SerializedProperty, bool> includeProperty)
        {
            VisualElement container = new VisualElement();
            if (serializedObject == null || targetType == null)
                return container;

            SerializedProperty iterator = serializedObject.GetIterator();
            if (!iterator.NextVisible(true))
                return container;

            do
            {
                if (iterator.name == "m_Script")
                    continue;

                SerializedProperty property = iterator.Copy();
                if (includeProperty != null && !includeProperty(property))
                    continue;

                FieldInfo field = FindField(targetType, property.name);
                AddFieldDecorators(container, field);

                PropertyField propertyField = new PropertyField(property, property.displayName);
                propertyField.AddToClassList("yoki-editor-inspector__field");
                if (field != null && field.GetCustomAttribute<InspectorReadOnlyAttribute>() != null)
                {
                    propertyField.AddToClassList("yoki-editor-inspector__field--readonly");
                    propertyField.SetEnabled(false);
                }

                propertyField.BindProperty(property);
                container.Add(propertyField);
            }
            while (iterator.NextVisible(false));

            return container;
        }

        /// <summary>
        /// 创建当前类型声明的 Inspector 按钮区域。
        /// </summary>
        /// <param name="targetType">目标组件类型。</param>
        /// <param name="invoke">按钮点击后的方法调用回调。</param>
        /// <returns>按钮区域；没有按钮时返回空容器。</returns>
        public static VisualElement CreateActionButtons(Type targetType, Action<MethodInfo> invoke)
        {
            VisualElement container = new VisualElement();
            if (targetType == null || invoke == null)
                return container;

            MethodInfo[] methods = targetType.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            for (int index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                InspectorButtonAttribute attribute = method.GetCustomAttribute<InspectorButtonAttribute>();
                if (attribute == null || method.IsSpecialName || method.GetParameters().Length != 0)
                    continue;

                if (container.childCount == 0)
                    container.AddToClassList("yoki-editor-inspector__actions");

                Button button = new Button(() => invoke(method));
                button.text = string.IsNullOrEmpty(attribute.Label) ? method.Name : attribute.Label;
                button.AddToClassList("yoki-editor-inspector__action");
                container.Add(button);
            }

            return container;
        }

        /// <summary>
        /// 创建 Inspector 分组标题。
        /// </summary>
        /// <param name="title">标题文本。</param>
        /// <returns>分组标题元素。</returns>
        public static VisualElement CreateSection(string title)
        {
            VisualElement section = new VisualElement();
            section.AddToClassList("yoki-editor-inspector__section");

            Label label = new Label(title ?? string.Empty);
            label.AddToClassList("yoki-editor-inspector__section-title");
            section.Add(label);
            return section;
        }

        /// <summary>
        /// 创建 Inspector 信息提示卡片。
        /// </summary>
        /// <param name="message">提示内容。</param>
        /// <param name="type">提示级别。</param>
        /// <returns>信息提示元素。</returns>
        public static VisualElement CreateInfoBox(string message, InspectorInfoBoxType type)
        {
            VisualElement box = new VisualElement();
            box.AddToClassList("yoki-editor-inspector__info");
            if (type != InspectorInfoBoxType.Info)
                box.AddToClassList("yoki-editor-inspector__info--" + type.ToString().ToLowerInvariant());

            Label label = new Label(message ?? string.Empty);
            label.AddToClassList("yoki-editor-inspector__info-label");
            box.Add(label);
            return box;
        }

        /// <summary>
        /// 添加字段上的 section 和 info box 元数据。
        /// </summary>
        /// <param name="container">字段容器。</param>
        /// <param name="field">字段反射信息。</param>
        private static void AddFieldDecorators(VisualElement container, FieldInfo field)
        {
            if (field == null)
                return;

            InspectorSectionAttribute section = field.GetCustomAttribute<InspectorSectionAttribute>();
            if (section != null)
                container.Add(CreateSection(section.Title));

            InspectorInfoBoxAttribute infoBox = field.GetCustomAttribute<InspectorInfoBoxAttribute>();
            if (infoBox != null)
                container.Add(CreateInfoBox(infoBox.Message, infoBox.Type));
        }

        /// <summary>
        /// 从目标类型及其基类中查找序列化字段。
        /// </summary>
        /// <param name="targetType">目标类型。</param>
        /// <param name="fieldName">字段名称。</param>
        /// <returns>找到的字段；找不到时返回 null。</returns>
        private static FieldInfo FindField(Type targetType, string fieldName)
        {
            Type currentType = targetType;
            while (currentType != null)
            {
                FieldInfo field = currentType.GetField(
                    fieldName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field;

                currentType = currentType.BaseType;
            }

            return null;
        }
    }
}
#endif
