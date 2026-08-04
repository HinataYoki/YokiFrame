#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    internal sealed partial class UIKitPanelInspector
    {
        /// <summary>为字段创建兼容第三方标签和提示文本的 IMGUI 标签。</summary>
        /// <param name="property">当前序列化字段。</param>
        /// <param name="field">字段反射信息。</param>
        /// <returns>需要覆盖默认名称或提示时返回 GUIContent，否则返回空。</returns>
        private static GUIContent CreateCustomPropertyLabel(
            SerializedProperty property,
            MemberInfo field)
        {
            if (property == null || field == null)
                return default;

            string labelText = ResolveAttributeLabel(field);
            string tooltip = ResolveAttributeTooltip(field);
            if (string.IsNullOrEmpty(labelText) && string.IsNullOrEmpty(tooltip))
                return default;

            return new GUIContent(
                string.IsNullOrEmpty(labelText) ? property.displayName : labelText,
                tooltip);
        }

        /// <summary>绘制字段级标题和信息框，同时保留第三方属性的声明顺序。</summary>
        /// <param name="memberInfo">字段或属性的反射信息。</param>
        private static void DrawCustomPropertyDecorators(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return;

            string title = ResolveAttributeTitle(memberInfo);
            if (!string.IsNullOrEmpty(title))
                DrawTitleDecorator(title, ShouldDrawTitleHorizontalLine(memberInfo));

            object[] attributes = memberInfo.GetCustomAttributes(true);
            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute == null || !LooksLikeInfoBoxAttribute(attribute.GetType()))
                    continue;

                string text = TryReadStringMember(attribute, "Text")
                    ?? TryReadStringMember(attribute, "text")
                    ?? TryReadStringMember(attribute, "Message")
                    ?? TryReadStringMember(attribute, "message");
                if (!string.IsNullOrEmpty(text))
                    EditorGUILayout.HelpBox(text, ToUnityMessageType(ResolveInfoBoxType(attribute)));
            }
        }

        /// <summary>绘制兼容 TitleAttribute 的标题和可选分隔线。</summary>
        /// <param name="title">标题文本。</param>
        /// <param name="horizontalLine">是否在标题下绘制分隔线。</param>
        private static void DrawTitleDecorator(string title, bool horizontalLine)
        {
            EditorGUILayout.Space(4f);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            if (!horizontalLine)
                return;

            Rect lineRect = EditorGUILayout.GetControlRect(false, 1f);
            EditorGUI.DrawRect(lineRect, new Color(0.45f, 0.45f, 0.45f, 1f));
        }

        /// <summary>读取字段上第一个可识别标题属性的文本。</summary>
        /// <param name="memberInfo">字段或属性的反射信息。</param>
        /// <returns>标题文本；不存在时返回空。</returns>
        private static string ResolveAttributeTitle(MemberInfo memberInfo)
        {
            object[] attributes = memberInfo == null ? default : memberInfo.GetCustomAttributes(true);
            if (attributes == null)
                return default;

            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute == null || !LooksLikeTitleAttribute(attribute.GetType()))
                    continue;

                string title = TryReadStringMember(attribute, "Title")
                    ?? TryReadStringMember(attribute, "title")
                    ?? TryReadStringMember(attribute, "Text")
                    ?? TryReadStringMember(attribute, "text")
                    ?? TryReadStringMember(attribute, "Name")
                    ?? TryReadStringMember(attribute, "name");
                if (!string.IsNullOrEmpty(title))
                    return title;
            }

            return default;
        }

        /// <summary>读取标题属性的 HorizontalLine 设置，缺省保持常见工具的分隔线行为。</summary>
        /// <param name="memberInfo">字段或属性的反射信息。</param>
        /// <returns>是否绘制分隔线。</returns>
        private static bool ShouldDrawTitleHorizontalLine(MemberInfo memberInfo)
        {
            object[] attributes = memberInfo == null ? default : memberInfo.GetCustomAttributes(true);
            if (attributes == null)
                return false;

            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute == null || !LooksLikeTitleAttribute(attribute.GetType()))
                    continue;

                bool? horizontalLine = TryReadBoolMember(attribute, "HorizontalLine")
                    ?? TryReadBoolMember(attribute, "horizontalLine");
                return !horizontalLine.HasValue || horizontalLine.Value;
            }

            return false;
        }

        /// <summary>读取字段上第一个信息框属性的文本。</summary>
        /// <param name="memberInfo">字段或属性的反射信息。</param>
        /// <returns>信息框文本；不存在时返回空。</returns>
        private static string ResolveAttributeInfoBoxText(MemberInfo memberInfo)
        {
            object[] attributes = memberInfo == null ? default : memberInfo.GetCustomAttributes(true);
            if (attributes == null)
                return default;

            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute == null || !LooksLikeInfoBoxAttribute(attribute.GetType()))
                    continue;

                return TryReadStringMember(attribute, "Text")
                    ?? TryReadStringMember(attribute, "text")
                    ?? TryReadStringMember(attribute, "Message")
                    ?? TryReadStringMember(attribute, "message");
            }

            return default;
        }

        /// <summary>读取字段上第一个信息框属性的 Unity 消息级别。</summary>
        /// <param name="memberInfo">字段或属性的反射信息。</param>
        /// <returns>Unity 消息类型。</returns>
        private static MessageType ResolveAttributeInfoBoxType(MemberInfo memberInfo)
        {
            object[] attributes = memberInfo == null ? default : memberInfo.GetCustomAttributes(true);
            if (attributes == null)
                return MessageType.Info;

            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute != null && LooksLikeInfoBoxAttribute(attribute.GetType()))
                    return ToUnityMessageType(ResolveInfoBoxType(attribute));
            }

            return MessageType.Info;
        }

        /// <summary>读取信息框属性的枚举成员，兼容 TriInspector 与 InspectorKit 命名。</summary>
        /// <param name="attribute">信息框属性实例。</param>
        /// <returns>第三方消息级别对象。</returns>
        private static object ResolveInfoBoxType(object attribute)
        {
            return TryReadObjectMember(attribute, "MessageType")
                ?? TryReadObjectMember(attribute, "messageType")
                ?? TryReadObjectMember(attribute, "Type")
                ?? TryReadObjectMember(attribute, "type");
        }

        /// <summary>把第三方消息枚举名称映射到 Unity MessageType。</summary>
        /// <param name="value">第三方枚举值。</param>
        /// <returns>Unity 消息级别。</returns>
        private static MessageType ToUnityMessageType(object value)
        {
            if (value == null)
                return MessageType.Info;

            string name = value.ToString();
            if (string.Equals(name, "Warning", StringComparison.OrdinalIgnoreCase))
                return MessageType.Warning;
            if (string.Equals(name, "Error", StringComparison.OrdinalIgnoreCase))
                return MessageType.Error;
            if (string.Equals(name, "None", StringComparison.OrdinalIgnoreCase))
                return MessageType.None;
            return MessageType.Info;
        }

        /// <summary>判断字段是否声明了兼容的只读属性。</summary>
        /// <param name="memberInfo">字段或属性的反射信息。</param>
        /// <returns>声明只读属性时返回 true。</returns>
        private static bool HasReadOnlyAttribute(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return false;

            object[] attributes = memberInfo.GetCustomAttributes(true);
            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute != null && LooksLikeReadOnlyAttribute(attribute.GetType()))
                    return true;
            }

            return false;
        }

        /// <summary>读取 ListDrawerSettings 的 AlwaysExpanded，保持列表打开状态。</summary>
        /// <param name="memberInfo">字段或属性的反射信息。</param>
        /// <returns>要求始终展开时返回 true。</returns>
        private static bool ShouldForceExpandCustomProperty(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return false;

            object[] attributes = memberInfo.GetCustomAttributes(true);
            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute == null || !LooksLikeListDrawerSettingsAttribute(attribute.GetType()))
                    continue;

                bool? alwaysExpanded = TryReadBoolMember(attribute, "AlwaysExpanded")
                    ?? TryReadBoolMember(attribute, "alwaysExpanded");
                if (alwaysExpanded.HasValue && alwaysExpanded.Value)
                    return true;
            }

            return false;
        }

        /// <summary>解析第三方 LabelText、Label 或 InspectorName 属性的字段名称。</summary>
        /// <param name="memberInfo">字段或属性的反射信息。</param>
        /// <returns>覆盖后的字段名称；不存在时返回空。</returns>
        private static string ResolveAttributeLabel(MemberInfo memberInfo)
        {
            object[] attributes = memberInfo == null ? default : memberInfo.GetCustomAttributes(true);
            if (attributes == null)
                return default;

            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute == null || !LooksLikeLabelAttribute(attribute.GetType()))
                    continue;

                string label = TryReadStringMember(attribute, "Text")
                    ?? TryReadStringMember(attribute, "Label")
                    ?? TryReadStringMember(attribute, "Name")
                    ?? TryReadStringMember(attribute, "DisplayName")
                    ?? TryReadStringMember(attribute, "displayName")
                    ?? TryReadStringMember(attribute, "LabelText")
                    ?? TryReadStringMember(attribute, "text")
                    ?? TryReadStringMember(attribute, "label");
                if (!string.IsNullOrEmpty(label))
                    return label;
            }

            return default;
        }

        /// <summary>解析 Unity Tooltip 和第三方 PropertyTooltip 属性的提示文本。</summary>
        /// <param name="memberInfo">字段或属性的反射信息。</param>
        /// <returns>提示文本；不存在时返回空。</returns>
        private static string ResolveAttributeTooltip(MemberInfo memberInfo)
        {
            if (memberInfo == null)
                return default;

            object[] attributes = memberInfo.GetCustomAttributes(true);
            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute == null)
                    continue;

                Type attributeType = attribute.GetType();
                if (attributeType == typeof(TooltipAttribute) || LooksLikeTooltipAttribute(attributeType))
                {
                    string tooltip = TryReadStringMember(attribute, "tooltip")
                        ?? TryReadStringMember(attribute, "Tooltip")
                        ?? TryReadStringMember(attribute, "Text")
                        ?? TryReadStringMember(attribute, "text");
                    if (!string.IsNullOrEmpty(tooltip))
                        return tooltip;
                }
            }

            return default;
        }

        /// <summary>解析第三方 ButtonAttribute 的按钮文本，未配置文本时回退到方法名。</summary>
        /// <param name="method">按钮方法。</param>
        /// <returns>按钮显示文本。</returns>
        private static string ResolveButtonLabel(MethodInfo method)
        {
            if (method == null)
                return string.Empty;

            object[] attributes = method.GetCustomAttributes(true);
            for (var index = 0; index < attributes.Length; index++)
            {
                object attribute = attributes[index];
                if (attribute == null || !LooksLikeButtonAttribute(attribute.GetType()))
                    continue;

                string label = TryReadStringMember(attribute, "Name")
                    ?? TryReadStringMember(attribute, "Label")
                    ?? TryReadStringMember(attribute, "Text")
                    ?? TryReadStringMember(attribute, "name")
                    ?? TryReadStringMember(attribute, "label")
                    ?? TryReadStringMember(attribute, "text");
                return string.IsNullOrEmpty(label) ? method.Name : label;
            }

            return method.Name;
        }

        /// <summary>按类型名称识别标题属性，避免对 Tri/Odin 产生编译时依赖。</summary>
        /// <param name="attributeType">属性类型。</param>
        /// <returns>属于标题属性时返回 true。</returns>
        private static bool LooksLikeTitleAttribute(Type attributeType)
        {
            return attributeType != null
                && (attributeType.Name == "TitleAttribute"
                    || attributeType.Name == "InspectorSectionAttribute");
        }

        /// <summary>按类型名称识别信息框属性。</summary>
        /// <param name="attributeType">属性类型。</param>
        /// <returns>属于信息框属性时返回 true。</returns>
        private static bool LooksLikeInfoBoxAttribute(Type attributeType)
        {
            if (attributeType == null)
                return false;

            return attributeType.Name == "InfoBoxAttribute"
                || attributeType.Name == "HelpBoxAttribute"
                || attributeType.Name == "InspectorInfoBoxAttribute";
        }

        /// <summary>按类型名称识别只读属性。</summary>
        /// <param name="attributeType">属性类型。</param>
        /// <returns>属于只读属性时返回 true。</returns>
        private static bool LooksLikeReadOnlyAttribute(Type attributeType)
        {
            return attributeType != null
                && (attributeType.Name == "ReadOnlyAttribute"
                    || attributeType.Name == "InspectorReadOnlyAttribute");
        }

        /// <summary>按类型名称识别 Tooltip 和 PropertyTooltip 属性。</summary>
        /// <param name="attributeType">属性类型。</param>
        /// <returns>属于提示属性时返回 true。</returns>
        private static bool LooksLikeTooltipAttribute(Type attributeType)
        {
            if (attributeType == null)
                return false;

            return attributeType.Name == "TooltipAttribute"
                || attributeType.Name == "PropertyTooltipAttribute";
        }

        /// <summary>按类型名称识别第三方按钮属性，避免引用 TriInspector/Odin 程序集。</summary>
        /// <param name="attributeType">属性类型。</param>
        /// <returns>属于按钮属性时返回 true。</returns>
        private static bool LooksLikeButtonAttribute(Type attributeType)
        {
            return attributeType != null
                && (attributeType.Name == "ButtonAttribute"
                    || attributeType.Name == "InspectorButtonAttribute");
        }

        /// <summary>按类型名称识别列表绘制配置属性。</summary>
        /// <param name="attributeType">属性类型。</param>
        /// <returns>属于列表绘制配置时返回 true。</returns>
        private static bool LooksLikeListDrawerSettingsAttribute(Type attributeType)
        {
            return attributeType != null && attributeType.Name == "ListDrawerSettingsAttribute";
        }

        /// <summary>按类型名称识别字段标签属性。</summary>
        /// <param name="attributeType">属性类型。</param>
        /// <returns>属于标签属性时返回 true。</returns>
        private static bool LooksLikeLabelAttribute(Type attributeType)
        {
            if (attributeType == null)
                return false;

            string name = attributeType.Name;
            return name == "LabelTextAttribute"
                || name == "LabelAttribute"
                || name == "PropertyLabelAttribute"
                || name == "DisplayNameAttribute"
                || name == "InspectorNameAttribute";
        }

        /// <summary>读取属性实例上的字符串成员，兼容公开和私有实现。</summary>
        /// <param name="instance">属性实例。</param>
        /// <param name="memberName">成员名称。</param>
        /// <returns>字符串值；成员不存在或类型不符时返回空。</returns>
        private static string TryReadStringMember(object instance, string memberName)
        {
            if (instance == null)
                return default;

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.PropertyType == typeof(string))
                return property.GetValue(instance, null) as string;

            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(string)
                ? field.GetValue(instance) as string
                : default;
        }

        /// <summary>读取属性实例上的布尔成员，兼容公开和私有实现。</summary>
        /// <param name="instance">属性实例。</param>
        /// <param name="memberName">成员名称。</param>
        /// <returns>布尔值；成员不存在或类型不符时返回空。</returns>
        private static bool? TryReadBoolMember(object instance, string memberName)
        {
            if (instance == null)
                return default;

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null && property.PropertyType == typeof(bool))
                return (bool)property.GetValue(instance, null);

            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null && field.FieldType == typeof(bool)
                ? (bool?)field.GetValue(instance)
                : default;
        }

        /// <summary>读取属性实例上的任意成员，供枚举和可选配置解析使用。</summary>
        /// <param name="instance">属性实例。</param>
        /// <param name="memberName">成员名称。</param>
        /// <returns>成员值；不存在时返回空。</returns>
        private static object TryReadObjectMember(object instance, string memberName)
        {
            if (instance == null)
                return default;

            Type type = instance.GetType();
            PropertyInfo property = type.GetProperty(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property != null)
                return property.GetValue(instance, null);

            FieldInfo field = type.GetField(
                memberName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? default : field.GetValue(instance);
        }
    }
}
#endif
