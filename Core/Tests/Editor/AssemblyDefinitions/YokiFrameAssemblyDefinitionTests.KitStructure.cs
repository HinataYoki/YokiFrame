using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        /// <summary>
        /// 验证 Architecture Runtime 文件名不带 Editor，且编辑器钩子只使用共享工具宏。
        /// </summary>
        [Test]
        public void SourceGuard_Architecture运行时_编辑器钩子使用共享工具宏()
        {
            string runtimeRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "Architecture");
            Assert.IsTrue(Directory.Exists(runtimeRoot), "Architecture 运行时代码必须位于 Core/Runtime/Architecture。");
            string[] sourcePaths = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(sourcePaths);

            for (int index = 0; index < sourcePaths.Length; index++)
            {
                string sourcePath = sourcePaths[index];
                string fileName = Path.GetFileNameWithoutExtension(sourcePath);
                Assert.IsFalse(fileName.IndexOf("Editor", System.StringComparison.Ordinal) >= 0, "Architecture 编辑器源码不应位于 Runtime 树: " + NormalizePath(sourcePath));
                AssertPreprocessorExcludesRuntimeGodot(sourcePath);
                if (!HasEditorHookSignal(sourcePath))
                {
                    continue;
                }

                Assert.IsTrue(
                    HasExactTrimmedLine(sourcePath, EDITOR_CONTEXT_DEFINE),
                    "Architecture Runtime 中的编辑器钩子必须使用共享工具宏: " + NormalizePath(sourcePath));
            }
        }

        /// <summary>
        /// 验证 EventKit 完整观察实现位于共享 Editor，Runtime 只保留总线直接调用的最小 Hook。
        /// </summary>
        [Test]
        public void EventKitDiagnosticsUseSharedEditorBoundary_SourceGuard()
        {
            string runtimeRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "EventKit");
            string editorRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Editor", "EventKit");

            Assert.IsTrue(
                File.Exists(Path.Combine(editorRoot, "Diagnostics", "EventKitDiagnosticRegistry.cs")),
                "EventKit 诊断注册表必须位于共享 Core/Editor 边界。");
            Assert.IsTrue(
                File.Exists(Path.Combine(editorRoot, "Diagnostics", "EventKitDiagnosticModels.cs")),
                "EventKit 诊断模型必须位于共享 Core/Editor 边界。");
            Assert.IsFalse(
                File.Exists(Path.Combine(editorRoot, "Facade", "EventKitEditor.cs")),
                "未使用的 EventKitEditor 门面不得保留为平行事件总线。");
            Assert.IsTrue(
                File.Exists(Path.Combine(runtimeRoot, "Monitoring", "EasyEventEditorHook.cs")),
                "总线直接调用的 EasyEventEditorHook 必须保留在 Runtime 最小观察端口。" );

            string[] runtimeSources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
            foreach (string sourcePath in runtimeSources)
            {
                string fileName = Path.GetFileName(sourcePath);
                if (string.Equals(fileName, "EasyEventEditorHook.cs", System.StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.IsFalse(fileName.IndexOf("Editor", System.StringComparison.Ordinal) >= 0, "EventKit 重编辑器实现不应位于 Runtime 树: " + NormalizePath(sourcePath));
                Assert.IsFalse(fileName.IndexOf("Diagnostic", System.StringComparison.Ordinal) >= 0, "EventKit 完整诊断实现不应位于 Runtime 树: " + NormalizePath(sourcePath));
            }
        }

        /// <summary>
        /// 验证 EventKit 共享 Editor 源码整文件使用工具宏，且预处理行不把 Godot Runtime 放进该通道。
        /// </summary>
        [Test]
        public void SourceGuard_EventKit共享Editor源码_整文件使用工具宏()
        {
            string editorRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Editor", "EventKit");
            string[] sourcePaths = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(sourcePaths);

            for (int index = 0; index < sourcePaths.Length; index++)
            {
                AssertWholeFileGuard(sourcePaths[index], EDITOR_CONTEXT_DEFINE);
                AssertPreprocessorExcludesRuntimeGodot(sourcePaths[index]);
            }
        }

        /// <summary>
        /// 验证 EventKit Runtime 的编辑器钩子使用共享工具宏，而不是 Godot 运行时宏。
        /// </summary>
        [Test]
        public void SourceGuard_EventKit运行时_编辑器钩子使用共享工具宏()
        {
            string runtimeRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "EventKit");
            string[] sourcePaths = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(sourcePaths);

            for (int index = 0; index < sourcePaths.Length; index++)
            {
                string sourcePath = sourcePaths[index];
                AssertPreprocessorExcludesRuntimeGodot(sourcePath);
                if (!HasCallTokenInFile(sourcePath, "EasyEventEditorHook"))
                {
                    continue;
                }

                Assert.IsTrue(
                    HasExactTrimmedLine(sourcePath, EDITOR_CONTEXT_DEFINE),
                    "EventKit Runtime 的编辑器钩子必须使用共享工具宏: " + NormalizePath(sourcePath));
            }
        }

        /// <summary>
        /// 验证 Unity Runtime Adapter 源码整文件使用单一 Unity 环境宏。
        /// </summary>
        [Test]
        public void SourceGuard_Unity运行时适配源码_整文件使用单一Unity宏()
        {
            string unityRuntimeRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Adapters", "Unity", "Runtime");
            string[] sourcePaths = Directory.GetFiles(unityRuntimeRoot, "*.cs", SearchOption.AllDirectories);
            Assert.IsNotEmpty(sourcePaths);

            for (int index = 0; index < sourcePaths.Length; index++)
            {
                AssertWholeFileGuard(sourcePaths[index], UNITY_ADAPTER_DEFINE);
            }
        }

        /// <summary>
        /// 验证 LogKit 开发期日志入口的预处理行覆盖 Unity Editor、Unity checks 和 Godot tools。
        /// </summary>
        [Test]
        public void SourceGuard_LogKit开发期日志_使用Editor检查与Godot工具宏()
        {
            string sourcePath = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "LogKit", "Facade", "LogKit.cs");
            Assert.IsTrue(File.Exists(sourcePath), "LogKit 调试入口必须位于 Core/Runtime/LogKit/Facade: " + NormalizePath(sourcePath));
            Assert.IsTrue(
                HasExactTrimmedLine(sourcePath, "#if UNITY_EDITOR || UNITY_ENABLE_CHECKS || (GODOT && TOOLS)"),
                "LogKit 开发期日志入口必须同时覆盖 Unity Editor、Unity checks 和 Godot tools: " + NormalizePath(sourcePath));
            Assert.IsFalse(
                HasExactTrimmedLine(sourcePath, "[System.Diagnostics.Conditional(\"UNITY_EDITOR\")]"),
                "LogKit 开发期日志入口不能只依赖 Unity Editor Conditional: " + NormalizePath(sourcePath));
            AssertPreprocessorExcludesRuntimeGodot(sourcePath);
        }

        /// <summary>
        /// 验证全部 Workbench 诊断、Interaction 和通信协议源码使用整文件工具宏，Player 不编译这些类型。
        /// </summary>
        [Test]
        public void WorkbenchObservationSourcesUseWholeFileToolGuards_SourceGuard()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string coreRuntimeRoot = Path.Combine(packageRoot, "Core", "Runtime");
            string coreEditorRoot = Path.Combine(packageRoot, "Core", "Editor");
            HashSet<string> sourcePaths = new(System.StringComparer.OrdinalIgnoreCase);
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "CommandBridge"));
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "Telemetry"));
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "KitInteraction"));
            AddNamedFolderSources(sourcePaths, coreRuntimeRoot, "Diagnostics");
            // ArchitectureLifetime 负责会话换代时释放全部架构，Player 必须编译它，不能要求整文件工具宏。
            sourcePaths.Remove(Path.Combine(coreRuntimeRoot, "Architecture", "Diagnostics", "ArchitectureLifetime.cs"));
            AddNamedFolderSources(sourcePaths, coreRuntimeRoot, "Interaction");
            AddNamedFolderSources(sourcePaths, coreRuntimeRoot, "Editor");
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "Architecture"));
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "EventKit"));
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "FsmKit"));
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "LogKit"));
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "PoolKit"));
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "ResKit"));
            AddSourcesUnder(sourcePaths, Path.Combine(packageRoot, "Tools", "ActionKit", "Runtime", "Diagnostics"));
            AddSourcesUnder(sourcePaths, Path.Combine(packageRoot, "Tools", "ActionKit", "Editor", "Diagnostics"));
            AddSourcesUnder(sourcePaths, Path.Combine(packageRoot, "Tools", "ActionKit", "Editor", "Interaction"));
            AddSourcesUnder(sourcePaths, Path.Combine(packageRoot, "Tools", "AudioKit", "Runtime", "Diagnostics"));
            AddSourcesUnder(sourcePaths, Path.Combine(packageRoot, "Tools", "AudioKit", "Editor", "Diagnostics"));
            AddSourcesUnder(sourcePaths, Path.Combine(packageRoot, "Tools", "AudioKit", "Editor", "Interaction"));
            AddSourcesUnder(sourcePaths, Path.Combine(packageRoot, "Tools", "AudioKit", "Editor", "Installation"));
            sourcePaths.Add(Path.Combine(
                packageRoot, "Tools", "AudioKit", "Runtime", "Facade", "AudioKit.Diagnostics.cs"));

            Assert.Greater(sourcePaths.Count, 0, "必须找到 Workbench 观察源码，避免空扫描形成假通过。");
            foreach (string sourcePath in sourcePaths)
            {
                string normalizedPath = NormalizePath(sourcePath);
                string expected = normalizedPath.IndexOf("/CommandBridge/", System.StringComparison.Ordinal) >= 0
                                  || normalizedPath.IndexOf("/Telemetry/", System.StringComparison.Ordinal) >= 0
                    ? "#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING"
                    : EDITOR_CONTEXT_DEFINE;
                AssertWholeFileGuard(sourcePath, expected);
            }
        }

        /// <summary>
        /// 验证插入业务源码的观察调用全部位于 Editor/Tools 条件内，防止 Player 留下空 Hook 或诊断成本。
        /// </summary>
        [Test]
        public void RuntimeObservationCallsStayInsideToolConditions_SourceGuard()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string[] sourceRoots =
            {
                Path.Combine(packageRoot, "Core", "Runtime"),
                Path.Combine(packageRoot, "Tools", "ActionKit", "Runtime"),
                Path.Combine(packageRoot, "Tools", "AudioKit", "Runtime")
            };
            string[] observationTokens =
            {
                "ArchitectureRegistry.", "EasyEventEditorHook.", "EventKitDiagnosticRegistry.",
                "FsmEditorHook.", "FsmKitRegistry.", "PoolDebugger.", "SingletonRegistry.",
                "BumpDiagnosticVersionLocked(", "ResUnloadHistory", "ActionKitInteractionRegistration.",
                "ActionKitDiagnosticHistory.", "ActionStackTraceService.", "GodotFileBridgeHost",
                "IEngineLoggerWithStackTrace", "ResolveStackTrace(", "GetDebugInfo(",
                "ListenerCount", "GetListeners(", "GetAllEvents(", "WithCallback(", "mOnUnRegister",
                "EventKitEditorNotification", "WithEditorUnregisterNotification(", "mUnregisterNotification"
            };

            foreach (string sourceRoot in sourceRoots)
            {
                foreach (string sourcePath in Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
                {
                    AssertTokensUseToolCondition(sourcePath, observationTokens);
                }
            }
        }

        /// <summary>把存在目录下的全部 C# 源码加入去重集合。</summary>
        private static void AddSourcesUnder(HashSet<string> sourcePaths, string directoryPath)
        {
            if (!Directory.Exists(directoryPath)) return;
            foreach (string sourcePath in Directory.GetFiles(directoryPath, "*.cs", SearchOption.AllDirectories))
            {
                sourcePaths.Add(sourcePath);
            }
        }

        /// <summary>收集指定根目录下名称完全匹配的职责目录源码。</summary>
        private static void AddNamedFolderSources(
            HashSet<string> sourcePaths,
            string rootPath,
            string folderName)
        {
            foreach (string directoryPath in Directory.GetDirectories(rootPath, folderName, SearchOption.AllDirectories))
            {
                AddSourcesUnder(sourcePaths, directoryPath);
            }
        }

        /// <summary>沿预处理条件栈检查观察 token，只接受位于工具专属分支中的调用。</summary>
        private static void AssertTokensUseToolCondition(string sourcePath, string[] tokens)
        {
            Stack<bool> parentConditions = new();
            bool toolOnly = false;
            string[] lines = File.ReadAllLines(sourcePath);
            for (var index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.StartsWith("#if ", System.StringComparison.Ordinal))
                {
                    parentConditions.Push(toolOnly);
                    toolOnly = toolOnly || IsToolOnlyExpression(line);
                    continue;
                }
                if (line.StartsWith("#elif ", System.StringComparison.Ordinal))
                {
                    toolOnly = parentConditions.Peek() || IsToolOnlyExpression(line);
                    continue;
                }
                if (line == "#else")
                {
                    toolOnly = parentConditions.Peek();
                    continue;
                }
                if (line == "#endif")
                {
                    toolOnly = parentConditions.Pop();
                    continue;
                }

                for (var tokenIndex = 0; tokenIndex < tokens.Length; tokenIndex++)
                {
                    if (!HasCallToken(line, tokens[tokenIndex])) continue;
                    Assert.IsTrue(
                        toolOnly,
                        "Workbench 观察调用必须位于 Editor/Tools 宏内: "
                        + NormalizePath(sourcePath) + ":" + (index + 1) + " " + tokens[tokenIndex]);
                }
            }
        }

        /// <summary>识别不会在 Unity Player 或 Godot 导出包成立的编译条件。</summary>
        private static bool IsToolOnlyExpression(string expression)
        {
            string condition = expression.Substring(expression.IndexOf(' ') + 1)
                .Replace(" ", string.Empty);
            if (condition.IndexOf("||", System.StringComparison.Ordinal) >= 0)
            {
                return condition == "UNITY_EDITOR||(GODOT&&TOOLS)"
                       || condition == "UNITY_EDITOR||(GODOT&&TOOLS)||YOKIFRAME_TOOLING";
            }

            return condition == "UNITY_EDITOR"
                   || condition.StartsWith("UNITY_EDITOR&&", System.StringComparison.Ordinal)
                   || condition == "UNITY_EDITOR_WIN"
                   || condition == "UNITY_EDITOR_OSX"
                   || condition == "UNITY_EDITOR_LINUX"
                   || condition == "GODOT&&TOOLS"
                   || condition.StartsWith("(GODOT&&TOOLS)&&", System.StringComparison.Ordinal)
                   || condition == "YOKIFRAME_TOOLING"
                   || condition.StartsWith("YOKIFRAME_TOOLING&&", System.StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断源码是否出现编辑器钩子特征词。
        /// </summary>
        /// <param name="sourcePath">源码路径。</param>
        /// <returns>出现特征词时返回 true。</returns>
        private static bool HasEditorHookSignal(string sourcePath)
        {
            return HasCallTokenInFile(sourcePath, "EditorHook")
                || HasCallTokenInFile(sourcePath, "EditorMonitor")
                || HasCallTokenInFile(sourcePath, "CallerFilePath")
                || HasCallTokenInFile(sourcePath, "CallerLineNumber")
                || HasCallTokenInFile(sourcePath, "CallerMemberName");
        }

        /// <summary>
        /// 判断预处理行没有把 Godot 运行时或裸 TOOLS 放进编辑器条件。
        /// </summary>
        /// <param name="sourcePath">源码路径。</param>
        private static void AssertPreprocessorExcludesRuntimeGodot(string sourcePath)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (!line.StartsWith("#if ", System.StringComparison.Ordinal)
                    && !line.StartsWith("#elif ", System.StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.IsFalse(HasCallToken(line, "UNITY_EDITOR || GODOT"), NormalizePath(sourcePath) + ":" + (index + 1));
                Assert.IsFalse(HasCallToken(line, "UNITY_EDITOR || TOOLS"), NormalizePath(sourcePath) + ":" + (index + 1));
                Assert.IsFalse(HasBareGodotRuntimeOr(line), NormalizePath(sourcePath) + ":" + (index + 1));
            }
        }

        /// <summary>
        /// 判断文件中是否有一行去除空白后与给定文本相同。
        /// </summary>
        /// <param name="sourcePath">源码路径。</param>
        /// <param name="expected">期望的整行文本。</param>
        /// <returns>存在匹配行时返回 true。</returns>
        private static bool HasExactTrimmedLine(string sourcePath, string expected)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            for (int index = 0; index < lines.Length; index++)
            {
                if (string.Equals(lines[index].Trim(), expected, System.StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断文件中是否出现左侧有边界的调用或类型记号。
        /// </summary>
        /// <param name="sourcePath">源码路径。</param>
        /// <param name="token">记号。</param>
        /// <returns>出现记号时返回 true。</returns>
        private static bool HasCallTokenInFile(string sourcePath, string token)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            for (int index = 0; index < lines.Length; index++)
            {
                if (HasCallToken(lines[index], token))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断一行里是否出现左侧不属于标识符的记号。
        /// </summary>
        /// <param name="line">源码行。</param>
        /// <param name="token">记号。</param>
        /// <returns>出现记号时返回 true。</returns>
        private static bool HasCallToken(string line, string token)
        {
            for (int index = 0; index <= line.Length - token.Length; index++)
            {
                if (string.CompareOrdinal(line, index, token, 0, token.Length) != 0)
                {
                    continue;
                }

                if (index == 0 || IsStructureIdentifierChar(line[index - 1]) == false)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断预处理行是否使用裸的 Godot 运行时或条件，而不是 Godot tools。
        /// </summary>
        /// <param name="line">已去除空白的预处理行。</param>
        /// <returns>出现裸 Godot 运行时条件时返回 true。</returns>
        private static bool HasBareGodotRuntimeOr(string line)
        {
            const string token = "|| GODOT";
            for (int index = 0; index <= line.Length - token.Length; index++)
            {
                if (string.CompareOrdinal(line, index, token, 0, token.Length) != 0)
                {
                    continue;
                }

                int end = index + token.Length;
                if (end < line.Length && line[end] == '(')
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// 判断字符是否属于 C# 标识符。
        /// </summary>
        /// <param name="value">待判断字符。</param>
        /// <returns>可作为标识符的一部分时返回 true。</returns>
        private static bool IsStructureIdentifierChar(char value)
        {
            return value == '_'
                || (value >= '0' && value <= '9')
                || (value >= 'A' && value <= 'Z')
                || (value >= 'a' && value <= 'z');
        }

    }
}
