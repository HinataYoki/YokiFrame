using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>验证 SceneKit、ResKit 场景契约和具体宿主实现之间的依赖边界。</summary>
    [TestFixture]
    public sealed class SceneKitArchitectureTests
    {
        private const string UNITY_PROVIDER_PATH = "YokiFrame/Core/Adapters/Unity/Runtime/ResKit/Resources/UnityResourceProvider.cs";
        private const string GODOT_PROVIDER_PATH = "YokiFrame/Core/Adapters/Godot/Runtime/ResKit/GodotResourceProvider.cs";
        private const string YOOASSET_PROVIDER_PATH = "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YooAssetResourceProvider.cs";
        private const string ENDIF_DIRECTIVE = "#endif";

        private static readonly string[] FORBIDDEN_HOST_TOKENS =
        {
            "UnityEngine",
            "UnityEditor",
            "UNITY_EDITOR",
            "Godot",
            "YooAsset",
        };

        /// <summary>验证普通资源、raw 与场景接口互不继承。</summary>
        [Test]
        public void IsAssignableFrom_资源Raw与场景接口_互不继承()
        {
            Type resource = typeof(IResourceProvider);
            Type raw = typeof(IRawResourceProvider);
            Type scene = typeof(IResSceneProvider);

            Assert.IsFalse(resource.IsAssignableFrom(raw));
            Assert.IsFalse(resource.IsAssignableFrom(scene));
            Assert.IsFalse(raw.IsAssignableFrom(resource));
            Assert.IsFalse(raw.IsAssignableFrom(scene));
            Assert.IsFalse(scene.IsAssignableFrom(resource));
            Assert.IsFalse(scene.IsAssignableFrom(raw));
        }

        /// <summary>验证三个 Provider 源码分别由对应宿主宏整文件包裹。</summary>
        [Test]
        public void SourceGuard_三个Provider源码_整文件使用各自宿主宏()
        {
            AssertProviderSource(UNITY_PROVIDER_PATH, "#if UNITY_2022_3_OR_NEWER");
            AssertProviderSource(GODOT_PROVIDER_PATH, "#if GODOT");
            AssertProviderSource(
                YOOASSET_PROVIDER_PATH,
                "#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3");
        }

        /// <summary>验证 SceneKit Runtime 源码不出现宿主 API、宿主宏或 YooAsset 标识。</summary>
        [Test]
        public void SourceGuard_SceneKit运行时源码_不包含宿主标识()
        {
            string runtimeRoot = PackagePath("YokiFrame/Tools/SceneKit/Runtime");
            string[] sources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);

            Assert.IsNotEmpty(sources);
            for (var index = 0; index < sources.Length; index++)
            {
                AssertNoForbiddenHostToken(sources[index]);
            }
        }

        /// <summary>验证已编译的 SceneKit Runtime 程序集不引用 Unity、Godot 或 YooAsset。</summary>
        [Test]
        public void AssemblyBoundary_SceneKit运行时_不引用宿主程序集()
        {
            System.Reflection.AssemblyName[] references = typeof(SceneKit).Assembly.GetReferencedAssemblies();
            for (var index = 0; index < references.Length; index++)
            {
                string assemblyName = references[index].Name ?? string.Empty;
                Assert.IsFalse(assemblyName.StartsWith("Unity", StringComparison.Ordinal),
                    "SceneKit Runtime 禁止引用 Unity 程序集: " + assemblyName);
                Assert.IsFalse(assemblyName.StartsWith("Godot", StringComparison.Ordinal),
                    "SceneKit Runtime 禁止引用 Godot 程序集: " + assemblyName);
                Assert.IsFalse(assemblyName.StartsWith("YooAsset", StringComparison.Ordinal),
                    "SceneKit Runtime 禁止引用 YooAsset 程序集: " + assemblyName);
            }
        }

        /// <summary>验证 Provider 源码的首尾非空行是预期的整文件宏。</summary>
        /// <param name="relativePath">相对 Assets 的 Provider 源码路径。</param>
        /// <param name="expectedGuard">期望的第一条宿主宏。</param>
        private static void AssertProviderSource(string relativePath, string expectedGuard)
        {
            string sourcePath = PackagePath(relativePath);
            string[] lines = File.ReadAllLines(sourcePath);

            Assert.AreEqual(expectedGuard, FirstNonEmptyLine(lines), "Provider 必须使用匹配的整文件宿主宏: " + relativePath);
            Assert.AreEqual(ENDIF_DIRECTIVE, LastNonEmptyLine(lines), "Provider 必须以 #endif 结束: " + relativePath);
        }

        /// <summary>验证单个 Runtime 源码文件没有宿主标识。</summary>
        /// <param name="sourcePath">源码绝对路径。</param>
        private static void AssertNoForbiddenHostToken(string sourcePath)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            string path = NormalizePath(sourcePath);
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                for (var tokenIndex = 0; tokenIndex < FORBIDDEN_HOST_TOKENS.Length; tokenIndex++)
                {
                    Assert.IsFalse(
                        HasToken(lines[lineIndex], FORBIDDEN_HOST_TOKENS[tokenIndex]),
                        "SceneKit Runtime 禁止出现宿主标识 " + FORBIDDEN_HOST_TOKENS[tokenIndex] + ": " + path);
                }
            }
        }

        /// <summary>判断一行里是否有独立标识符与给定记号相同。</summary>
        /// <param name="line">源码行。</param>
        /// <param name="token">禁止出现的标识符。</param>
        /// <returns>出现独立标识符时返回 true。</returns>
        private static bool HasToken(string line, string token)
        {
            for (var index = 0; index <= line.Length - token.Length; index++)
            {
                if (string.CompareOrdinal(line, index, token, 0, token.Length) != 0)
                {
                    continue;
                }

                bool leftBoundary = index == 0 || IsIdentifierChar(line[index - 1]) == false;
                int end = index + token.Length;
                bool rightBoundary = end == line.Length || IsIdentifierChar(line[end]) == false;
                if (leftBoundary && rightBoundary)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>判断字符是否属于 C# 标识符，用于避免把更长的名字误判为宿主标识。</summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>可作为标识符的一部分时返回 true。</returns>
        private static bool IsIdentifierChar(char value)
        {
            return value == '_' || (value >= '0' && value <= '9') || (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
        }

        /// <summary>把相对 Assets 的路径转换为当前项目中的绝对路径。</summary>
        /// <param name="relativePath">相对 Assets 的路径。</param>
        /// <returns>规范化绝对路径。</returns>
        private static string PackagePath(string relativePath)
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
        }

        /// <summary>读取第一条非空行，用于验证整文件条件编译入口。</summary>
        /// <param name="lines">源码行。</param>
        /// <returns>第一条非空行。</returns>
        private static string FirstNonEmptyLine(string[] lines)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }

        /// <summary>读取最后一条非空行，用于验证整文件条件编译出口。</summary>
        /// <param name="lines">源码行。</param>
        /// <returns>最后一条非空行。</returns>
        private static string LastNonEmptyLine(string[] lines)
        {
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }

        /// <summary>把路径统一为正斜杠，便于跨平台错误输出。</summary>
        /// <param name="path">原始路径。</param>
        /// <returns>使用正斜杠的绝对路径。</returns>
        private static string NormalizePath(string path)
        {
            return Path.GetFullPath(path).Replace('\\', '/');
        }
    }
}
