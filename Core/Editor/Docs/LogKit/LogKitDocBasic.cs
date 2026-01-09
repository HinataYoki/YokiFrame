#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LogKit 基本使用文档
    /// </summary>
    internal static class LogKitDocBasic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "基本使用",
                Description = "提供 Log、Warning、Error、Exception 四个级别的日志输出。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "输出日志",
                        Code = @"// 普通日志
KitLogger.Log(""游戏启动"");
KitLogger.Log($""玩家等级: {level}"");

// 警告
KitLogger.Warning(""配置文件缺失，使用默认值"");

// 错误
KitLogger.Error(""网络连接失败"");

// 异常
try
{
    // ...
}
catch (Exception ex)
{
    KitLogger.Exception(ex);
}"
                    }
                }
            };
        }
    }
}
#endif
