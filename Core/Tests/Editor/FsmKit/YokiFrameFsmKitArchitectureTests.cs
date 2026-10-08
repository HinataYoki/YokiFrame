using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 锁定 FsmKit 编入 Core 主程序集，以及诊断源码的整文件 Editor/Tools 边界。
    /// </summary>
    public sealed class YokiFrameFsmKitArchitectureTests
    {
        private const string EDITOR_TOOLS_DEFINE = "#if UNITY_EDITOR || (GODOT && TOOLS)";
        private static readonly string[] FORBIDDEN_REFERENCES =
        {
            "using UnityEngine",
            "using UnityEditor",
            "using Godot",
            "Cysharp.Threading.Tasks"
        };

        /// <summary>
        /// 验证 FsmKit 运行时类型编入 YokiFrame，且目录下没有独立 asmdef。
        /// </summary>
        [Test]
        public void AssemblyBoundary_FsmKit运行时_编入Core且没有独立asmdef()
        {
            string fsmRoot = GetFsmRuntimeRoot();

            Assert.AreEqual("YokiFrame", typeof(IState).Assembly.GetName().Name);
            Assert.IsEmpty(Directory.GetFiles(fsmRoot, "*.asmdef", SearchOption.AllDirectories));
        }

        /// <summary>
        /// 验证 FsmKit Runtime 源码不引用 Unity、Godot 或 UniTask。
        /// 按独立引用片段判断，避免注释里的宿主名称被误判。
        /// </summary>
        [Test]
        public void SourceGuard_FsmKit运行时源码_不引用宿主或UniTask()
        {
            string[] sourcePaths = Directory.GetFiles(GetFsmRuntimeRoot(), "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(sourcePaths);
            for (int index = 0; index < sourcePaths.Length; index++)
            {
                AssertNoForbiddenReference(sourcePaths[index]);
            }
        }

        /// <summary>
        /// 验证诊断目录和观察分部使用整文件 Editor/Tools 条件，避免进入 Player。
        /// </summary>
        [Test]
        public void SourceGuard_诊断与观察源码_整文件使用Editor或Tools宏()
        {
            string runtimeRoot = GetFsmRuntimeRoot();
            string[] sourcePaths = Directory.GetFiles(
                Path.Combine(runtimeRoot, "Diagnostics"),
                "*.cs",
                SearchOption.TopDirectoryOnly);
            Assert.IsNotEmpty(sourcePaths);
            for (int index = 0; index < sourcePaths.Length; index++)
            {
                AssertWholeFileGuard(sourcePaths[index]);
            }

            AssertWholeFileGuard(Path.Combine(runtimeRoot, "Machines", "FSM.Observation.cs"));
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
        /// 断言单个源码文件没有宿主或 UniTask 引用片段。
        /// </summary>
        /// <param name="sourcePath">被检查源码路径。</param>
        private static void AssertNoForbiddenReference(string sourcePath)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                for (int tokenIndex = 0; tokenIndex < FORBIDDEN_REFERENCES.Length; tokenIndex++)
                {
                    Assert.IsFalse(
                        HasReference(lines[lineIndex], FORBIDDEN_REFERENCES[tokenIndex]),
                        "FsmKit Core 禁止引用 " + FORBIDDEN_REFERENCES[tokenIndex] + ": " + sourcePath);
                }
            }
        }

        /// <summary>
        /// 断言源码首尾使用统一 Editor/Tools 条件编译边界。
        /// </summary>
        /// <param name="sourcePath">待检查源码路径。</param>
        private static void AssertWholeFileGuard(string sourcePath)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            Assert.AreEqual(EDITOR_TOOLS_DEFINE, FindFirstNonEmptyLine(lines), sourcePath);
            Assert.AreEqual("#endif", FindLastNonEmptyLine(lines), sourcePath);
        }

        /// <summary>
        /// 判断一行里是否出现独立的禁止引用片段。
        /// </summary>
        /// <param name="line">源码行。</param>
        /// <param name="reference">禁止出现的引用片段。</param>
        /// <returns>出现独立片段时返回 true。</returns>
        private static bool HasReference(string line, string reference)
        {
            for (int index = 0; index <= line.Length - reference.Length; index++)
            {
                if (string.CompareOrdinal(line, index, reference, 0, reference.Length) != 0)
                {
                    continue;
                }

                bool leftBoundary = index == 0 || IsIdentifierChar(line[index - 1]) == false;
                int end = index + reference.Length;
                bool rightBoundary = end == line.Length || IsIdentifierChar(line[end]) == false;
                if (leftBoundary && rightBoundary)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断字符是否属于 C# 标识符，避免把更长的名字误判为禁止引用。
        /// </summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>可作为标识符的一部分时返回 true。</returns>
        private static bool IsIdentifierChar(char value)
        {
            return value == '_'
                || (value >= '0' && value <= '9')
                || (value >= 'A' && value <= 'Z')
                || (value >= 'a' && value <= 'z');
        }

        /// <summary>
        /// 从开头寻找第一条非空源码行。
        /// </summary>
        /// <param name="lines">源码行。</param>
        /// <returns>找到的非空行；不存在时返回空字符串。</returns>
        private static string FindFirstNonEmptyLine(string[] lines)
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
        /// 从末尾寻找最后一条非空源码行。
        /// </summary>
        /// <param name="lines">源码行。</param>
        /// <returns>找到的非空行；不存在时返回空字符串。</returns>
        private static string FindLastNonEmptyLine(string[] lines)
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
    }
}
