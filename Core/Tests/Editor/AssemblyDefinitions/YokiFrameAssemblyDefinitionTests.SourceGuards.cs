using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 提供程序集测试共用的源码宏、宿主 API 标识符和路径判断。
    /// </summary>
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        /// <summary>
        /// 判断 asmdef 是否是 Tool/Runtime 根上的直接程序集，而不是 Adapter、Integration 或 Tests。
        /// </summary>
        /// <param name="toolsRoot">Tools 根目录绝对路径。</param>
        /// <param name="asmdefPath">待判断的 asmdef 绝对路径。</param>
        /// <returns>相对路径正好是 Kit/Runtime/文件名时返回 true。</returns>
        private static bool IsDirectToolRuntimeAsmdef(string toolsRoot, string asmdefPath)
        {
            string relativePath = NormalizePath(asmdefPath).Substring(NormalizePath(toolsRoot).Length).TrimStart('/');
            string[] segments = relativePath.Split('/');
            return segments.Length == 3 && string.Equals(segments[1], "Runtime", StringComparison.Ordinal);
        }

        /// <summary>
        /// 断言路径的每一段目录名都不等于指定名称。
        /// </summary>
        /// <param name="assetPath">使用正斜杠的项目相对路径。</param>
        /// <param name="segment">禁止出现的目录名。</param>
        private static void AssertDirectorySegmentAbsent(string assetPath, string segment)
        {
            string[] parts = assetPath.Split('/');
            for (int index = 0; index < parts.Length; index++)
            {
                Assert.AreNotEqual(segment, parts[index], "路径不能包含目录 " + segment + ": " + assetPath);
            }
        }

        /// <summary>
        /// 验证指定目录下不存在 asmdef，确保这些 Core Kit 直接编入 YokiFrame 主程序集。
        /// </summary>
        /// <param name="assetPath">Unity 项目内目录路径。</param>
        private static void AssertNoAsmdefUnder(string assetPath)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            Assert.IsTrue(Directory.Exists(fullPath), "缺少 Core Kit 目录: " + assetPath);
            string[] asmdefPaths = Directory.GetFiles(fullPath, "*.asmdef", SearchOption.AllDirectories);
            Assert.AreEqual(0, asmdefPaths.Length, "Core Kit 不应单独声明程序集: " + assetPath);
        }

        /// <summary>
        /// 扫描一个 Runtime 根，逐个文件检查宿主 API 位置和整文件宏。
        /// </summary>
        /// <param name="sourceRoot">Runtime 源码根。</param>
        private static void AssertHostRuntimeRoot(string sourceRoot)
        {
            string[] sourcePaths = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(sourcePaths.Length, 0, "Runtime 目录必须包含源码: " + NormalizePath(sourceRoot));
            for (int index = 0; index < sourcePaths.Length; index++)
            {
                AssertHostSpecificRuntimeSource(sourcePaths[index]);
            }
        }

        /// <summary>
        /// 验证单个 Runtime 源码的宿主 API、目录归属和整文件宏边界。
        /// </summary>
        /// <param name="sourcePath">待检查 Runtime 源码路径。</param>
        private static void AssertHostSpecificRuntimeSource(string sourcePath)
        {
            if (IsGeneratedBuildSourcePath(sourcePath))
            {
                return;
            }

            string source = File.ReadAllText(sourcePath);
            bool usesUnity = ContainsUnityRuntimeApi(source);
            bool usesGodot = ContainsGodotRuntimeApi(source);
            bool usesUnityEditor = ContainsUnityEditorApi(source);
            if (!usesUnity && !usesGodot && !usesUnityEditor)
            {
                return;
            }

            string normalizedPath = NormalizePath(sourcePath);
            Assert.IsFalse(usesUnityEditor, "Runtime Adapter 禁止引用 UnityEditor: " + normalizedPath);
            Assert.IsFalse(usesUnity && usesGodot, "单个 Runtime Adapter 文件不能同时绑定 Unity 与 Godot: " + normalizedPath);
            string expectedPath = usesUnity ? UNITY_RUNTIME_ADAPTER_PATH_FRAGMENT : GODOT_RUNTIME_ADAPTER_PATH_FRAGMENT;
            Assert.IsTrue(
                normalizedPath.IndexOf(expectedPath, StringComparison.Ordinal) >= 0,
                "Runtime 宿主实现必须位于匹配的 Core/Adapters/<Engine>/Runtime: " + normalizedPath);
            AssertWholeFileGuard(sourcePath, usesUnity ? UNITY_ADAPTER_DEFINE : GODOT_ADAPTER_DEFINE);
        }

        /// <summary>
        /// 断言单个共享 Editor 源码没有宿主 API。
        /// </summary>
        /// <param name="sourcePath">待检查源码路径。</param>
        private static void AssertSourceDoesNotUseHostApi(string sourcePath)
        {
            string source = File.ReadAllText(sourcePath);
            string normalizedPath = NormalizePath(sourcePath);
            Assert.IsFalse(ContainsUnityEditorApi(source), "共享 Editor 禁止引用 UnityEditor: " + normalizedPath);
            Assert.IsFalse(ContainsUnityRuntimeApi(source), "共享 Editor 禁止引用 UnityEngine: " + normalizedPath);
            Assert.IsFalse(ContainsGodotRuntimeApi(source), "共享 Editor 禁止引用 Godot: " + normalizedPath);
        }

        /// <summary>
        /// 验证源码文件的第一条有效行和最后一条有效行组成完整条件编译边界。
        /// </summary>
        /// <param name="sourcePath">源码文件路径。</param>
        /// <param name="expectedDefine">期望的起始宏。</param>
        private static void AssertWholeFileGuard(string sourcePath, string expectedDefine)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            string firstLine = FirstNonEmptyLine(lines);
            string lastLine = LastNonEmptyLine(lines);
            bool matchesExpectedDefine = string.Equals(firstLine, expectedDefine, StringComparison.Ordinal)
                || firstLine.StartsWith(expectedDefine + " &&", StringComparison.Ordinal)
                || string.Equals(expectedDefine, UNITY_ADAPTER_DEFINE, StringComparison.Ordinal)
                    && firstLine.StartsWith("#if UNITY_2022_3_OR_NEWER", StringComparison.Ordinal);
            Assert.IsTrue(matchesExpectedDefine, "源码文件必须从指定宿主宏开始: " + NormalizePath(sourcePath) + " 实际为: " + firstLine);
            Assert.AreEqual("#endif", lastLine, "源码文件必须用 #endif 结束，确保整文件被宏包裹: " + NormalizePath(sourcePath));
        }

        /// <summary>
        /// 从行数组中获取第一条非空行。
        /// </summary>
        /// <param name="lines">源码行数组。</param>
        /// <returns>第一条非空行；不存在时返回空字符串。</returns>
        private static string FirstNonEmptyLine(string[] lines)
        {
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 从行数组中获取最后一条非空行。
        /// </summary>
        /// <param name="lines">源码行数组。</param>
        /// <returns>最后一条非空行；不存在时返回空字符串。</returns>
        private static string LastNonEmptyLine(string[] lines)
        {
            for (int index = lines.Length - 1; index >= 0; index--)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 判断源码是否直接使用 Unity Runtime API。只认行首 using 和独立的 UnityEngine 限定名。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <returns>包含 UnityEngine 引用时返回 true。</returns>
        private static bool ContainsUnityRuntimeApi(string source)
        {
            return HasLineStartUsing(source, "UnityEngine") || HasNamespaceQualifier(source, "UnityEngine", true);
        }

        /// <summary>
        /// 判断源码是否直接使用 Unity Editor API。只认行首 using 和独立的 UnityEditor 限定名。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <returns>包含 UnityEditor 引用时返回 true。</returns>
        private static bool ContainsUnityEditorApi(string source)
        {
            return HasLineStartUsing(source, "UnityEditor") || HasNamespaceQualifier(source, "UnityEditor", true);
        }

        /// <summary>
        /// 判断源码是否直接使用 Godot Runtime API。点号前缀不视为 API，避免把 YokiFrame.Godot 程序集名误判为宿主调用。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <returns>包含 Godot 引用时返回 true。</returns>
        private static bool ContainsGodotRuntimeApi(string source)
        {
            return HasLineStartUsing(source, "Godot") || HasNamespaceQualifier(source, "Godot", false);
        }

        /// <summary>
        /// 判断源码是否包含位于行首的 using 指令。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <param name="namespaceName">命名空间。</param>
        /// <returns>存在对应 using 时返回 true。</returns>
        private static bool HasLineStartUsing(string source, string namespaceName)
        {
            string directive = "using " + namespaceName + ";";
            int start = 0;
            while (start < source.Length)
            {
                int index = source.IndexOf(directive, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    return false;
                }

                if (IsTokenAtLineStart(source, index))
                {
                    return true;
                }

                start = index + directive.Length;
            }

            return false;
        }

        /// <summary>
        /// 判断源码是否把名称用作命名空间限定符。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <param name="namespaceName">命名空间。</param>
        /// <param name="allowDottedPrefix">为 false 时，前面的点号不算限定符起点。</param>
        /// <returns>存在独立限定符时返回 true。</returns>
        private static bool HasNamespaceQualifier(string source, string namespaceName, bool allowDottedPrefix)
        {
            string prefix = namespaceName + ".";
            int start = 0;
            while (start < source.Length)
            {
                int index = source.IndexOf(prefix, start, StringComparison.Ordinal);
                if (index < 0)
                {
                    return false;
                }

                bool leftBound = index == 0 || !IsIdentifierCharacter(source[index - 1]);
                bool precededByDot = index > 0 && source[index - 1] == '.';
                if (leftBound && (allowDottedPrefix || !precededByDot))
                {
                    return true;
                }

                start = index + prefix.Length;
            }

            return false;
        }

        /// <summary>
        /// 判断标记之前到行首是否只有空白。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <param name="index">标记起点。</param>
        /// <returns>标记位于行首时返回 true。</returns>
        private static bool IsTokenAtLineStart(string source, int index)
        {
            int cursor = index - 1;
            while (cursor >= 0 && source[cursor] != '\n' && source[cursor] != '\r')
            {
                if (!char.IsWhiteSpace(source[cursor]))
                {
                    return false;
                }

                cursor--;
            }

            return true;
        }

        /// <summary>
        /// 判断字符是否属于 C# 标识符的一部分。
        /// </summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>字母、数字或下划线时返回 true。</returns>
        private static bool IsIdentifierCharacter(char value)
        {
            return value >= 'A' && value <= 'Z'
                || value >= 'a' && value <= 'z'
                || value >= '0' && value <= '9'
                || value == '_';
        }

        /// <summary>
        /// 判断源码路径是否属于宿主 SDK 生成的构建缓存，避免把临时 AssemblyInfo 当成框架权威源码。
        /// </summary>
        /// <param name="sourcePath">待检查的 C# 文件路径。</param>
        /// <returns>位于 `.godot`、`bin` 或 `obj` 目录时返回 true。</returns>
        private static bool IsGeneratedBuildSourcePath(string sourcePath)
        {
            string normalizedPath = NormalizePath(sourcePath);
            return normalizedPath.IndexOf("/.godot/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/bin/", StringComparison.OrdinalIgnoreCase) >= 0
                || normalizedPath.IndexOf("/obj/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 把文件路径归一化为正斜杠，便于跨平台检查目录片段。
        /// </summary>
        /// <param name="path">原始文件路径。</param>
        /// <returns>使用正斜杠的标准路径。</returns>
        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }
    }
}
