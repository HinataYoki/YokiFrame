#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FluentApi String 扩展文档
    /// </summary>
    internal static class FluentApiDocString
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "String 扩展",
                Description = "字符串处理扩展方法。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "字符串操作",
                        Code = @"// 空值判断
if (str.IsNullOrEmpty()) return;
if (str.IsNotNullOrWhiteSpace()) Process(str);

// StringBuilder 链式
var result = ""Hello""
    .Builder()
    .AddSuffix("" World"")
    .AddPrefix(""Say: "")
    .ToString();

// 格式化
var msg = ""{0} has {1} HP"".Format(name, hp);

// 首字母大小写
var upper = ""hello"".UpperFirst(); // ""Hello""
var lower = ""Hello"".LowerFirst(); // ""hello""

// 安全截取
var sub = str.SafeSubstring(0, 10);

// 移除前后缀
var name = ""PlayerController"".RemoveSuffix(""Controller""); // ""Player""
var path = ""/root/file"".RemovePrefix(""/root/""); // ""file"""
                    }
                }
            };
        }
    }
}
#endif
