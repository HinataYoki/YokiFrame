#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>集中生成多 Member 的确定性字段名。</summary>
    internal static class UIKitBindMemberNaming
    {
        private static readonly HashSet<string> sCSharpKeywords = new(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal",
            "is", "lock", "long", "namespace", "new", "null", "object",
            "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while"
        };

        /// <summary>为指定目标生成首项兼容、后续项带组件类型的字段名。</summary>
        internal static string CreateDefaultName(
            AbstractBind bind,
            Component target,
            IReadOnlyList<BindMemberTarget> targets,
            int index)
        {
            if (index == 0 && !string.IsNullOrWhiteSpace(bind.Name))
                return bind.Name.Trim();

            string nodeName = ToPascalIdentifier(bind.gameObject.name);
            if (index == 0 || target == default)
                return nodeName;

            Type targetType = target.GetType();
            int typeIndex = CountPreviousType(targets, index, targetType) + 1;
            string suffix = typeIndex > 1
                ? typeIndex.ToString(CultureInfo.InvariantCulture)
                : string.Empty;
            return nodeName + ToPascalIdentifier(targetType.Name) + suffix;
        }

        /// <summary>把任意节点或类型文本转换为可用的 PascalCase C# 标识符。</summary>
        internal static string ToPascalIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Item";
            StringBuilder builder = new(value.Length);
            bool upperNext = true;
            for (var index = 0; index < value.Length; index++)
            {
                char character = value[index];
                if (char.IsLetterOrDigit(character) || character == '_')
                {
                    builder.Append(upperNext ? char.ToUpperInvariant(character) : character);
                    upperNext = false;
                }
                else
                {
                    upperNext = true;
                }
            }

            if (builder.Length == 0)
                builder.Append("Item");
            if (char.IsDigit(builder[0]) || sCSharpKeywords.Contains(builder.ToString()))
                builder.Insert(0, '_');
            return builder.ToString();
        }

        /// <summary>统计当前项之前相同组件类型的目标数量。</summary>
        private static int CountPreviousType(
            IReadOnlyList<BindMemberTarget> targets,
            int index,
            Type targetType)
        {
            int count = 0;
            for (var targetIndex = 0; targetIndex < index; targetIndex++)
            {
                BindMemberTarget item = targets[targetIndex];
                if (item != null && item.Target != default && item.Target.GetType() == targetType)
                    count++;
            }
            return count;
        }
    }
}
#endif
