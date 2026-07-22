#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 通过真实 Standalone Player 构建验证业务 Runtime 与 Editor/Workbench 工具代码的编译边界。
    /// </summary>
    public sealed partial class YokiFramePlayerBuildBoundaryTests
    {
        private const string OUTPUT_RELATIVE_PATH = ".yokiframe/test-runs/player-boundary";
        private const string EXECUTABLE_NAME = "YokiFramePlayerBoundary.exe";
        private const string EVIDENCE_FILE_NAME = "player-boundary-evidence.txt";

        /// <summary>
        /// 验证 Workbench smoke/stress 源码整文件限制为 Editor 编译边界，避免它们进入 Player。
        /// </summary>
        [Test]
        public void ProjectSmokeHarnessSourcesAreEditorOnly()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            for (var index = 0; index < sSmokeSourceRelativePaths.Length; index++)
            {
                string sourcePath = Path.Combine(projectRoot, sSmokeSourceRelativePaths[index]);
                Assert.IsTrue(File.Exists(sourcePath), "缺少 Workbench smoke/stress 源码: " + sourcePath);
                Assert.IsTrue(
                    File.ReadAllText(sourcePath).StartsWith("#if UNITY_EDITOR", StringComparison.Ordinal),
                    "Smoke/stress 源码必须整文件由 UNITY_EDITOR 包裹: " + sourcePath);
            }

        }

        /// <summary>
        /// 构建未启用 Editor 宏且关闭 managed stripping 的 Windows Player，并扫描真实 Managed DLL 与 Resources 数据。
        /// </summary>
        [Test]
        [Explicit("该测试会执行完整 Standalone Player 构建，只在发布边界验证时按完整测试名运行。")]
        public void StandaloneWindowsPlayerExcludesWorkbenchTooling()
        {
            Assert.AreEqual(RuntimePlatform.WindowsEditor, Application.platform, "该守卫需要 Windows Unity Editor。");
            Assert.IsFalse(EditorApplication.isCompiling, "Player 边界构建开始时 Unity 仍在编译脚本。");
            Assert.IsFalse(EditorApplication.isUpdating, "Player 边界构建开始时 Unity 仍在导入资源。");
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputRoot = PrepareOutputRoot(projectRoot);
            BuildReport report = BuildStandalonePlayer(outputRoot);
            AssertBuildSucceeded(report);
            string[] assemblyPaths = FindFirstPartyManagedAssemblies(outputRoot);
            AssertRequiredRuntimeAssemblies(assemblyPaths);
            AssertPlayerFsmNameConstructor(assemblyPaths);
            string[] forbiddenTypeNames = CollectToolOnlyTypeNames(projectRoot);
            List<string> typeHits = FindForbiddenTypeHits(assemblyPaths, forbiddenTypeNames);
            List<string> memberHits = FindForbiddenMemberHits(
                assemblyPaths,
                sForbiddenPlayerMemberNames);
            List<string> parameterHits = FindForbiddenParameterHits(
                assemblyPaths,
                sForbiddenPlayerParameterNames);
            List<string> constructorParameterHits = FindForbiddenConstructorParameterHits(
                assemblyPaths,
                sForbiddenPlayerConstructorParameters);
            List<string> qualifiedMemberHits = FindForbiddenQualifiedMemberHits(
                assemblyPaths,
                sForbiddenPlayerQualifiedMemberNames);
            List<string> assemblyReferenceHits = FindForbiddenAssemblyReferenceHits(
                assemblyPaths,
                sForbiddenPlayerAssemblyReferenceFragments);
            List<string> configurationHits = FindEditorConfigurationHits(outputRoot, assemblyPaths);
            WriteEvidence(
                report,
                outputRoot,
                assemblyPaths,
                forbiddenTypeNames,
                typeHits,
                memberHits,
                parameterHits,
                constructorParameterHits,
                qualifiedMemberHits,
                assemblyReferenceHits,
                configurationHits);

            Assert.IsEmpty(typeHits, "Player Managed DLL 仍包含 Editor/Workbench 类型:\n" + string.Join("\n", typeHits));
            Assert.IsEmpty(memberHits, "Player 业务类型仍包含 Workbench 观察成员:\n" + string.Join("\n", memberHits));
            Assert.IsEmpty(parameterHits, "Player 业务方法仍包含 Workbench 观察参数:\n" + string.Join("\n", parameterHits));
            Assert.IsEmpty(constructorParameterHits, "Player 构造器仍包含工具专属参数:\n" + string.Join("\n", constructorParameterHits));
            Assert.IsEmpty(qualifiedMemberHits, "Player partial 类型仍包含 Workbench 观察成员:\n" + string.Join("\n", qualifiedMemberHits));
            Assert.IsEmpty(assemblyReferenceHits, "Player Runtime 仍引用 Editor/Workbench 工具程序集:\n" + string.Join("\n", assemblyReferenceHits));
            Assert.IsEmpty(configurationHits, "Player 仍包含 Editor 项目配置:\n" + string.Join("\n", configurationHits));
        }

        /// <summary>创建并清空受项目根约束的验证输出目录，避免扫描到旧构建。</summary>
        private static string PrepareOutputRoot(string projectRoot)
        {
            string outputRoot = Path.GetFullPath(Path.Combine(projectRoot, OUTPUT_RELATIVE_PATH));
            string containmentRoot = projectRoot.TrimEnd(
                Path.DirectorySeparatorChar,
                Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            Assert.IsTrue(
                outputRoot.StartsWith(containmentRoot, StringComparison.OrdinalIgnoreCase),
                "Player 边界验证输出目录必须位于当前项目内。");

            if (Directory.Exists(outputRoot)) Directory.Delete(outputRoot, true);
            Directory.CreateDirectory(outputRoot);
            return outputRoot;
        }

        /// <summary>确认当前审计配置为 Mono 且关闭裁剪，再执行不改写项目设置的发布构建。</summary>
        private static BuildReport BuildStandalonePlayer(string outputRoot)
        {
            NamedBuildTarget target = NamedBuildTarget.Standalone;
            Assert.AreEqual(
                ScriptingImplementation.Mono2x,
                PlayerSettings.GetScriptingBackend(target),
                "Player 边界审计需要 Standalone 使用 Mono2x，以保留可扫描的 Managed DLL。");
            Assert.AreEqual(
                ManagedStrippingLevel.Disabled,
                PlayerSettings.GetManagedStrippingLevel(target),
                "Player 边界审计需要关闭 managed stripping，避免工具类型缺席来自链接裁剪。");
            BuildPlayerOptions options = new()
            {
                scenes = GetEnabledScenePaths(),
                locationPathName = Path.Combine(outputRoot, EXECUTABLE_NAME),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache
            };
            return BuildPipeline.BuildPlayer(options);
        }

        /// <summary>读取当前启用的正式构建场景，确保守卫验证与项目 Player 构建路径一致。</summary>
        private static string[] GetEnabledScenePaths()
        {
            List<string> scenePaths = new();
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (var index = 0; index < scenes.Length; index++)
            {
                if (scenes[index].enabled) scenePaths.Add(scenes[index].path);
            }

            Assert.Greater(scenePaths.Count, 0, "Player 边界验证至少需要一个启用的 Build Settings 场景。");
            return scenePaths.ToArray();
        }

        /// <summary>确认 Unity BuildPipeline 已真实产出无错误的 Standalone Player。</summary>
        private static void AssertBuildSucceeded(BuildReport report)
        {
            Assert.IsNotNull(report, "BuildPipeline 未返回 Player 构建报告。");
            BuildSummary summary = report.summary;
            Assert.AreEqual(BuildResult.Succeeded, summary.result, "Standalone Player 构建失败: " + summary.result);
            Assert.AreEqual(0, summary.totalErrors, "Standalone Player 构建包含错误。");
            Assert.IsTrue(File.Exists(summary.outputPath), "Standalone Player 主程序不存在: " + summary.outputPath);
            AssertNoStaleScriptBuildWarning(report);
        }

        /// <summary>拒绝 Unity 在脚本未完成编译时生成的 Player，避免扫描旧程序集形成假证据。</summary>
        private static void AssertNoStaleScriptBuildWarning(BuildReport report)
        {
            BuildStep[] steps = report.steps;
            for (var stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                BuildStepMessage[] messages = steps[stepIndex].messages;
                for (var messageIndex = 0; messageIndex < messages.Length; messageIndex++)
                {
                    BuildStepMessage message = messages[messageIndex];
                    if (message.type != LogType.Warning
                        || message.content.IndexOf("uncompiled code changes", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    Assert.Fail("Player 构建使用了未完成编译的脚本状态: " + message.content);
                }
            }
        }

        /// <summary>用业务 Runtime 正向类型证明程序集未被整体裁掉，并确认 Editor/Test 程序集缺席。</summary>
        private static void AssertRequiredRuntimeAssemblies(string[] assemblyPaths)
        {
            AssertMetadataTypeExists(FindAssembly(assemblyPaths, "YokiFrame.dll"), "EventKit");
            AssertMetadataTypeExists(FindAssembly(assemblyPaths, "YokiFrame.ActionKit.dll"), "ActionKit");
            AssertMetadataTypeExists(FindAssembly(assemblyPaths, "YokiFrame.AudioKit.dll"), "AudioKit");
            AssertMetadataTypeExists(FindAssembly(assemblyPaths, "YokiFrame.SaveKit.dll"), "SaveKit");
            AssertMetadataTypeExists(FindAssembly(assemblyPaths, "YokiFrame.SpatialKit.dll"), "SpatialKit");
            string uiKitAssembly = FindAssembly(assemblyPaths, "YokiFrame.UIKit.Unity.dll");
            AssertMetadataTypeExists(uiKitAssembly, "UIKit");
            AssertMetadataTypeExists(uiKitAssembly, "Bind");
            AssertMetadataTypeExists(uiKitAssembly, "AbstractBind");
            AssertMetadataTypeExists(uiKitAssembly, "UIElement");
            AssertMetadataTypeExists(uiKitAssembly, "UIComponent");
            AssertMetadataTypeExists(FindAssembly(assemblyPaths, "YokiFrame.Unity.Runtime.dll"), "UnityLogKitRuntimeInstaller");
            for (var index = 0; index < assemblyPaths.Length; index++)
            {
                string fileName = Path.GetFileName(assemblyPaths[index]);
                Assert.IsFalse(fileName.Contains(".Editor"), "Player 禁止包含 Editor 程序集: " + fileName);
                Assert.IsFalse(fileName.Contains(".Tests"), "Player 禁止包含 Tests 程序集: " + fileName);
            }
        }

        /// <summary>按文件名从 Player Managed 程序集中定位唯一目标程序集。</summary>
        private static string FindAssembly(string[] assemblyPaths, string expectedFileName)
        {
            for (var index = 0; index < assemblyPaths.Length; index++)
            {
                if (string.Equals(Path.GetFileName(assemblyPaths[index]), expectedFileName, StringComparison.OrdinalIgnoreCase))
                {
                    return assemblyPaths[index];
                }
            }

            Assert.Fail("Player 缺少业务 Runtime 程序集: " + expectedFileName);
            return string.Empty;
        }

        /// <summary>从 Runtime 与 Unity Editor Adapter 源码的工具条件分支提取禁止进入 Player 的类型名。</summary>
        private static string[] CollectToolOnlyTypeNames(string projectRoot)
        {
            HashSet<string> toolTypeNames = new(StringComparer.Ordinal);
            HashSet<string> runtimeTypeNames = new(StringComparer.Ordinal);
            CollectConditionalTypeNamesUnder(
                Path.Combine(projectRoot, "Assets", "YokiFrame", "Core", "Runtime"),
                toolTypeNames,
                runtimeTypeNames);
            CollectConditionalTypeNamesUnder(
                Path.Combine(projectRoot, "Assets", "YokiFrame", "Core", "Editor"),
                toolTypeNames,
                runtimeTypeNames);
            CollectConditionalTypeNamesUnder(
                Path.Combine(projectRoot, "Assets", "YokiFrame", "Tools"),
                toolTypeNames,
                runtimeTypeNames);
            CollectConditionalTypeNamesUnder(
                Path.Combine(projectRoot, "Assets", "YokiFrame", "Core", "Adapters", "Unity", "Editor"),
                toolTypeNames,
                runtimeTypeNames);
            CollectConditionalTypeNamesUnder(
                Path.Combine(projectRoot, "Assets", "Scripts"),
                toolTypeNames,
                runtimeTypeNames);
            toolTypeNames.ExceptWith(runtimeTypeNames);
            AssertRequiredToolTypesDiscovered(toolTypeNames);
            string[] result = new string[toolTypeNames.Count];
            toolTypeNames.CopyTo(result);
            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        /// <summary>扫描目录内源码的预处理分支，把类型声明分别归入工具专属或 Player 可达集合。</summary>
        private static void CollectConditionalTypeNamesUnder(
            string sourceRoot,
            HashSet<string> toolTypeNames,
            HashSet<string> runtimeTypeNames)
        {
            if (!Directory.Exists(sourceRoot)) return;
            string[] sourcePaths = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            for (var index = 0; index < sourcePaths.Length; index++)
            {
                CollectConditionalTypeNamesFromSource(sourcePaths[index], toolTypeNames, runtimeTypeNames);
            }
        }

        /// <summary>沿单个源码文件的预处理条件栈提取一行式类型声明。</summary>
        private static void CollectConditionalTypeNamesFromSource(
            string sourcePath,
            HashSet<string> toolTypeNames,
            HashSet<string> runtimeTypeNames)
        {
            Stack<bool> parentConditions = new();
            bool toolOnly = false;
            string[] lines = File.ReadAllLines(sourcePath);
            for (var index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (TryUpdateToolCondition(line, parentConditions, ref toolOnly)) continue;
                Match match = sTypeDeclarationPattern.Match(lines[index]);
                if (!match.Success) continue;
                (toolOnly ? toolTypeNames : runtimeTypeNames).Add(match.Groups["name"].Value);
            }
        }

        /// <summary>更新预处理条件栈；返回 true 表示当前行是条件指令而非类型声明。</summary>
        private static bool TryUpdateToolCondition(string line, Stack<bool> parents, ref bool toolOnly)
        {
            if (line.StartsWith("#if ", StringComparison.Ordinal))
            {
                parents.Push(toolOnly);
                toolOnly = toolOnly || IsToolOnlyExpression(line);
            }
            else if (line.StartsWith("#elif ", StringComparison.Ordinal))
            {
                toolOnly = parents.Peek() || IsToolOnlyExpression(line);
            }
            else if (line == "#else")
            {
                toolOnly = parents.Peek();
            }
            else if (line == "#endif")
            {
                toolOnly = parents.Pop();
            }
            else
            {
                return false;
            }

            return true;
        }

        /// <summary>识别只可能在 Unity Editor、Godot Tools 或工具链共享编译中成立的条件。</summary>
        private static bool IsToolOnlyExpression(string expression)
        {
            if (expression.Contains("UNITY_ENABLE_CHECKS") || expression.Contains("DEVELOPMENT_BUILD")) return false;
            string condition = expression.Substring(expression.IndexOf(' ') + 1)
                .Replace(" ", string.Empty);
            if (condition.Contains("||"))
            {
                return condition == "UNITY_EDITOR||(GODOT&&TOOLS)"
                       || condition == "UNITY_EDITOR||(GODOT&&TOOLS)||YOKIFRAME_TOOLING";
            }

            return condition == "UNITY_EDITOR"
                   || condition.StartsWith("UNITY_EDITOR&&", StringComparison.Ordinal)
                   || condition == "UNITY_EDITOR_WIN"
                   || condition == "UNITY_EDITOR_OSX"
                   || condition == "UNITY_EDITOR_LINUX"
                   || condition == "GODOT&&TOOLS"
                   || condition.StartsWith("(GODOT&&TOOLS)&&", StringComparison.Ordinal)
                   || condition == "YOKIFRAME_TOOLING"
                   || condition.StartsWith("YOKIFRAME_TOOLING&&", StringComparison.Ordinal);
        }

        /// <summary>确认源码提取覆盖各关键类别，避免条件解析退化为低覆盖率假通过。</summary>
        private static void AssertRequiredToolTypesDiscovered(HashSet<string> toolTypeNames)
        {
            for (var index = 0; index < sRequiredToolTypeNames.Length; index++)
            {
                Assert.IsTrue(
                    toolTypeNames.Contains(sRequiredToolTypeNames[index]),
                    "Player 边界守卫未识别关键工具类型: " + sRequiredToolTypeNames[index]);
            }

            Assert.Greater(toolTypeNames.Count, 100, "Player 边界守卫提取的工具类型数量异常偏低。");
        }

        /// <summary>扫描 Player 一方程序集和 Resources 数据，返回残留的 Editor 配置字段或文件。</summary>
        private static List<string> FindEditorConfigurationHits(string outputRoot, string[] assemblyPaths)
        {
            List<string> hits = new();
            string[] editorSettingsFiles = Directory.GetFiles(outputRoot, "editor-settings.json", SearchOption.AllDirectories);
            for (var index = 0; index < editorSettingsFiles.Length; index++) hits.Add(editorSettingsFiles[index]);

            HashSet<string> dataPaths = new(assemblyPaths, StringComparer.OrdinalIgnoreCase);
            AddMatchingFiles(dataPaths, outputRoot, "*.assets");
            AddMatchingFiles(dataPaths, outputRoot, "*.resource");
            AddMatchingFiles(dataPaths, outputRoot, "*.json");
            foreach (string dataPath in dataPaths)
            {
                byte[] bytes = File.ReadAllBytes(dataPath);
                AddEditorConfigurationHits(hits, dataPath, bytes);
            }

            return hits;
        }

        /// <summary>把符合名称模式的数据文件加入去重扫描集合。</summary>
        private static void AddMatchingFiles(HashSet<string> paths, string outputRoot, string searchPattern)
        {
            string[] matchingPaths = Directory.GetFiles(outputRoot, searchPattern, SearchOption.AllDirectories);
            for (var index = 0; index < matchingPaths.Length; index++) paths.Add(matchingPaths[index]);
        }

        /// <summary>同时按 UTF-8 与 UTF-16LE 检查一个数据文件中的 Editor 配置文本。</summary>
        private static void AddEditorConfigurationHits(List<string> hits, string dataPath, byte[] bytes)
        {
            for (var index = 0; index < sEditorConfigurationTokens.Length; index++)
            {
                string token = sEditorConfigurationTokens[index];
                if (!ContainsSequence(bytes, Encoding.UTF8.GetBytes(token))
                    && !ContainsSequence(bytes, Encoding.Unicode.GetBytes(token))) continue;
                hits.Add(Path.GetFileName(dataPath) + ":" + token);
            }
        }

        /// <summary>写入可独立审计的 Player 构建与扫描摘要，保留在验证产物目录。</summary>
        private static void WriteEvidence(
            BuildReport report,
            string outputRoot,
            string[] assemblyPaths,
            string[] forbiddenTypeNames,
            List<string> typeHits,
            List<string> memberHits,
            List<string> parameterHits,
            List<string> constructorParameterHits,
            List<string> qualifiedMemberHits,
            List<string> assemblyReferenceHits,
            List<string> configurationHits)
        {
            BuildSummary summary = report.summary;
            List<string> lines = new()
            {
                "unityVersion=" + Application.unityVersion,
                "target=StandaloneWindows64",
                "scriptingBackend=Mono2x",
                "managedStripping=Disabled",
                "cleanBuildCache=True",
                "buildResult=" + summary.result,
                "buildErrors=" + summary.totalErrors,
                "buildWarnings=" + summary.totalWarnings,
                "outputPath=" + summary.outputPath,
                "positiveType=YokiFrame.dll:EventKit",
                "positiveType=YokiFrame.ActionKit.dll:ActionKit",
                "positiveType=YokiFrame.AudioKit.dll:AudioKit",
                "positiveType=YokiFrame.UIKit.Unity.dll:UIKit",
                "positiveType=YokiFrame.UIKit.Unity.dll:Bind",
                "positiveType=YokiFrame.UIKit.Unity.dll:AbstractBind",
                "positiveType=YokiFrame.UIKit.Unity.dll:UIElement",
                "positiveType=YokiFrame.UIKit.Unity.dll:UIComponent",
                "positiveType=YokiFrame.Unity.Runtime.dll:UnityLogKitRuntimeInstaller",
                "forbiddenTypeCount=" + forbiddenTypeNames.Length,
                "forbiddenTypeHits=" + typeHits.Count,
                "forbiddenMemberHits=" + memberHits.Count,
                "forbiddenParameterHits=" + parameterHits.Count,
                "forbiddenConstructorParameterHits=" + constructorParameterHits.Count,
                "forbiddenQualifiedMemberHits=" + qualifiedMemberHits.Count,
                "forbiddenAssemblyReferenceHits=" + assemblyReferenceHits.Count,
                "editorConfigurationHits=" + configurationHits.Count
            };
            AppendEvidenceAssemblies(lines, outputRoot, assemblyPaths);
            AppendEvidenceWarnings(lines, report);
            File.WriteAllLines(Path.Combine(outputRoot, EVIDENCE_FILE_NAME), lines, new UTF8Encoding(false));
        }

        /// <summary>把一方 Managed 程序集相对路径追加到证据摘要。</summary>
        private static void AppendEvidenceAssemblies(List<string> lines, string outputRoot, string[] assemblyPaths)
        {
            string normalizedRoot = outputRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (var index = 0; index < assemblyPaths.Length; index++)
            {
                string relativePath = assemblyPaths[index].Substring(normalizedRoot.Length)
                    .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                lines.Add("managedAssembly=" + relativePath.Replace('\\', '/'));
            }
        }

        /// <summary>把 BuildReport 中去重后的警告正文追加到证据，便于区分边界失败与既有项目告警。</summary>
        private static void AppendEvidenceWarnings(List<string> lines, BuildReport report)
        {
            HashSet<string> warnings = new(StringComparer.Ordinal);
            BuildStep[] steps = report.steps;
            for (var stepIndex = 0; stepIndex < steps.Length; stepIndex++)
            {
                BuildStepMessage[] messages = steps[stepIndex].messages;
                for (var messageIndex = 0; messageIndex < messages.Length; messageIndex++)
                {
                    if (messages[messageIndex].type != LogType.Warning) continue;
                    string content = messages[messageIndex].content
                        .Replace('\r', ' ')
                        .Replace('\n', ' ')
                        .Trim();
                    if (!string.IsNullOrEmpty(content)) warnings.Add(content);
                }
            }

            foreach (string warning in warnings) lines.Add("warning=" + warning);
        }
    }
}
#endif
