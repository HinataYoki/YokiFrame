using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 代码扫描器 - 静态分析项目中的事件注册和发送
    /// </summary>
    public static class EventCodeScanner
    {
        /// <summary>
        /// 扫描结果
        /// </summary>
        public struct ScanResult
        {
            public string FilePath;
            public int LineNumber;
            public string LineContent;
            public string EventType;      // Enum/Type/String
            public string CallType;       // Register/Send/UnRegister
            public string EventKey;       // 事件键（枚举值、类型名、字符串）
            public string ParamType;      // 参数类型（用于区分不同通道）
        }

        // 正则表达式模式 - 捕获泛型参数
        // Group 1: 完整泛型参数 (可选)
        // Group 2: 事件键
        private static readonly Regex EnumRegisterPattern = new(
            @"EventKit\.Enum\.Register\s*(?:<\s*([^>]+)\s*>)?\s*\(\s*(\w+\.\w+)",
            RegexOptions.Compiled);
        
        private static readonly Regex EnumSendPattern = new(
            @"EventKit\.Enum\.Send\s*(?:<\s*([^>]+)\s*>)?\s*\(\s*(\w+\.\w+)",
            RegexOptions.Compiled);
        
        private static readonly Regex EnumUnRegisterPattern = new(
            @"EventKit\.Enum\.UnRegister\s*(?:<\s*([^>]+)\s*>)?\s*\(\s*(\w+\.\w+)",
            RegexOptions.Compiled);
        
        // Type 事件 - 支持多种调用模式
        // 模式1: EventKit.Type.Send<DamageEvent>(...)
        // 模式2: EventKit.Type.Send(new DamageEvent(...)) 或 EventKit.Type.Send(new DamageEvent { ... })
        private static readonly Regex TypeRegisterPattern = new(
            @"EventKit\.Type\.Register\s*<\s*(\w+)\s*>",
            RegexOptions.Compiled);
        
        private static readonly Regex TypeSendGenericPattern = new(
            @"EventKit\.Type\.Send\s*<\s*(\w+)\s*>",
            RegexOptions.Compiled);
        
        // 匹配 Send(new TypeName 模式（支持跨行初始化器）
        private static readonly Regex TypeSendNewPattern = new(
            @"EventKit\.Type\.Send\s*\(\s*new\s+(\w+)",
            RegexOptions.Compiled);
        
        // 匹配 Send(变量) 模式 - 需要从上下文推断类型，暂不支持
        
        private static readonly Regex TypeUnRegisterPattern = new(
            @"EventKit\.Type\.UnRegister\s*<\s*(\w+)\s*>",
            RegexOptions.Compiled);
        
        // String 事件也需要捕获参数类型
        private static readonly Regex StringRegisterPattern = new(
            @"EventKit\.String\.Register\s*(?:<\s*([^>]+)\s*>)?\s*\(\s*""([^""]+)""",
            RegexOptions.Compiled);
        
        private static readonly Regex StringSendPattern = new(
            @"EventKit\.String\.Send\s*(?:<\s*([^>]+)\s*>)?\s*\(\s*""([^""]+)""",
            RegexOptions.Compiled);

        private static readonly List<ScanResult> CachedResults = new();
        private static string mLastScanFolder;
        private static DateTime mLastScanTime;

        /// <summary>
        /// 扫描指定文件夹下的所有 C# 文件
        /// </summary>
        /// <param name="folderPath">扫描目录</param>
        /// <param name="forceRescan">强制重新扫描</param>
        /// <param name="excludeEditor">是否排除 Editor 目录</param>
        public static List<ScanResult> ScanFolder(string folderPath, bool forceRescan = false, bool excludeEditor = true)
        {
            // 缓存检查
            if (!forceRescan && mLastScanFolder == folderPath && 
                (DateTime.Now - mLastScanTime).TotalSeconds < 30)
            {
                return CachedResults;
            }

            CachedResults.Clear();
            mLastScanFolder = folderPath;
            mLastScanTime = DateTime.Now;

            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"[EventCodeScanner] 文件夹不存在: {folderPath}");
                return CachedResults;
            }

            var csFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);
            
            foreach (var file in csFiles)
            {
                // 根据开关决定是否过滤 Editor 目录
                if (excludeEditor && (file.Contains("\\Editor\\") || file.Contains("/Editor/"))) continue;
                
                ScanFile(file);
            }

            return CachedResults;
        }

        private static void ScanFile(string filePath)
        {
            try
            {
                var lines = File.ReadAllLines(filePath);
                var relativePath = GetRelativePath(filePath);

                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    var lineNumber = i + 1;

                    // Enum 事件 - 带参数类型提取
                    ScanEnumLine(line, EnumRegisterPattern, relativePath, lineNumber, "Register");
                    ScanEnumLine(line, EnumSendPattern, relativePath, lineNumber, "Send");
                    ScanEnumLine(line, EnumUnRegisterPattern, relativePath, lineNumber, "UnRegister");
                    
                    // Type 事件 - 多种模式
                    ScanTypeLine(line, TypeRegisterPattern, relativePath, lineNumber, "Register");
                    ScanTypeLine(line, TypeSendGenericPattern, relativePath, lineNumber, "Send");
                    ScanTypeLine(line, TypeSendNewPattern, relativePath, lineNumber, "Send");
                    ScanTypeLine(line, TypeUnRegisterPattern, relativePath, lineNumber, "UnRegister");
                    
                    // String 事件 - 带参数类型提取
                    ScanStringLine(line, StringRegisterPattern, relativePath, lineNumber, "Register");
                    ScanStringLine(line, StringSendPattern, relativePath, lineNumber, "Send");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EventCodeScanner] 扫描文件失败: {filePath}, {e.Message}");
            }
        }

        private static void ScanEnumLine(string line, Regex pattern, string filePath, int lineNumber, string callType)
        {
            var match = pattern.Match(line);
            if (!match.Success) return;

            var genericParams = match.Groups[1].Value.Trim();
            var eventKey = match.Groups[2].Value;
            var paramType = ExtractParamType(genericParams);

            CachedResults.Add(new ScanResult
            {
                FilePath = filePath,
                LineNumber = lineNumber,
                LineContent = line.Trim(),
                EventType = "Enum",
                CallType = callType,
                EventKey = eventKey,
                ParamType = paramType
            });
        }

        private static void ScanTypeLine(string line, Regex pattern, string filePath, int lineNumber, string callType)
        {
            var match = pattern.Match(line);
            if (!match.Success) return;

            CachedResults.Add(new ScanResult
            {
                FilePath = filePath,
                LineNumber = lineNumber,
                LineContent = line.Trim(),
                EventType = "Type",
                CallType = callType,
                EventKey = match.Groups[1].Value,
                ParamType = match.Groups[1].Value // Type 事件的类型本身就是参数
            });
        }

        private static void ScanStringLine(string line, Regex pattern, string filePath, int lineNumber, string callType)
        {
            var match = pattern.Match(line);
            if (!match.Success) return;

            var genericParams = match.Groups[1].Value.Trim();
            var eventKey = match.Groups[2].Value;

            CachedResults.Add(new ScanResult
            {
                FilePath = filePath,
                LineNumber = lineNumber,
                LineContent = line.Trim(),
                EventType = "String",
                CallType = callType,
                EventKey = eventKey,
                ParamType = string.IsNullOrEmpty(genericParams) ? "void" : genericParams
            });
        }

        /// <summary>
        /// 从泛型参数中提取参数类型
        /// 例如: "GameEvent, int" -> "int"
        /// 例如: "GameEvent" -> "void"
        /// 例如: "" -> "void"
        /// </summary>
        private static string ExtractParamType(string genericParams)
        {
            if (string.IsNullOrEmpty(genericParams))
                return "void";

            var commaIndex = genericParams.IndexOf(',');
            if (commaIndex < 0)
                return "void"; // 只有枚举类型，无参数

            // 取逗号后面的部分作为参数类型
            return genericParams[(commaIndex + 1)..].Trim();
        }

        private static string GetRelativePath(string fullPath)
        {
            var assetsIndex = fullPath.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
            return assetsIndex >= 0 ? fullPath[assetsIndex..].Replace("\\", "/") : fullPath;
        }

        /// <summary>
        /// 清除缓存
        /// </summary>
        public static void ClearCache()
        {
            CachedResults.Clear();
            mLastScanFolder = null;
        }
    }
}
