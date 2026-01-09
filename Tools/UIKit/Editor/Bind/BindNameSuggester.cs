using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 命名建议器 - 根据组件类型和 GameObject 名称生成规范字段名
    /// </summary>
    public static class BindNameSuggester
    {
        #region 常量

        /// <summary>
        /// 用于移除非法字符的正则表达式
        /// </summary>
        private static readonly Regex sInvalidCharsRegex = new(
            @"[^a-zA-Z0-9_]",
            RegexOptions.Compiled);

        /// <summary>
        /// 用于检测连续下划线的正则表达式
        /// </summary>
        private static readonly Regex sMultipleUnderscoresRegex = new(
            @"_{2,}",
            RegexOptions.Compiled);

        /// <summary>
        /// 用于检测数字开头的正则表达式
        /// </summary>
        private static readonly Regex sStartsWithDigitRegex = new(
            @"^\d",
            RegexOptions.Compiled);

        /// <summary>
        /// StringBuilder 复用，避免频繁分配
        /// </summary>
        private static readonly StringBuilder sStringBuilder = new(64);

        #endregion

        #region 公共方法

        /// <summary>
        /// 根据 GameObject 和绑定类型生成建议名称
        /// </summary>
        /// <param name="gameObject">目标 GameObject</param>
        /// <param name="bindType">绑定类型</param>
        /// <param name="componentType">组件类型（可选，用于获取前缀）</param>
        /// <returns>建议的字段名</returns>
        public static string SuggestName(GameObject gameObject, BindType bindType, Type componentType = null)
        {
            if (gameObject == null)
                return string.Empty;

            string baseName = gameObject.name;
            string prefix = string.Empty;

            // 获取组件类型前缀
            if (componentType != null)
            {
                prefix = GetTypePrefix(componentType);
            }

            // 清理名称
            string cleanName = CleanName(baseName);

            // 如果清理后为空，使用默认名称
            if (string.IsNullOrEmpty(cleanName))
            {
                cleanName = "Item";
            }

            // 组合前缀和名称
            return CombinePrefixAndName(prefix, cleanName);
        }

        /// <summary>
        /// 根据 GameObject 名称和组件类型名称生成建议名称
        /// </summary>
        /// <param name="gameObjectName">GameObject 名称</param>
        /// <param name="componentTypeName">组件完整类型名</param>
        /// <returns>建议的字段名</returns>
        public static string SuggestName(string gameObjectName, string componentTypeName)
        {
            if (string.IsNullOrEmpty(gameObjectName))
                return string.Empty;

            string prefix = string.Empty;

            // 获取组件类型前缀
            if (!string.IsNullOrEmpty(componentTypeName))
            {
                prefix = UIKitBindConfig.Instance.GetPrefix(componentTypeName);
            }

            // 清理名称
            string cleanName = CleanName(gameObjectName);

            // 如果清理后为空，使用默认名称
            if (string.IsNullOrEmpty(cleanName))
            {
                cleanName = "Item";
            }

            // 组合前缀和名称
            return CombinePrefixAndName(prefix, cleanName);
        }

        /// <summary>
        /// 获取组件类型的前缀
        /// </summary>
        /// <param name="componentType">组件类型</param>
        /// <returns>前缀，如果未找到则返回空字符串</returns>
        public static string GetTypePrefix(Type componentType)
        {
            if (componentType == null)
                return string.Empty;

            return UIKitBindConfig.Instance.GetPrefix(componentType);
        }

        /// <summary>
        /// 获取组件类型的前缀（通过类型名）
        /// </summary>
        /// <param name="componentTypeName">组件完整类型名</param>
        /// <returns>前缀，如果未找到则返回空字符串</returns>
        public static string GetTypePrefix(string componentTypeName)
        {
            if (string.IsNullOrEmpty(componentTypeName))
                return string.Empty;

            return UIKitBindConfig.Instance.GetPrefix(componentTypeName);
        }

        /// <summary>
        /// 清理名称，移除非法字符并转换为有效标识符
        /// </summary>
        /// <param name="name">原始名称</param>
        /// <returns>清理后的名称</returns>
        public static string CleanName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            // 使用复用的 StringBuilder
            sStringBuilder.Clear();

            // 遍历每个字符
            foreach (char c in name)
            {
                // 只接受 ASCII 字母、数字
                if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                {
                    sStringBuilder.Append(c);
                }
                else if (c == '_' || c == ' ' || c == '-')
                {
                    // 下划线、空格和连字符转换为下划线，但避免连续下划线
                    if (sStringBuilder.Length > 0 && sStringBuilder[sStringBuilder.Length - 1] != '_')
                    {
                        sStringBuilder.Append('_');
                    }
                }
                // 其他字符（包括中文、特殊符号）跳过
            }

            // 移除末尾的下划线
            while (sStringBuilder.Length > 0 && sStringBuilder[sStringBuilder.Length - 1] == '_')
            {
                sStringBuilder.Length--;
            }

            string result = sStringBuilder.ToString();

            // 如果以数字开头，添加下划线前缀
            if (result.Length > 0 && char.IsDigit(result[0]))
            {
                result = "_" + result;
            }

            return result;
        }

        /// <summary>
        /// 组合前缀和名称
        /// </summary>
        /// <param name="prefix">前缀</param>
        /// <param name="name">名称</param>
        /// <returns>组合后的字段名</returns>
        public static string CombinePrefixAndName(string prefix, string name)
        {
            if (string.IsNullOrEmpty(name))
                return prefix ?? string.Empty;

            if (string.IsNullOrEmpty(prefix))
                return EnsurePascalCase(name);

            // 检查名称是否已经包含前缀（不区分大小写）
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return EnsurePascalCase(name);
            }

            // 组合前缀和名称，确保 PascalCase
            return prefix + EnsurePascalCase(name);
        }

        /// <summary>
        /// 确保名称为 PascalCase
        /// </summary>
        /// <param name="name">原始名称</param>
        /// <returns>PascalCase 格式的名称</returns>
        public static string EnsurePascalCase(string name)
        {
            if (string.IsNullOrEmpty(name))
                return string.Empty;

            // 如果第一个字符是小写字母，转换为大写
            if (char.IsLower(name[0]))
            {
                return char.ToUpper(name[0]) + name.Substring(1);
            }

            return name;
        }

        /// <summary>
        /// 检查名称是否需要建议（为空或无效）
        /// </summary>
        /// <param name="name">当前名称</param>
        /// <returns>是否需要建议</returns>
        public static bool NeedsSuggestion(string name)
        {
            return string.IsNullOrEmpty(name) || !BindValidator.IsValidIdentifier(name);
        }

        #endregion

        #region 批量建议

        /// <summary>
        /// 为 GameObject 上的所有可绑定组件生成建议名称
        /// </summary>
        /// <param name="gameObject">目标 GameObject</param>
        /// <returns>组件类型到建议名称的映射</returns>
        public static System.Collections.Generic.Dictionary<Type, string> SuggestNamesForComponents(GameObject gameObject)
        {
            var result = new System.Collections.Generic.Dictionary<Type, string>(8);

            if (gameObject == null)
                return result;

            // 获取常用 UI 组件
            var components = gameObject.GetComponents<Component>();
            foreach (var component in components)
            {
                if (component == null)
                    continue;

                var type = component.GetType();

                // 跳过 Transform 和 RectTransform（通常不需要绑定）
                if (type == typeof(Transform))
                    continue;

                // 检查是否有前缀映射
                string prefix = GetTypePrefix(type);
                if (!string.IsNullOrEmpty(prefix))
                {
                    string suggestion = SuggestName(gameObject, BindType.Member, type);
                    result[type] = suggestion;
                }
            }

            return result;
        }

        #endregion
    }
}
