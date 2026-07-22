#if UNITY_5_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame.Unity.Inspector
{
    /// <summary>
    /// InspectorKit 的 Unity 属性基类。
    /// 该类型只保存编辑器元数据，不包含运行时行为。
    /// </summary>
    public abstract class InspectorMetadataAttribute : PropertyAttribute
    {
    }

    /// <summary>
    /// 为字段声明 Inspector 分组标题。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class InspectorSectionAttribute : InspectorMetadataAttribute
    {
        /// <summary>
        /// 获取分组标题。
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// 创建 Inspector 分组标题元数据。
        /// </summary>
        /// <param name="title">显示在字段前方的标题。</param>
        public InspectorSectionAttribute(string title)
        {
            Title = title ?? string.Empty;
        }
    }

    /// <summary>
    /// 为字段声明信息提示卡片。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class InspectorInfoBoxAttribute : InspectorMetadataAttribute
    {
        /// <summary>
        /// 获取提示消息。
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// 获取提示级别。
        /// </summary>
        public InspectorInfoBoxType Type { get; }

        /// <summary>
        /// 创建 Inspector 信息提示元数据。
        /// </summary>
        /// <param name="message">显示给用户的提示内容。</param>
        /// <param name="type">提示级别。</param>
        public InspectorInfoBoxAttribute(string message, InspectorInfoBoxType type = InspectorInfoBoxType.Info)
        {
            Message = message ?? string.Empty;
            Type = type;
        }
    }

    /// <summary>
    /// Inspector 信息提示级别。
    /// </summary>
    public enum InspectorInfoBoxType
    {
        /// <summary>普通说明。</summary>
        Info,
        /// <summary>成功状态。</summary>
        Success,
        /// <summary>警告状态。</summary>
        Warning,
        /// <summary>错误状态。</summary>
        Error
    }

    /// <summary>
    /// 将字段显示为只读状态。
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
    public sealed class InspectorReadOnlyAttribute : InspectorMetadataAttribute
    {
    }

    /// <summary>
    /// 为 Inspector 声明一个可执行按钮。
    /// 方法必须是实例方法，且只能使用无参数或可选参数签名。
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class InspectorButtonAttribute : Attribute
    {
        /// <summary>
        /// 获取按钮标题。
        /// </summary>
        public string Label { get; }

        /// <summary>
        /// 创建 Inspector 按钮元数据。
        /// </summary>
        /// <param name="label">按钮显示文本。</param>
        public InspectorButtonAttribute(string label)
        {
            Label = label ?? string.Empty;
        }
    }
}
#endif
