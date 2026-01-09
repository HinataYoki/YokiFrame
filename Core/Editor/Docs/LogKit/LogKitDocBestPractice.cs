#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LogKit 最佳实践文档
    /// </summary>
    internal static class LogKitDocBestPractice
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "最佳实践",
                Description = "推荐的 KitLogger 使用方式。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "初始化示例",
                        Code = @"public class GameLauncher : MonoBehaviour
{
    void Awake()
    {
        // 配置日志系统
        KitLogger.Level = KitLogger.LogLevel.All;
        KitLogger.EnableEncryption = true;
        
        // 仅在开发/测试版本启用 IMGUI
        #if DEVELOPMENT_BUILD || UNITY_EDITOR
        KitLogger.EnableIMGUI(300);
        #endif
        
        KitLogger.Log(""游戏启动"");
    }
}

// 使用条件编译控制日志级别
#if UNITY_EDITOR
    KitLogger.Level = KitLogger.LogLevel.All;
#elif DEVELOPMENT_BUILD
    KitLogger.Level = KitLogger.LogLevel.Warning;
#else
    KitLogger.Level = KitLogger.LogLevel.Error;
#endif"
                    }
                }
            };
        }
    }
}
#endif
