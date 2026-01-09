#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LogKit 日志配置文档
    /// </summary>
    internal static class LogKitDocConfig
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "日志配置",
                Description = "配置日志级别、文件写入、加密等选项。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "配置选项",
                        Code = @"// 设置日志级别
KitLogger.Level = KitLogger.LogLevel.All;     // 输出所有日志
KitLogger.Level = KitLogger.LogLevel.Warning; // 只输出 Warning 和 Error
KitLogger.Level = KitLogger.LogLevel.Error;   // 只输出 Error
KitLogger.Level = KitLogger.LogLevel.None;    // 关闭所有日志

// 启用文件写入（自动异步写入）
KitLogger.AutoEnableWriteLogToFile = true;

// 启用加密（保护敏感信息）
KitLogger.EnableEncryption = true;

// 编辑器中保存日志
KitLogger.SaveLogInEditor = true;

// 配置限制
KitLogger.MaxQueueSize = 20000;      // 最大队列大小
KitLogger.MaxSameLogCount = 50;      // 相同日志最大重复次数
KitLogger.MaxRetentionDays = 10;     // 日志保留天数
KitLogger.MaxFileBytes = 50 * 1024 * 1024; // 单文件最大 50MB"
                    }
                }
            };
        }
    }
}
#endif
