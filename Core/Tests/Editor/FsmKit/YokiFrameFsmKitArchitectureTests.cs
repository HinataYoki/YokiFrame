using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 锁定 FsmKit 的 Core Kit 目录、程序集归属和纯 C# 依赖边界。
    /// </summary>
    public sealed class YokiFrameFsmKitArchitectureTests
    {
        private const string EDITOR_TOOLS_DEFINE = "#if UNITY_EDITOR || (GODOT && TOOLS)";

        /// <summary>
        /// 验证 Runtime 源码只落在契约、状态、机器、诊断和统一交互五个职责目录中。
        /// </summary>
        [Test]
        public void RuntimeUsesRequiredResponsibilityDirectories()
        {
            string runtimeRoot = GetFsmRuntimeRoot();
            string[] directories = Directory.GetDirectories(runtimeRoot, "*", SearchOption.TopDirectoryOnly);
            string[] directoryNames = new string[directories.Length];
            for (var index = 0; index < directories.Length; index++)
            {
                directoryNames[index] = Path.GetFileName(directories[index]);
            }

            CollectionAssert.AreEquivalent(
                new[] { "Contracts", "States", "Machines", "Diagnostics" },
                directoryNames);
            Assert.IsEmpty(Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.TopDirectoryOnly));
            Assert.IsFalse(File.Exists(Path.Combine(runtimeRoot, ".keep")));
        }

        /// <summary>
        /// 验证 FsmKit 编入 Core 主程序集，且源码没有宿主 SDK 或可选依赖引用。
        /// </summary>
        [Test]
        public void RuntimeStaysInPureCoreAssemblyBoundary()
        {
            string fsmRoot = GetFsmRuntimeRoot();
            Assert.IsEmpty(Directory.GetFiles(fsmRoot, "*.asmdef", SearchOption.AllDirectories));

            string[] sourcePaths = Directory.GetFiles(fsmRoot, "*.cs", SearchOption.AllDirectories);
            for (var index = 0; index < sourcePaths.Length; index++)
            {
                string source = File.ReadAllText(sourcePaths[index]);
                AssertForbiddenDependency(sourcePaths[index], source, "using UnityEngine");
                AssertForbiddenDependency(sourcePaths[index], source, "using UnityEditor");
                AssertForbiddenDependency(sourcePaths[index], source, "using Godot");
                AssertForbiddenDependency(sourcePaths[index], source, "Cysharp.Threading.Tasks");
            }
        }

        /// <summary>
        /// 验证诊断和 Interaction 文件使用整文件 Editor/Tools 条件，防止类型进入 Player 程序集。
        /// </summary>
        [Test]
        public void ObservationSourcesUseWholeFileEditorToolsGuards()
        {
            string runtimeRoot = GetFsmRuntimeRoot();
            string[] directories = { "Diagnostics" };
            for (var directoryIndex = 0; directoryIndex < directories.Length; directoryIndex++)
            {
                string directory = Path.Combine(runtimeRoot, directories[directoryIndex]);
                string[] sourcePaths = Directory.GetFiles(directory, "*.cs", SearchOption.TopDirectoryOnly);
                for (var sourceIndex = 0; sourceIndex < sourcePaths.Length; sourceIndex++)
                {
                    AssertWholeFileGuard(sourcePaths[sourceIndex]);
                }
            }

            string machines = Path.Combine(runtimeRoot, "Machines");
            AssertWholeFileGuard(Path.Combine(machines, "FSM.Observation.cs"));
        }

        /// <summary>
        /// 获取当前包内 FsmKit Runtime 的绝对路径。
        /// </summary>
        /// <returns>FsmKit Runtime 根路径。</returns>
        private static string GetFsmRuntimeRoot()
        {
            return Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "FsmKit");
        }

        /// <summary>
        /// 断言源码不包含指定依赖入口，并在失败信息中输出准确文件。
        /// </summary>
        /// <param name="sourcePath">被检查源码路径。</param>
        /// <param name="source">源码文本。</param>
        /// <param name="forbiddenText">禁止出现的依赖文本。</param>
        private static void AssertForbiddenDependency(
            string sourcePath,
            string source,
            string forbiddenText)
        {
            Assert.IsFalse(
                source.Contains(forbiddenText),
                "FsmKit Core 禁止依赖 '" + forbiddenText + "': " + sourcePath);
        }

        /// <summary>断言源码首尾使用统一 Editor/Tools 条件编译边界。</summary>
        /// <param name="sourcePath">待检查源码路径。</param>
        private static void AssertWholeFileGuard(string sourcePath)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            Assert.AreEqual(EDITOR_TOOLS_DEFINE, FindFirstNonEmptyLine(lines), sourcePath);
            Assert.AreEqual("#endif", FindLastNonEmptyLine(lines), sourcePath);
        }

        /// <summary>从开头寻找第一条非空源码行。</summary>
        /// <param name="lines">源码行。</param>
        /// <returns>找到的非空行；不存在时返回空字符串。</returns>
        private static string FindFirstNonEmptyLine(string[] lines)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length > 0) return line;
            }

            return string.Empty;
        }

        /// <summary>从末尾寻找最后一条非空源码行。</summary>
        /// <param name="lines">源码行。</param>
        /// <returns>找到的非空行；不存在时返回空字符串。</returns>
        private static string FindLastNonEmptyLine(string[] lines)
        {
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                string line = lines[index].Trim();
                if (line.Length > 0) return line;
            }

            return string.Empty;
        }
    }
}
