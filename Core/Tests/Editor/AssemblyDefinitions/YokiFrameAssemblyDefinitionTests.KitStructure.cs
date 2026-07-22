using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        /// <summary>
        /// 验证 Architecture Runtime 内部按功能目录收纳，避免在 Architecture 根层平铺源码。
        /// </summary>
        [Test]
        public void ArchitectureRuntimeSourcesUseFunctionalFolders()
        {
            string kitRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "Architecture");
            string runtimeRoot = kitRoot;
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Contracts")), "Architecture 缺少 Contracts 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Facade")), "Architecture 缺少 Facade 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Diagnostics")), "Architecture 缺少 Diagnostics 功能目录。");

            string[] directRuntimeSources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(0, directRuntimeSources.Length, "Architecture Runtime 根层不应直接平铺源码文件。");
        }

        /// <summary>
        /// 验证 Architecture Runtime 中不能混入未受保护的编辑器功能；若未来在 Runtime 内埋监控钩子，必须用编辑器上下文宏包裹。
        /// </summary>
        [Test]
        public void ArchitectureRuntimeEditorHooksUseEditorContextDefine()
        {
            string runtimeRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "Architecture");
            Assert.IsTrue(Directory.Exists(runtimeRoot), "Architecture 运行时代码必须位于 Core/Runtime/Architecture。");
            string[] sourcePaths = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);

            foreach (string sourcePath in sourcePaths)
            {
                string source = File.ReadAllText(sourcePath);
                string fileName = Path.GetFileNameWithoutExtension(sourcePath);
                Assert.IsFalse(fileName.Contains("Editor"), "Architecture 编辑器源码不应位于 Runtime 树: " + NormalizePath(sourcePath));
                Assert.IsFalse(source.Contains("UNITY_EDITOR || GODOT"), "Architecture Runtime 不能用 GODOT 运行时宏开启编辑器 hook: " + NormalizePath(sourcePath));
                Assert.IsFalse(source.Contains("UNITY_EDITOR || TOOLS"), "Architecture Runtime 不能脱离 GODOT 约束单独使用 TOOLS 宏: " + NormalizePath(sourcePath));

                if (!ContainsEditorHookSignal(source))
                {
                    continue;
                }

                Assert.IsTrue(source.Contains(EDITOR_CONTEXT_DEFINE), "Architecture Runtime 中的编辑器/工具钩子必须同时覆盖 Unity Editor 与 Godot tools: " + NormalizePath(sourcePath));
            }
        }

        /// <summary>
        /// 验证 EventKit Runtime 内部按功能目录收纳，避免在 Runtime 根层平铺大量源码。
        /// </summary>
        [Test]
        public void EventKitRuntimeSourcesUseFunctionalFolders()
        {
            string runtimeRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "EventKit");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Buses")), "EventKit Runtime 缺少 Buses 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Events")), "EventKit Runtime 缺少 Events 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Lifetime")), "EventKit Runtime 缺少 Lifetime 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Monitoring")), "EventKit Runtime 缺少最小观察端口目录。");
            Assert.IsFalse(Directory.Exists(Path.Combine(runtimeRoot, "Diagnostics")), "EventKit 完整诊断必须位于 Core/Editor/EventKit。");

            string[] directSources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(0, directSources.Length, "EventKit Runtime 根层不应直接平铺源码文件。");
        }

        /// <summary>
        /// 验证 PoolKit Runtime 内部按功能目录收纳，避免在 PoolKit 根层平铺大量源码。
        /// </summary>
        [Test]
        public void PoolKitRuntimeSourcesUseFunctionalFolders()
        {
            string kitRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "PoolKit");
            string runtimeRoot = kitRoot;
            string editorRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Editor", "PoolKit");

            Assert.IsTrue(Directory.Exists(editorRoot), "PoolKit Editor 能力必须位于共享 Core/Editor 边界。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Contracts")), "PoolKit Runtime 缺少 Contracts 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Pools")), "PoolKit Runtime 缺少 Pools 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Infrastructure")), "PoolKit Runtime 缺少 Infrastructure 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Diagnostics")), "PoolKit Runtime 缺少 Diagnostics 功能目录。");
            Assert.IsTrue(File.Exists(Path.Combine(runtimeRoot, "Pools", "PoolKit.cs")), "PoolKit Runtime 缺少统一门面。");
            Assert.IsTrue(File.Exists(Path.Combine(runtimeRoot, "Pools", "ObjectPool.cs")), "PoolKit Runtime 缺少统一对象池实现。");
            Assert.IsTrue(File.Exists(Path.Combine(runtimeRoot, "Pools", "SharedPoolRegistry.cs")), "PoolKit Runtime 缺少共享池注册表。");
            Assert.IsTrue(File.Exists(Path.Combine(runtimeRoot, "Contracts", "PoolOptions.cs")), "PoolKit Runtime 缺少统一容量配置。");
            Assert.IsFalse(Directory.Exists(Path.Combine(runtimeRoot, "Factories")), "PoolKit 已直接使用强类型 factory 委托，不能恢复冗余 Factories 目录。");
            Assert.IsFalse(Directory.Exists(Path.Combine(kitRoot, "CollectionPools")), "PoolKit 不再暴露集合池 API，不能保留 CollectionPools 目录。");
            Assert.IsFalse(Directory.Exists(Path.Combine(runtimeRoot, "CollectionPools")), "PoolKit Runtime 不再暴露集合池 API，不能保留 CollectionPools 目录。");

            string[] directRuntimeSources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(0, directRuntimeSources.Length, "PoolKit Runtime 根层不应直接平铺源码文件。");
        }

        /// <summary>
        /// 验证 EventKit 完整观察实现位于共享 Editor，Runtime 只保留总线直接调用的最小 Hook。
        /// </summary>
        [Test]
        public void EventKitDiagnosticsUseSharedEditorBoundary()
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

                Assert.IsFalse(fileName.Contains("Editor"), "EventKit 重编辑器实现不应位于 Runtime 树: " + NormalizePath(sourcePath));
                Assert.IsFalse(fileName.Contains("Diagnostic"), "EventKit 完整诊断实现不应位于 Runtime 树: " + NormalizePath(sourcePath));
            }
        }

        /// <summary>
        /// 验证 EventKit 共享 Editor 源码整体由编辑器上下文宏包裹，避免 Player 打包时出现诊断类型，同时允许 Godot tools 场景使用。
        /// </summary>
        [Test]
        public void EventKitSharedEditorSourcesAreGuardedByEditorContextDefines()
        {
            string editorRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Editor", "EventKit");
            string[] sourcePaths = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories);

            foreach (string sourcePath in sourcePaths)
            {
                AssertWholeFileGuard(sourcePath, EDITOR_CONTEXT_DEFINE);
                string source = File.ReadAllText(sourcePath);
                Assert.IsFalse(source.Contains("|| GODOT"), "EventKit 编辑器源码不能把 Godot Runtime 宏混入通用 Editor 通道: " + NormalizePath(sourcePath));
            }
        }

        /// <summary>
        /// 验证 EventKit Runtime 不再把编辑器 hook 绑定到 GODOT 宏，并使用 Unity Editor / Godot tools 共享的编辑器上下文宏。
        /// </summary>
        [Test]
        public void EventKitRuntimeUsesEditorContextDefineForEditorHooks()
        {
            string runtimeRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "EventKit");
            string[] sourcePaths = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);

            foreach (string sourcePath in sourcePaths)
            {
                string source = File.ReadAllText(sourcePath);
                Assert.IsFalse(source.Contains("UNITY_EDITOR || GODOT"), "EventKit Runtime 不能用 GODOT 运行时宏开启编辑器 hook: " + NormalizePath(sourcePath));
                Assert.IsFalse(source.Contains("UNITY_EDITOR || TOOLS"), "EventKit Runtime 不能脱离 GODOT 约束单独使用 TOOLS 宏: " + NormalizePath(sourcePath));
                Assert.IsFalse(source.Contains("|| GODOT"), "EventKit Runtime 的编辑器条件不能混入 Godot Runtime 宏: " + NormalizePath(sourcePath));
                if (!source.Contains("EasyEventEditorHook"))
                {
                    continue;
                }

                Assert.IsTrue(source.Contains(EDITOR_CONTEXT_DEFINE), "EventKit Runtime 的编辑器 hook 必须同时覆盖 Unity Editor 与 Godot tools: " + NormalizePath(sourcePath));
            }
        }

        /// <summary>
        /// 验证 Unity Runtime Adapter 源码整体由单一 Unity 环境宏包裹，避免非 Unity 宿主误编译 Unity API。
        /// </summary>
        [Test]
        public void UnityRuntimeAdapterSourcesUseSingleUnityDefine()
        {
            string unityRuntimeRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Adapters", "Unity", "Runtime");
            string[] sourcePaths = Directory.GetFiles(unityRuntimeRoot, "*.cs", SearchOption.AllDirectories);

            foreach (string sourcePath in sourcePaths)
            {
                AssertWholeFileGuard(sourcePath, UNITY_ADAPTER_DEFINE);
                string firstLine = FirstNonEmptyLine(sourcePath);
                Assert.IsFalse(firstLine.Contains("||"), "Unity Runtime Adapter 只允许一个 Unity 环境宏: " + NormalizePath(sourcePath));
            }
        }

        /// <summary>
        /// 验证 LogKit 属于 Core Runtime 基础设施，避免所有 Tool 为了打日志而依赖另一个 Tool。
        /// </summary>
        [Test]
        public void LogKitLivesInCoreRuntimeInsteadOfToolLayer()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string coreLogKitRoot = Path.Combine(packageRoot, "Core", "Runtime", "LogKit");
            string toolLogKitRoot = Path.Combine(packageRoot, "Tools", "LogKit");

            Assert.IsTrue(Directory.Exists(coreLogKitRoot), "LogKit 必须位于 Core/Runtime/LogKit，供所有 Tool 通过 Core 使用。");
            Assert.IsTrue(File.Exists(Path.Combine(coreLogKitRoot, "Facade", "LogKit.cs")), "Core LogKit 缺少统一日志门面。");
            Assert.AreEqual(0, Directory.GetFiles(coreLogKitRoot, "KitLogger.cs", SearchOption.AllDirectories).Length, "LogKit 不再保留旧版 KitLogger 兼容入口。");
            Assert.IsFalse(Directory.Exists(toolLogKitRoot), "LogKit 不应放在 Tools/LogKit；Tool 层不能依赖另一个 Tool。");
        }

        /// <summary>验证 AudioKit Unity Adapter 复用 Core PoolKit，禁止恢复私有 AudioSource 栈池。</summary>
        [Test]
        public void AudioKitUnityAdapterUsesCorePoolKitForAudioSources()
        {
            string adapterRoot = Path.Combine(
                Application.dataPath,
                "YokiFrame", "Tools", "AudioKit", "Adapters", "Unity", "Runtime");
            string backendSource = File.ReadAllText(Path.Combine(adapterRoot, "UnityAudioKitBackend.cs"));
            string utilitySource = File.ReadAllText(Path.Combine(adapterRoot, "UnityAudioKitBackend.Utility.cs"));

            Assert.IsTrue(
                backendSource.Contains("ObjectPool<PooledAudioSource>"),
                "AudioKit Unity 后端必须使用 Core PoolKit 管理 AudioSource 租约。");
            Assert.IsTrue(
                backendSource.Contains("PoolKit.Create("),
                "AudioKit Unity 后端必须通过 PoolKit 门面创建对象池。");
            Assert.IsFalse(
                backendSource.Contains("Stack<AudioSource>") || utilitySource.Contains("Stack<AudioSource>"),
                "AudioKit Unity 后端禁止恢复私有 Stack<AudioSource> 对象池。");
        }

        /// <summary>验证 AudioKit Godot Adapter 复用 Core PoolKit，禁止维护私有 Player 栈池。</summary>
        [Test]
        public void AudioKitGodotAdapterUsesCorePoolKitForAudioPlayers()
        {
            string adapterRoot = Path.Combine(
                Application.dataPath,
                "YokiFrame", "Tools", "AudioKit", "Adapters", "Godot", "Runtime");
            string backendSource = File.ReadAllText(Path.Combine(adapterRoot, "GodotAudioKitBackend.cs"));
            string utilitySource = File.ReadAllText(Path.Combine(adapterRoot, "GodotAudioKitBackend.Utility.cs"));

            Assert.IsTrue(
                backendSource.Contains("ObjectPool<PooledAudioPlayer2D>")
                && backendSource.Contains("ObjectPool<PooledAudioPlayer3D>"),
                "AudioKit Godot 后端必须使用 Core PoolKit 管理二维和三维 Player 租约。");
            Assert.IsTrue(
                backendSource.Contains("PoolKit.Create("),
                "AudioKit Godot 后端必须通过 PoolKit 门面创建 Player 池。");
            Assert.IsFalse(
                backendSource.Contains("Stack<AudioStreamPlayer>")
                || utilitySource.Contains("Stack<AudioStreamPlayer>")
                || backendSource.Contains("Stack<AudioStreamPlayer3D>")
                || utilitySource.Contains("Stack<AudioStreamPlayer3D>"),
                "AudioKit Godot 后端禁止恢复私有 AudioStreamPlayer 栈池。");
        }

        /// <summary>
        /// 验证 LogKit 源码先按 Runtime / Editor 分层，再按门面、条目、设置和诊断职责收纳。
        /// </summary>
        [Test]
        public void LogKitRuntimeSourcesUseFunctionalFolders()
        {
            string kitRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "LogKit");
            string runtimeRoot = kitRoot;
            string editorRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Editor", "LogKit");

            Assert.IsTrue(Directory.Exists(editorRoot), "LogKit Editor 能力必须位于共享 Core/Editor 边界。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Facade")), "LogKit Runtime 缺少 Facade 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Entries")), "LogKit Runtime 缺少 Entries 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Settings")), "LogKit Runtime 缺少 Settings 功能目录。");
            Assert.IsTrue(Directory.Exists(Path.Combine(runtimeRoot, "Diagnostics")), "LogKit Runtime 缺少 Diagnostics 功能目录。");
            Assert.IsTrue(File.Exists(Path.Combine(runtimeRoot, "Facade", "LogKit.cs")), "LogKit 统一门面必须位于 Runtime/Facade。");
            Assert.IsTrue(File.Exists(Path.Combine(runtimeRoot, "Entries", "LogKitEntry.cs")), "LogKit 日志条目必须位于 Runtime/Entries。");
            Assert.IsTrue(File.Exists(Path.Combine(runtimeRoot, "Settings", "LogKitSettings.cs")), "LogKit 设置必须位于 Runtime/Settings。");
            Assert.IsTrue(File.Exists(Path.Combine(runtimeRoot, "Diagnostics", "LogKitStats.cs")), "LogKit 诊断统计必须位于 Runtime/Diagnostics。");

            string[] directRuntimeSources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(0, directRuntimeSources.Length, "LogKit Runtime 根层不应直接平铺源码文件。");
        }

        /// <summary>
        /// 验证 LogKit 开发期日志入口使用 Unity Editor / Unity checks / Godot tools 的共享宏边界。
        /// </summary>
        [Test]
        public void LogKitDebugEntriesUseEditorOrToolsGuard()
        {
            string sourcePath = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "LogKit", "Facade", "LogKit.cs");
            Assert.IsTrue(File.Exists(sourcePath), "LogKit 调试入口必须位于 Core/Runtime/LogKit/Facade: " + NormalizePath(sourcePath));
            string source = File.ReadAllText(sourcePath);

            Assert.IsTrue(
                source.Contains("#if UNITY_EDITOR || UNITY_ENABLE_CHECKS || (GODOT && TOOLS)"),
                "LogKit 开发期日志入口必须同时覆盖 Unity Editor、Unity checks 和 Godot tools: " + NormalizePath(sourcePath));
            Assert.IsFalse(
                source.Contains("[System.Diagnostics.Conditional(\"UNITY_EDITOR\")]"),
                "LogKit 开发期日志入口不能只依赖 Unity Editor Conditional，避免 Godot tools 场景失效: " + NormalizePath(sourcePath));
            Assert.IsFalse(
                source.Contains("UNITY_EDITOR || TOOLS"),
                "LogKit 不能脱离 GODOT 约束单独使用 TOOLS 宏: " + NormalizePath(sourcePath));
        }

        /// <summary>
        /// 验证全部 Workbench 诊断、Interaction 和通信协议源码使用整文件工具宏，Player 不编译这些类型。
        /// </summary>
        [Test]
        public void WorkbenchObservationSourcesUseWholeFileToolGuards()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string coreRuntimeRoot = Path.Combine(packageRoot, "Core", "Runtime");
            string coreEditorRoot = Path.Combine(packageRoot, "Core", "Editor");
            HashSet<string> sourcePaths = new(System.StringComparer.OrdinalIgnoreCase);
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "CommandBridge"));
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "Telemetry"));
            AddSourcesUnder(sourcePaths, Path.Combine(coreEditorRoot, "KitInteraction"));
            AddNamedFolderSources(sourcePaths, coreRuntimeRoot, "Diagnostics");
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
                string expected = normalizedPath.Contains("/CommandBridge/")
                                  || normalizedPath.Contains("/Telemetry/")
                    ? "#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING"
                    : EDITOR_CONTEXT_DEFINE;
                AssertWholeFileGuard(sourcePath, expected);
            }
        }

        /// <summary>
        /// 验证插入业务源码的观察调用全部位于 Editor/Tools 条件内，防止 Player 留下空 Hook 或诊断成本。
        /// </summary>
        [Test]
        public void RuntimeObservationCallsStayInsideToolConditions()
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
                    if (!line.Contains(tokens[tokenIndex])) continue;
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
            if (condition.Contains("||"))
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
    }
}
