using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>验证 SceneKit、ResKit 场景契约和具体宿主实现之间的长期依赖边界。</summary>
    [TestFixture]
    public sealed class SceneKitArchitectureTests
    {
        private const string UNITY_PROVIDER_PATH = "YokiFrame/Core/Adapters/Unity/Runtime/ResKit/Resources/UnityResourceProvider.cs";
        private const string GODOT_PROVIDER_PATH = "YokiFrame/Core/Adapters/Godot/Runtime/ResKit/GodotResourceProvider.cs";
        private const string YOOASSET_PROVIDER_PATH = "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YooAssetResourceProvider.cs";

        /// <summary>验证普通资源、raw 与场景能力接口互不继承，避免一个 Provider 被迫实现无关能力。</summary>
        [Test]
        public void ResKitProviderCapabilitiesRemainIndependent()
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

        /// <summary>验证三个已支持 Provider 都显式声明场景能力，并保持整文件宿主宏。</summary>
        [Test]
        public void SupportedProvidersDeclareSceneCapabilityAndWholeFileGuards()
        {
            AssertProviderSource(UNITY_PROVIDER_PATH, "#if UNITY_2022_3_OR_NEWER");
            AssertProviderSource(GODOT_PROVIDER_PATH, "#if GODOT");
            AssertProviderSource(
                YOOASSET_PROVIDER_PATH,
                "#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3");
        }

        /// <summary>验证 SceneKit Runtime 只依赖纯 C# Core 契约，不直接选择 Unity、Godot 或 YooAsset。</summary>
        [Test]
        public void SceneKitRuntimeDoesNotReferenceHostApis()
        {
            string runtimeRoot = PackagePath("YokiFrame/Tools/SceneKit/Runtime");
            string[] sources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);

            Assert.IsNotEmpty(sources);
            for (var index = 0; index < sources.Length; index++)
            {
                string source = File.ReadAllText(sources[index]);
                string path = NormalizePath(sources[index]);
                Assert.IsFalse(source.Contains("UnityEngine"), "SceneKit Runtime 禁止引用 Unity: " + path);
                Assert.IsFalse(source.Contains("UnityEditor"), "SceneKit Runtime 禁止引用 UnityEditor: " + path);
                Assert.IsFalse(source.Contains("using Godot;") || source.Contains("Godot."),
                    "SceneKit Runtime 禁止引用 Godot: " + path);
                Assert.IsFalse(source.Contains("YooAsset"), "SceneKit Runtime 禁止引用 YooAsset: " + path);
                Assert.IsFalse(source.Contains("#if UNITY_EDITOR"), "SceneKit Runtime 禁止包含 Editor 宏: " + path);
                Assert.IsFalse(source.Contains("#if GODOT"), "SceneKit Runtime 禁止包含 Godot 宏: " + path);
            }
        }

        /// <summary>验证已经编译的 SceneKit Runtime 程序集不链接任何宿主或可选依赖程序集。</summary>
        [Test]
        public void SceneKitRuntimeAssemblyDoesNotReferenceHostAssemblies()
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

        /// <summary>读取 Provider 源码并验证场景接口声明以及首尾整文件宏。</summary>
        /// <param name="relativePath">相对 Assets 的 Provider 源码路径。</param>
        /// <param name="expectedGuard">期望的第一条宿主宏。</param>
        private static void AssertProviderSource(string relativePath, string expectedGuard)
        {
            string sourcePath = PackagePath(relativePath);
            string source = File.ReadAllText(sourcePath);
            string[] lines = File.ReadAllLines(sourcePath);

            Assert.IsTrue(source.Contains("IResSceneProvider"), "Provider 必须实现 IResSceneProvider: " + relativePath);
            Assert.AreEqual(expectedGuard, FirstNonEmptyLine(lines), "Provider 必须使用匹配的整文件宿主宏: " + relativePath);
            Assert.AreEqual("#endif", LastNonEmptyLine(lines), "Provider 必须以 #endif 结束: " + relativePath);
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
