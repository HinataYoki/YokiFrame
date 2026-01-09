#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YokiFrame 编辑器工具类 - 提供路径查找等通用功能
    /// </summary>
    public static class YokiFrameEditorUtility
    {
        private static string sCachedRootPath;

        #region C# 标识符验证

        /// <summary>
        /// C# 标识符正则表达式
        /// 规则：以字母或下划线开头，后跟字母、数字或下划线
        /// </summary>
        private static readonly Regex sIdentifierRegex = new(
            @"^[a-zA-Z_][a-zA-Z0-9_]*$",
            RegexOptions.Compiled);

        /// <summary>
        /// C# 关键字集合
        /// </summary>
        private static readonly HashSet<string> sCSharpKeywords = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal", "is",
            "lock", "long", "namespace", "new", "null", "object", "operator",
            "out", "override", "params", "private", "protected", "public",
            "readonly", "ref", "return", "sbyte", "sealed", "short", "sizeof",
            "stackalloc", "static", "string", "struct", "switch", "this", "throw",
            "true", "try", "typeof", "uint", "ulong", "unchecked", "unsafe",
            "ushort", "using", "virtual", "void", "volatile", "while",
            // 上下文关键字
            "add", "alias", "ascending", "async", "await", "by", "descending",
            "dynamic", "equals", "from", "get", "global", "group", "into",
            "join", "let", "nameof", "on", "orderby", "partial", "remove",
            "select", "set", "value", "var", "when", "where", "yield"
        };

        /// <summary>
        /// 检查名称是否为有效的 C# 标识符
        /// </summary>
        /// <param name="name">要检查的名称</param>
        /// <returns>是否有效</returns>
        public static bool IsValidCSharpIdentifier(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return sIdentifierRegex.IsMatch(name);
        }

        /// <summary>
        /// 检查名称是否为 C# 关键字
        /// </summary>
        /// <param name="name">要检查的名称</param>
        /// <returns>是否为关键字</returns>
        public static bool IsCSharpKeyword(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;

            return sCSharpKeywords.Contains(name);
        }

        /// <summary>
        /// 验证 C# 标识符并返回错误信息
        /// </summary>
        /// <param name="name">要验证的名称</param>
        /// <param name="errorMessage">错误信息（如果无效）</param>
        /// <param name="suggestion">修复建议（如果无效）</param>
        /// <returns>是否有效</returns>
        public static bool ValidateCSharpIdentifier(string name, out string errorMessage, out string suggestion)
        {
            errorMessage = null;
            suggestion = null;

            // 空名称检查
            if (string.IsNullOrEmpty(name))
            {
                errorMessage = "名称不能为空";
                suggestion = "请输入有效的名称";
                return false;
            }

            // C# 关键字检查
            if (IsCSharpKeyword(name))
            {
                errorMessage = $"'{name}' 是 C# 关键字，不能作为标识符";
                suggestion = $"建议使用 '@{name}' 或修改名称";
                return false;
            }

            // 标识符格式检查
            if (!IsValidCSharpIdentifier(name))
            {
                errorMessage = $"'{name}' 不是有效的 C# 标识符";
                suggestion = GetIdentifierSuggestion(name);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 获取标识符修复建议
        /// </summary>
        private static string GetIdentifierSuggestion(string name)
        {
            if (string.IsNullOrEmpty(name))
                return "请输入有效的名称";

            char firstChar = name[0];

            // 以数字开头
            if (char.IsDigit(firstChar))
                return $"标识符不能以数字开头，建议改为 '_{name}' 或 'Item{name}'";

            // 包含非法字符
            return "标识符只能包含字母、数字和下划线，且不能以数字开头";
        }

        #endregion

        #region 路径工具
        
        /// <summary>
        /// 获取 YokiFrame 根目录路径（相对于 Assets）
        /// </summary>
        public static string GetYokiFrameRootPath()
        {
            if (!string.IsNullOrEmpty(sCachedRootPath) && Directory.Exists(sCachedRootPath))
                return sCachedRootPath;
            
            // 通过查找标志性文件来定位 YokiFrame 目录
            var guids = AssetDatabase.FindAssets("YokiFrameToolStyles t:StyleSheet");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                // path = "Assets/.../YokiFrame/Core/Editor/Styles/YokiFrameToolStyles.uss"
                // 需要回退到 YokiFrame 目录
                var dir = Path.GetDirectoryName(path); // Styles
                dir = Path.GetDirectoryName(dir);       // Editor
                dir = Path.GetDirectoryName(dir);       // Core
                dir = Path.GetDirectoryName(dir);       // YokiFrame
                sCachedRootPath = dir?.Replace('\\', '/');
                return sCachedRootPath;
            }
            
            // 回退：使用默认路径
            sCachedRootPath = "Assets/YokiFrame";
            return sCachedRootPath;
        }
        
        /// <summary>
        /// 获取主样式表路径
        /// </summary>
        public static string GetMainStyleSheetPath()
        {
            return $"{GetYokiFrameRootPath()}/Core/Editor/Styles/YokiFrameToolStyles.uss";
        }
        
        /// <summary>
        /// 加载主样式表
        /// </summary>
        public static StyleSheet LoadMainStyleSheet()
        {
            var path = GetMainStyleSheetPath();
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            
            if (styleSheet == null)
            {
                Debug.LogWarning($"[YokiFrame] 无法加载样式表: {path}");
            }
            
            return styleSheet;
        }
        
        /// <summary>
        /// 为 VisualElement 应用主样式表
        /// </summary>
        public static void ApplyMainStyleSheet(VisualElement root)
        {
            var styleSheet = LoadMainStyleSheet();
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
        }
        
        /// <summary>
        /// 通过文件名查找并加载样式表
        /// </summary>
        /// <param name="ussFileName">USS 文件名（不含路径，如 "BindInspectorStyles"）</param>
        public static StyleSheet LoadStyleSheetByName(string ussFileName)
        {
            var guids = AssetDatabase.FindAssets($"{ussFileName} t:StyleSheet");
            if (guids.Length > 0)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                return AssetDatabase.LoadAssetAtPath<StyleSheet>(path);
            }
            
            Debug.LogWarning($"[YokiFrame] 无法找到样式表: {ussFileName}");
            return null;
        }
        
        /// <summary>
        /// 清除缓存（用于路径变更后刷新）
        /// </summary>
        public static void ClearCache()
        {
            sCachedRootPath = null;
        }

        #endregion
    }
}
#endif
