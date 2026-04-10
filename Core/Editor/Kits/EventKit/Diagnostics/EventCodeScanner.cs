using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 代码扫描器。
    /// 用于静态扫描项目中的事件注册、发送与注销调用。
    /// </summary>
    public static class EventCodeScanner
    {
        /// <summary>
        /// 单条扫描结果。
        /// </summary>
        public struct ScanResult
        {
            public string FilePath;
            public int LineNumber;
            public string LineContent;
            public string EventType;
            public string CallType;
            public string EventKey;
            public string ParamType;
        }

        private static readonly Regex EnumRegisterPattern = new(
            @"EventKit\.Enum\.Register\s*(?:<\s*([^>]+)\s*>)?\s*\(\s*(\w+\.\w+)",
            RegexOptions.Compiled);

        private static readonly Regex EnumSendPattern = new(
            @"EventKit\.Enum\.Send\s*(?:<\s*([^>]+)\s*>)?\s*\(\s*(\w+\.\w+)",
            RegexOptions.Compiled);

        private static readonly Regex EnumUnRegisterPattern = new(
            @"EventKit\.Enum\.UnRegister\s*(?:<\s*([^>]+)\s*>)?\s*\(\s*(\w+\.\w+)",
            RegexOptions.Compiled);

        private static readonly Regex TypeRegisterPattern = new(
            @"EventKit\.Type\.Register\s*<\s*(\w+)\s*>",
            RegexOptions.Compiled);

        private static readonly Regex TypeSendGenericPattern = new(
            @"EventKit\.Type\.Send\s*<\s*(\w+)\s*>",
            RegexOptions.Compiled);

        private static readonly Regex TypeSendNewPattern = new(
            @"EventKit\.Type\.Send\s*\(\s*new\s+(\w+)",
            RegexOptions.Compiled);

        private static readonly Regex TypeUnRegisterPattern = new(
            @"EventKit\.Type\.UnRegister\s*<\s*(\w+)\s*>",
            RegexOptions.Compiled);

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
        /// 扫描指定目录下的所有 C# 文件。
        /// </summary>
        public static List<ScanResult> ScanFolder(string folderPath, bool forceRescan = false, bool excludeEditor = true)
        {
            if (!forceRescan &&
                mLastScanFolder == folderPath &&
                (DateTime.Now - mLastScanTime).TotalSeconds < 30)
            {
                return CachedResults;
            }

            CachedResults.Clear();
            mLastScanFolder = folderPath;
            mLastScanTime = DateTime.Now;

            if (!Directory.Exists(folderPath))
            {
                Debug.LogWarning($"[EventCodeScanner] 目录不存在: {folderPath}");
                return CachedResults;
            }

            var csFiles = Directory.GetFiles(folderPath, "*.cs", SearchOption.AllDirectories);
            foreach (var file in csFiles)
            {
                if (excludeEditor && (file.Contains("\\Editor\\") || file.Contains("/Editor/")))
                {
                    continue;
                }

                ScanFile(file);
            }

            return CachedResults;
        }

        /// <summary>
        /// 扫描单个文件中的 EventKit 调用。
        /// </summary>
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

                    ScanEnumLine(line, EnumRegisterPattern, relativePath, lineNumber, "Register");
                    ScanEnumLine(line, EnumSendPattern, relativePath, lineNumber, "Send");
                    ScanEnumLine(line, EnumUnRegisterPattern, relativePath, lineNumber, "UnRegister");

                    ScanTypeLine(line, TypeRegisterPattern, relativePath, lineNumber, "Register");
                    ScanTypeLine(line, TypeSendGenericPattern, relativePath, lineNumber, "Send");
                    ScanTypeLine(line, TypeSendNewPattern, relativePath, lineNumber, "Send");
                    ScanTypeLine(line, TypeUnRegisterPattern, relativePath, lineNumber, "UnRegister");

                    ScanStringLine(line, StringRegisterPattern, relativePath, lineNumber, "Register");
                    ScanStringLine(line, StringSendPattern, relativePath, lineNumber, "Send");
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EventCodeScanner] 扫描文件失败: {filePath}, {e.Message}");
            }
        }

        /// <summary>
        /// 扫描 Enum 事件调用。
        /// </summary>
        private static void ScanEnumLine(string line, Regex pattern, string filePath, int lineNumber, string callType)
        {
            var match = pattern.Match(line);
            if (!match.Success)
            {
                return;
            }

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

        /// <summary>
        /// 扫描 Type 事件调用。
        /// </summary>
        private static void ScanTypeLine(string line, Regex pattern, string filePath, int lineNumber, string callType)
        {
            var match = pattern.Match(line);
            if (!match.Success)
            {
                return;
            }

            CachedResults.Add(new ScanResult
            {
                FilePath = filePath,
                LineNumber = lineNumber,
                LineContent = line.Trim(),
                EventType = "Type",
                CallType = callType,
                EventKey = match.Groups[1].Value,
                ParamType = match.Groups[1].Value
            });
        }

        /// <summary>
        /// 扫描 String 事件调用。
        /// </summary>
        private static void ScanStringLine(string line, Regex pattern, string filePath, int lineNumber, string callType)
        {
            var match = pattern.Match(line);
            if (!match.Success)
            {
                return;
            }

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
        /// 从泛型参数中提取事件参数类型。
        /// 例如 `GameEvent, int` 返回 `int`。
        /// </summary>
        private static string ExtractParamType(string genericParams)
        {
            if (string.IsNullOrEmpty(genericParams))
            {
                return "void";
            }

            var commaIndex = genericParams.IndexOf(',');
            if (commaIndex < 0)
            {
                return "void";
            }

            return genericParams[(commaIndex + 1)..].Trim();
        }

        /// <summary>
        /// 将绝对路径转换为以 `Assets` 开头的项目相对路径。
        /// </summary>
        private static string GetRelativePath(string fullPath)
        {
            var assetsIndex = fullPath.IndexOf("Assets", StringComparison.OrdinalIgnoreCase);
            return assetsIndex >= 0 ? fullPath[assetsIndex..].Replace("\\", "/") : fullPath;
        }

        /// <summary>
        /// 清空扫描缓存。
        /// </summary>
        public static void ClearCache()
        {
            CachedResults.Clear();
            mLastScanFolder = null;
        }
    }
}
