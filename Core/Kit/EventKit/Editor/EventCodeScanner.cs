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
        }

        // 正则表达式模式
        private static readonly Regex EnumRegisterPattern = new(
            @"EventKit\.Enum\.Register\s*<\s*(\w+)\s*>\s*\(\s*(\w+\.\w+)",
            RegexOptions.Compiled);
        
        private static readonly Regex EnumSendPattern = new(
            @"EventKit\.Enum\.Send\s*<?\s*\w*\s*>?\s*\(\s*(\w+\.\w+)",
            RegexOptions.Compiled);
        
        private static readonly Regex TypeRegisterPattern = new(
            @"EventKit\.Type\.Register\s*<\s*(\w+)\s*>",
            RegexOptions.Compiled);
        
        private static readonly Regex TypeSendPattern = new(
            @"EventKit\.Type\.Send\s*<\s*(\w+)\s*>",
            RegexOptions.Compiled);
        
        private static readonly Regex StringRegisterPattern = new(
            @"EventKit\.String\.Register\s*(?:<\s*\w+\s*>)?\s*\(\s*""([^""]+)""",
            RegexOptions.Compiled);
        
        private static readonly Regex StringSendPattern = new(
            @"EventKit\.String\.Send\s*(?:<\s*\w+\s*>)?\s*\(\s*""([^""]+)""",
            RegexOptions.Compiled);

        private static readonly List<ScanResult> CachedResults = new();
        private static string mLastScanFolder;
        private static DateTime mLastScanTime;

        /// <summary>
        /// 扫描指定文件夹下的所有 C# 文件
        /// </summary>
        public static List<ScanResult> ScanFolder(string folderPath, bool forceRescan = false)
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
                // 跳过 Editor 文件夹和 YokiFrame 内部代码
                if (file.Contains("Editor") || file.Contains("YokiFrame")) continue;
                
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

                    // Enum 事件
                    ScanLine(line, EnumRegisterPattern, relativePath, lineNumber, "Enum", "Register", 2);
                    ScanLine(line, EnumSendPattern, relativePath, lineNumber, "Enum", "Send", 1);
                    
                    // Type 事件
                    ScanLine(line, TypeRegisterPattern, relativePath, lineNumber, "Type", "Register", 1);
                    ScanLine(line, TypeSendPattern, relativePath, lineNumber, "Type", "Send", 1);
                    
                    // String 事件
                    ScanLine(line, StringRegisterPattern, relativePath, lineNumber, "String", "Register", 1);
                    ScanLine(line, StringSendPattern, relativePath, lineNumber, "String", "Send", 1);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EventCodeScanner] 扫描文件失败: {filePath}, {e.Message}");
            }
        }

        private static void ScanLine(string line, Regex pattern, string filePath, int lineNumber, 
            string eventType, string callType, int keyGroupIndex)
        {
            var match = pattern.Match(line);
            if (!match.Success) return;

            CachedResults.Add(new ScanResult
            {
                FilePath = filePath,
                LineNumber = lineNumber,
                LineContent = line.Trim(),
                EventType = eventType,
                CallType = callType,
                EventKey = match.Groups[keyGroupIndex].Value
            });
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
