#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// C# 语法高亮器
    /// </summary>
    public static class CSharpSyntaxHighlighter
    {
        // 颜色定义
        private static readonly Color KeywordColor = new(0.34f, 0.61f, 0.84f);      // 蓝色 - 关键字
        private static readonly Color TypeColor = new(0.31f, 0.78f, 0.78f);         // 青色 - 类型
        private static readonly Color StringColor = new(0.84f, 0.62f, 0.46f);       // 橙色 - 字符串
        private static readonly Color CommentColor = new(0.45f, 0.60f, 0.45f);      // 绿色 - 注释
        private static readonly Color NumberColor = new(0.71f, 0.82f, 0.53f);       // 浅绿 - 数字
        private static readonly Color MethodColor = new(0.86f, 0.86f, 0.67f);       // 黄色 - 方法
        private static readonly Color DefaultColor = new(0.85f, 0.85f, 0.85f);      // 白色 - 默认
        
        private static readonly HashSet<string> Keywords = new()
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
            "false", "finally", "fixed", "float", "for", "foreach", "goto", "if",
            "implicit", "in", "int", "interface", "internal", "is", "lock", "long",
            "namespace", "new", "null", "object", "operator", "out", "override",
            "params", "private", "protected", "public", "readonly", "ref", "return",
            "sbyte", "sealed", "short", "sizeof", "stackalloc", "static", "string",
            "struct", "switch", "this", "throw", "true", "try", "typeof", "uint",
            "ulong", "unchecked", "unsafe", "ushort", "using", "var", "virtual",
            "void", "volatile", "while", "async", "await", "partial", "get", "set",
            "where", "yield"
        };
        
        private static readonly HashSet<string> Types = new()
        {
            "Action", "Func", "Task", "List", "Dictionary", "HashSet", "Queue",
            "Stack", "Array", "String", "Int32", "Int64", "Single", "Double",
            "Boolean", "Object", "Type", "Enum", "Attribute", "Exception",
            "EventHandler", "IEnumerable", "IEnumerator", "IDisposable",
            "GameObject", "Transform", "MonoBehaviour", "ScriptableObject",
            "Component", "Vector2", "Vector3", "Vector4", "Quaternion", "Color",
            "Rect", "Bounds", "Debug", "Input", "Time", "Mathf", "Physics",
            "RectTransform", "Canvas", "Image", "Text", "Button", "Sprite",
            "AudioSource", "AudioClip", "Rigidbody", "Collider", "Animator",
            "FSM", "HierarchicalSM", "IState", "AbstractState",
            "EventKit", "UIKit", "UIPanel", "UIElement", "UIComponent", "UITool",
            "ActionKit", "AudioKit", "SaveKit", "PoolKit", "SingletonKit"
        };
        
        /// <summary>
        /// 将代码转换为带颜色标签的富文本
        /// </summary>
        /// <param name="code">源代码</param>
        /// <param name="fontSize">字体大小，用于 size 标签确保行高一致</param>
        public static string Highlight(string code, int fontSize = 0)
        {
            if (string.IsNullOrEmpty(code)) return code;
            
            var result = new StringBuilder(code.Length * 2);
            var lines = code.Split('\n');
            
            // 是否使用 size 标签包裹每行
            bool useSize = fontSize > 0;
            string sizeOpen = useSize ? $"<size={fontSize}>" : "";
            string sizeClose = useSize ? "</size>" : "";
            
            for (int lineIdx = 0; lineIdx < lines.Length; lineIdx++)
            {
                var line = lines[lineIdx];
                if (lineIdx > 0) result.Append('\n');
                
                // 空行直接保留
                if (string.IsNullOrEmpty(line) || string.IsNullOrWhiteSpace(line))
                {
                    if (useSize)
                    {
                        // 空行也需要 size 标签保持行高一致
                        result.Append(sizeOpen);
                        result.Append(line);
                        result.Append(sizeClose);
                    }
                    else
                    {
                        result.Append(line);
                    }
                    continue;
                }
                
                // 检查是否是注释行
                var trimmed = line.TrimStart();
                if (trimmed.StartsWith("//"))
                {
                    var leadingSpaces = line.Length - line.TrimStart().Length;
                    result.Append(sizeOpen);
                    result.Append(new string(' ', leadingSpaces));
                    result.Append(ColorTag(line.TrimStart(), CommentColor));
                    result.Append(sizeClose);
                    continue;
                }
                
                // 对于非注释行，用 size 标签包裹整行
                result.Append(sizeOpen);
                HighlightLine(line, result);
                result.Append(sizeClose);
            }
            
            return result.ToString();
        }
        
        private static void HighlightLine(string line, StringBuilder result)
        {
            int i = 0;
            while (i < line.Length)
            {
                // 跳过空白
                if (char.IsWhiteSpace(line[i]))
                {
                    result.Append(line[i]);
                    i++;
                    continue;
                }
                
                // 字符串
                if (line[i] == '"')
                {
                    int start = i;
                    i++;
                    while (i < line.Length && (line[i] != '"' || line[i - 1] == '\\'))
                        i++;
                    if (i < line.Length) i++;
                    result.Append(ColorTag(line.Substring(start, i - start), StringColor));
                    continue;
                }
                
                // 字符
                if (line[i] == '\'')
                {
                    int start = i;
                    i++;
                    while (i < line.Length && line[i] != '\'')
                        i++;
                    if (i < line.Length) i++;
                    result.Append(ColorTag(line.Substring(start, i - start), StringColor));
                    continue;
                }
                
                // 行内注释
                if (i + 1 < line.Length && line[i] == '/' && line[i + 1] == '/')
                {
                    result.Append(ColorTag(line.Substring(i), CommentColor));
                    break;
                }
                
                // 数字
                if (char.IsDigit(line[i]) || (line[i] == '.' && i + 1 < line.Length && char.IsDigit(line[i + 1])))
                {
                    int start = i;
                    while (i < line.Length && (char.IsDigit(line[i]) || line[i] == '.' || line[i] == 'f' || line[i] == 'd'))
                        i++;
                    result.Append(ColorTag(line.Substring(start, i - start), NumberColor));
                    continue;
                }
                
                // 标识符
                if (char.IsLetter(line[i]) || line[i] == '_')
                {
                    int start = i;
                    while (i < line.Length && (char.IsLetterOrDigit(line[i]) || line[i] == '_'))
                        i++;
                    var word = line.Substring(start, i - start);
                    
                    // 检查是否是方法调用
                    bool isMethod = i < line.Length && line[i] == '(';
                    
                    if (Keywords.Contains(word))
                        result.Append(ColorTag(word, KeywordColor));
                    else if (Types.Contains(word))
                        result.Append(ColorTag(word, TypeColor));
                    else if (isMethod)
                        result.Append(ColorTag(word, MethodColor));
                    else
                        result.Append(EscapeRichText(word)); // 默认颜色不添加标签
                    continue;
                }
                
                // 其他字符 - 不添加颜色标签
                result.Append(EscapeRichText(line[i].ToString()));
                i++;
            }
        }
        
        private static string ColorTag(string text, Color color)
        {
            return $"<color=#{ColorUtility.ToHtmlStringRGB(color)}>{EscapeRichText(text)}</color>";
        }
        
        private static string EscapeRichText(string text)
        {
            return text.Replace("<", "＜").Replace(">", "＞");
        }
    }
}
#endif
