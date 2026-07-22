#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Globalization;
using System.Text;

namespace YokiFrame
{
    /// <summary>为 EventKit Workbench 诊断提供一致的完整类型身份文本。</summary>
    internal static class EventKitTypeIdentity
    {
        /// <summary>格式化普通、数组、嵌套和闭合泛型类型的稳定身份。</summary>
        /// <param name="type">需要格式化的 Runtime 类型。</param>
        /// <returns>不含程序集限定名的稳定完整类型名。</returns>
        internal static string Format(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.IsArray)
            {
                return Format(type.GetElementType()) + "[]";
            }

            var builder = new StringBuilder(64);
            AppendType(builder, type);
            return builder.ToString();
        }

        /// <summary>格式化枚举类型和值构成的稳定事件键，并保留无定义值的底层数字。</summary>
        /// <param name="key">Runtime EnumEvent 使用的类型和值键。</param>
        /// <returns>形如 Namespace.EnumName.ValueName 的稳定事件键。</returns>
        internal static string FormatEnumEventKey(EnumEventKey key)
        {
            if (key.EnumType == null)
            {
                return key.EnumValue.ToString(CultureInfo.InvariantCulture);
            }

            string typeName = Format(key.EnumType);
            try
            {
                return typeName + "." + Enum.ToObject(key.EnumType, key.EnumValue);
            }
            catch (ArgumentException)
            {
                return typeName + "." + key.EnumValue.ToString(CultureInfo.InvariantCulture);
            }
        }

        /// <summary>递归写入命名空间、嵌套类型段和当前段泛型实参。</summary>
        /// <param name="builder">复用的目标字符串构建器。</param>
        /// <param name="type">当前类型段。</param>
        private static void AppendType(StringBuilder builder, Type type)
        {
            Type declaringType = type.DeclaringType;
            if (declaringType != null)
            {
                AppendType(builder, declaringType);
                builder.Append('+');
            }
            else if (!string.IsNullOrEmpty(type.Namespace))
            {
                builder.Append(type.Namespace);
                builder.Append('.');
            }

            AppendNameWithoutArity(builder, type.Name);
            Type[] arguments = GetOwnGenericArguments(type, declaringType);
            if (arguments.Length == 0)
            {
                return;
            }

            builder.Append('<');
            for (var index = 0; index < arguments.Length; index++)
            {
                if (index > 0)
                {
                    builder.Append(',');
                }

                AppendType(builder, arguments[index]);
            }

            builder.Append('>');
        }

        /// <summary>返回当前嵌套类型段自己声明的泛型实参。</summary>
        /// <param name="type">当前类型。</param>
        /// <param name="declaringType">外层类型；顶层类型为空。</param>
        /// <returns>当前段自己的泛型实参数组。</returns>
        private static Type[] GetOwnGenericArguments(Type type, Type declaringType)
        {
            if (!type.IsGenericType)
            {
                return Type.EmptyTypes;
            }

            Type[] allArguments = type.GetGenericArguments();
            int inheritedCount = declaringType != null && declaringType.IsGenericType
                ? declaringType.GetGenericArguments().Length
                : 0;
            int ownCount = allArguments.Length - inheritedCount;
            if (ownCount <= 0)
            {
                return Type.EmptyTypes;
            }

            var ownArguments = new Type[ownCount];
            Array.Copy(allArguments, inheritedCount, ownArguments, 0, ownCount);
            return ownArguments;
        }

        /// <summary>移除 Runtime 泛型名称中的反引号 arity 后缀。</summary>
        /// <param name="builder">目标字符串构建器。</param>
        /// <param name="name">Runtime 类型短名。</param>
        private static void AppendNameWithoutArity(StringBuilder builder, string name)
        {
            int arityIndex = name.IndexOf('`');
            builder.Append(arityIndex < 0 ? name : name.Substring(0, arityIndex));
        }
    }
}
#endif
