#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SaveKit 文件格式配置文档
    /// </summary>
    internal static class SaveKitDocFileFormat
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "文件格式配置",
                Description = "单文件格式 .yoki，支持自定义前缀和后缀。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "配置文件格式",
                        Code = @"// 设置文件格式（默认: save_0.yoki）
SaveKit.SetFileFormat(
    prefix: ""save_"",    // 文件名前缀
    extension: "".yoki""  // 文件后缀
);

// 获取当前配置
var (prefix, ext) = SaveKit.GetFileFormat();

// 设置存档路径
SaveKit.SetSavePath(Path.Combine(
    Application.persistentDataPath, ""MySaves""));

// 设置最大槽位数
SaveKit.SetMaxSlots(20);",
                        Explanation = "文件格式: [魔数YOKI][版本][槽位ID][创建时间][保存时间][名称长度][名称][加密数据]"
                    }
                }
            };
        }
    }
}
#endif
