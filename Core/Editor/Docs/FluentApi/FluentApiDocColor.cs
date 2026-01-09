#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FluentApi Color 扩展文档
    /// </summary>
    internal static class FluentApiDocColor
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Color 扩展",
                Description = "颜色操作扩展方法，支持分量修改、透明度、混合、转换等。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "分量修改",
                        Code = @"// 修改单个颜色分量
var color = Color.red
    .R(0.5f)   // 修改红色分量
    .G(0.3f)   // 修改绿色分量
    .B(0.8f)   // 修改蓝色分量
    .A(0.9f);  // 修改透明度

// 快捷透明度设置
var semiTransparent = Color.white.WithAlpha(0.5f);
var opaque = color.Opaque();       // 完全不透明
var transparent = color.Transparent(); // 完全透明",
                        Explanation = "所有方法返回新的 Color，不修改原值，支持链式调用。"
                    },
                    new()
                    {
                        Title = "颜色变换",
                        Code = @"// 反转颜色（RGB 取反，Alpha 不变）
var inverted = Color.red.Invert(); // 变为青色

// 转灰度
var gray = color.Grayscale();

// 线性插值
var lerped = Color.red.LerpTo(Color.blue, 0.5f);

// 颜色混合
var blended = color1.Blend(color2, 0.3f); // 30% color2",
                        Explanation = "颜色变换方法用于实现常见的视觉效果。"
                    },
                    new()
                    {
                        Title = "格式转换",
                        Code = @"// Color 转 Color32
Color32 color32 = color.ToColor32();

// 转十六进制字符串
string hex = color.ToHex();           // ""FF0000""
string hexAlpha = color.ToHex(true);  // ""FF0000FF""

// 从十六进制解析（静态方法）
var parsed = UnityColorExtension.FromHex(""#FF5500"");
var parsed2 = UnityColorExtension.FromHex(""FF5500FF"");",
                        Explanation = "支持带或不带 # 前缀的十六进制字符串，以及可选的 Alpha 通道。"
                    }
                }
            };
        }
    }
}
#endif
