using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 YokiFrame 包内程序集定义和旧版 GUID 继承关系，避免升级项目时 asmdef 引用丢失。
    /// </summary>
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        private const string CORE_RUNTIME_GUID_REFERENCE = "GUID:224151c835b786a4f8efadaa1799380d";
        private const string CORE_EDITOR_GUID_REFERENCE = "GUID:8e62f2f1c4b54a22a65cf2e98aa12920";
        private const string EVENT_KIT_GUID_REFERENCE = "GUID:09d4ab1f7ad648e1908cc84d94b3cc51";
        private const string ACTION_KIT_GUID_REFERENCE = "GUID:bfd6b57e437b4c940985132bb8b28cf0";
        private const string UNITASK_GUID_REFERENCE = "GUID:f51ebe6a0ceec4240a699833d6309b23";
        private const string CORE_EDITOR_ASMDEF_PATH = "Assets/YokiFrame/Core/Editor/YokiFrame.Editor.asmdef";
        private const string UNITY_EDITOR_ASMDEF_PATH = "Assets/YokiFrame/Core/Adapters/Unity/Editor/YokiFrame.Unity.Editor.asmdef";
        private const string UNITY_RUNTIME_ASMDEF_PATH = "Assets/YokiFrame/Core/Adapters/Unity/Runtime/YokiFrame.Unity.Runtime.asmdef";
        private const string LEGACY_UNITY_EDITOR_ASMDEF_PATH = "Assets/YokiFrame/Core/Editor/YokiFrame.Unity.Editor.asmdef";
        private const string UNITY_RUNTIME_ADAPTER_PATH_FRAGMENT = "/Assets/YokiFrame/Core/Adapters/Unity/Runtime/";
        private const string GODOT_RUNTIME_ADAPTER_PATH_FRAGMENT = "/Assets/YokiFrame/Core/Adapters/Godot/Runtime/";
        private const string UNITY_ADAPTER_DEFINE = "#if UNITY_5_3_OR_NEWER";
        private const string UNITY_EDITOR_ADAPTER_DEFINE = "#if UNITY_EDITOR";
        private const string UNITY_EDITOR_WINDOWS_ADAPTER_DEFINE = "#if UNITY_EDITOR_WIN";
        private const string GODOT_ADAPTER_DEFINE = "#if GODOT";
        private const string EDITOR_CONTEXT_DEFINE = "#if UNITY_EDITOR || (GODOT && TOOLS)";

        /// <summary>
        /// 验证 Core Runtime 继续使用旧版 `YokiFrame` 程序集 GUID，同时保持纯 C# Runtime 边界。
        /// </summary>
        [Test]
        public void CoreRuntimeAssemblyKeepsLegacyYokiFrameGuid()
        {
            AssertAssembly("Assets/YokiFrame/Core/Runtime/YokiFrame.asmdef", "YokiFrame", "224151c835b786a4f8efadaa1799380d", true);
        }

        /// <summary>
        /// 验证纯 Core 主程序集的真实引用表不包含 Unity 或 Godot 宿主程序集。
        /// </summary>
        [Test]
        public void CoreRuntimeAssemblyDoesNotReferenceHostAssemblies()
        {
            var referencedAssemblies = typeof(IEngineLogger).Assembly.GetReferencedAssemblies();
            foreach (var referencedAssembly in referencedAssemblies)
            {
                string name = referencedAssembly.Name ?? string.Empty;
                Assert.IsFalse(name.StartsWith("UnityEngine", System.StringComparison.Ordinal), "Core 主程序集禁止引用 UnityEngine: " + name);
                Assert.IsFalse(name.StartsWith("UnityEditor", System.StringComparison.Ordinal), "Core 主程序集禁止引用 UnityEditor: " + name);
                Assert.IsFalse(name.StartsWith("Godot", System.StringComparison.Ordinal), "Core 主程序集禁止引用 Godot: " + name);
            }
        }

        /// <summary>
        /// 验证 Unity Editor 适配程序集沿用旧版 Editor GUID，并只在 Editor 平台编译。
        /// </summary>
        [Test]
        public void UnityEditorAssemblyKeepsLegacyEditorGuid()
        {
            AssertAssembly(UNITY_EDITOR_ASMDEF_PATH, "YokiFrame.Unity.Editor", "c43cf80ab82b5e04ba14ddf6cd7b676e", false);
            string asmdef = File.ReadAllText(Path.Combine(Application.dataPath, "..", UNITY_EDITOR_ASMDEF_PATH));
            Assert.IsTrue(
                Regex.IsMatch(asmdef, "\\\"includePlatforms\\\"\\s*:\\s*\\[\\s*\\\"Editor\\\"\\s*\\]"),
                "Unity Editor asmdef 必须只包含 Editor 平台，禁止进入 Player。");
        }

        /// <summary>
        /// 验证共享 Editor 能力拥有独立纯 C# 程序集，只依赖 Core 且不会进入 Player。
        /// </summary>
        [Test]
        public void CoreEditorAssemblyIsPortableAndEditorOnly()
        {
            AssertAssembly(CORE_EDITOR_ASMDEF_PATH, "YokiFrame.Editor", "8e62f2f1c4b54a22a65cf2e98aa12920", true);
            string asmdef = File.ReadAllText(Path.Combine(Application.dataPath, "..", CORE_EDITOR_ASMDEF_PATH));
            Assert.IsTrue(asmdef.Contains(CORE_RUNTIME_GUID_REFERENCE), "共享 Editor 程序集必须只向 Core Runtime 建立依赖。");
            Assert.IsFalse(asmdef.Contains("YokiFrame.Unity"), "共享 Editor 程序集不能依赖 Unity Adapter。");
            Assert.IsTrue(
                Regex.IsMatch(asmdef, "\\\"includePlatforms\\\"\\s*:\\s*\\[\\s*\\\"Editor\\\"\\s*\\]"),
                "共享 Editor asmdef 必须只包含 Editor 平台，禁止进入 Player。");
        }

        /// <summary>
        /// 验证 Unity Runtime 适配程序集沿用旧版 Runtime GUID，并作为宿主专属程序集引用 Core。
        /// </summary>
        [Test]
        public void UnityRuntimeAssemblyKeepsLegacyRuntimeGuid()
        {
            AssertAssembly(UNITY_RUNTIME_ASMDEF_PATH, "YokiFrame.Unity.Runtime", "57d76a5674b6f474e97d39484877f63c", false, "YokiFrame.Unity");
            string asmdef = File.ReadAllText(Path.Combine(Application.dataPath, "..", UNITY_RUNTIME_ASMDEF_PATH));
            Assert.IsTrue(asmdef.Contains(CORE_RUNTIME_GUID_REFERENCE), "Unity Runtime Adapter 必须通过 GUID 依赖 Core。");
            Assert.IsFalse(asmdef.Contains(CORE_EDITOR_GUID_REFERENCE), "Unity Runtime Adapter 禁止依赖共享 Editor 程序集。");
            Assert.IsFalse(asmdef.Contains(EVENT_KIT_GUID_REFERENCE), "Unity Runtime Adapter 不应依赖独立 EventKit 程序集，Core Kit 应并入 YokiFrame 主程序集。");
            Assert.IsFalse(asmdef.Contains(ACTION_KIT_GUID_REFERENCE), "Unity Runtime Adapter 必须通过 Core FrameLoop 驱动，禁止反向依赖 ActionKit Tool。");
        }

        /// <summary>
        /// 验证 UniTask 只使用可缺失的程序集名引用，移除可选包后不会残留具体 GUID 硬绑定。
        /// </summary>
        [Test]
        public void UniTaskAssemblyReferencesRemainOptional()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string[] asmdefPaths = Directory.GetFiles(packageRoot, "*.asmdef", SearchOption.AllDirectories);

            foreach (string asmdefPath in asmdefPaths)
            {
                string asmdef = File.ReadAllText(asmdefPath);
                Assert.IsFalse(
                    asmdef.Contains(UNITASK_GUID_REFERENCE),
                    "UniTask 软依赖禁止使用具体 asmdef GUID: " + NormalizePath(asmdefPath));

            }

            string coreAsmdef = File.ReadAllText(Path.Combine(packageRoot, "Core", "Runtime", "YokiFrame.asmdef"));
            Assert.IsTrue(coreAsmdef.Contains("\"UniTask\""), "Core 的条件 UniTask API 必须使用可缺失的程序集名引用。");
        }

        /// <summary>
        /// 验证 Unity Editor 适配程序集必须收纳在 Unity Adapter 目录下，避免 Core/Editor 根层直接承载宿主实现。
        /// </summary>
        [Test]
        public void UnityEditorAssemblyLivesUnderUnityAdapter()
        {
            string legacyPath = Path.Combine(Application.dataPath, "..", LEGACY_UNITY_EDITOR_ASMDEF_PATH);
            string adapterPath = Path.Combine(Application.dataPath, "..", UNITY_EDITOR_ASMDEF_PATH);

            Assert.IsFalse(File.Exists(legacyPath), "Unity Editor asmdef 不应停留在 Core/Editor 根层: " + LEGACY_UNITY_EDITOR_ASMDEF_PATH);
            Assert.IsTrue(File.Exists(adapterPath), "Unity Editor asmdef 必须位于 Unity Adapter 目录: " + UNITY_EDITOR_ASMDEF_PATH);
        }

        /// <summary>
        /// 验证共享 Editor 源码保持纯 C#，不直接引用 Unity 或 Godot 宿主 API。
        /// </summary>
        [Test]
        public void SharedEditorSourcesDoNotReferenceHostApis()
        {
            string editorRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Editor");
            string[] sourcePaths = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories);

            foreach (string sourcePath in sourcePaths)
            {
                string source = File.ReadAllText(sourcePath);
                string normalizedPath = NormalizePath(sourcePath);
                Assert.IsFalse(ContainsUnityEditorApi(source), "共享 Editor 禁止引用 UnityEditor: " + normalizedPath);
                Assert.IsFalse(ContainsUnityRuntimeApi(source), "共享 Editor 禁止引用 UnityEngine: " + normalizedPath);
                Assert.IsFalse(ContainsGodotRuntimeApi(source), "共享 Editor 禁止引用 Godot: " + normalizedPath);
            }
        }

        /// <summary>
        /// 验证 Unity Editor Adapter 的全部源码由整文件 Editor 宏保护，Windows Named Pipe 实现允许使用更严格的 UNITY_EDITOR_WIN 宏。
        /// </summary>
        [Test]
        public void UnityEditorAdapterSourcesUseWholeFileUnityEditorDefine()
        {
            string adapterRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Adapters", "Unity", "Editor");
            string[] sourcePaths = Directory.GetFiles(adapterRoot, "*.cs", SearchOption.AllDirectories);

            Assert.IsTrue(sourcePaths.Length > 0, "Unity Editor Adapter 目录必须包含源码文件。");
            foreach (string sourcePath in sourcePaths)
            {
                string normalizedPath = NormalizePath(sourcePath);
                string expectedDefine = normalizedPath.IndexOf("/FastChannel/", System.StringComparison.Ordinal) >= 0
                    ? UNITY_EDITOR_WINDOWS_ADAPTER_DEFINE
                    : UNITY_EDITOR_ADAPTER_DEFINE;
                AssertWholeFileGuard(sourcePath, expectedDefine);
            }
        }

        /// <summary>
        /// 验证 Runtime 宿主 API 只能位于匹配的 Engine Adapter，并由整文件宿主宏保护。
        /// </summary>
        [Test]
        public void HostSpecificRuntimeSourcesStayInsideMatchingRuntimeAdapter()
        {
            string coreRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core");
            string[] sourceRoots =
            {
                Path.Combine(coreRoot, "Runtime"),
                Path.Combine(coreRoot, "Adapters", "Unity", "Runtime"),
                Path.Combine(coreRoot, "Adapters", "Godot", "Runtime")
            };
            foreach (string sourceRoot in sourceRoots)
            {
                string[] sourcePaths = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
                foreach (string sourcePath in sourcePaths)
                {
                    AssertHostSpecificRuntimeSource(sourcePath);
                }
            }
        }

        /// <summary>
        /// 验证单个 Runtime 源码的宿主 API、目录归属和整文件宏边界。
        /// </summary>
        /// <param name="sourcePath">待检查 Runtime 源码路径。</param>
        private static void AssertHostSpecificRuntimeSource(string sourcePath)
        {
            if (IsGeneratedBuildSourcePath(sourcePath)) return;
            string source = File.ReadAllText(sourcePath);
            bool usesUnity = ContainsUnityRuntimeApi(source);
            bool usesGodot = ContainsGodotRuntimeApi(source);
            if (!usesUnity && !usesGodot) return;

            string normalizedPath = NormalizePath(sourcePath);
            Assert.IsFalse(ContainsUnityEditorApi(source), "Runtime Adapter 禁止引用 UnityEditor: " + normalizedPath);
            Assert.IsFalse(usesUnity && usesGodot, "单个 Runtime Adapter 文件不能同时绑定 Unity 与 Godot: " + normalizedPath);
            string expectedPath = usesUnity ? UNITY_RUNTIME_ADAPTER_PATH_FRAGMENT : GODOT_RUNTIME_ADAPTER_PATH_FRAGMENT;
            Assert.IsTrue(
                normalizedPath.Contains(expectedPath),
                "Runtime 宿主实现必须位于匹配的 Core/Adapters/<Engine>/Runtime: " + normalizedPath);
            AssertWholeFileGuard(sourcePath, usesUnity ? UNITY_ADAPTER_DEFINE : GODOT_ADAPTER_DEFINE);
        }

        /// <summary>
        /// 验证 Core Editor 测试程序集沿用旧版测试 GUID，防止测试引用在升级时漂移。
        /// </summary>
        [Test]
        public void CoreEditorTestsAssemblyKeepsLegacyTestsGuid()
        {
            AssertAssembly("Assets/YokiFrame/Core/Tests/Editor/YokiFrame.Tests.asmdef", "YokiFrame.Tests", "3b3a9f8cf0b042fb8f4ab0fe3f0f1d91", false);
        }

        /// <summary>
        /// 验证已经真实迁移的 ActionKit Runtime 程序集继续使用稳定 GUID。
        /// </summary>
        [Test]
        public void ActionKitRuntimeAssemblyKeepsStableGuid()
        {
            AssertAssembly("Assets/YokiFrame/Tools/ActionKit/Runtime/YokiFrame.ActionKit.asmdef", "YokiFrame.ActionKit", "bfd6b57e437b4c940985132bb8b28cf0", true);
        }

        /// <summary>
        /// 验证 Core Kit 不再拆分独立 Runtime 程序集，外部 Tool 只需依赖 YokiFrame 主程序集。
        /// </summary>
        [Test]
        public void CoreKitsCompileIntoMainYokiFrameAssembly()
        {
            AssertNoAsmdefUnder("Assets/YokiFrame/Core/Runtime/Architecture");
            AssertNoAsmdefUnder("Assets/YokiFrame/Core/Runtime/EventKit");
            AssertNoAsmdefUnder("Assets/YokiFrame/Core/Runtime/FsmKit");
            AssertNoAsmdefUnder("Assets/YokiFrame/Core/Runtime/SingletonKit");
            AssertNoAsmdefUnder("Assets/YokiFrame/Core/Runtime/PoolKit");
        }

        /// <summary>
        /// 验证所有 Tool Runtime 程序集只依赖 Core，不相互引用，确保任意 Tool 文件夹可以独立删除。
        /// </summary>
        [Test]
        public void ToolRuntimeAssembliesReferenceCoreOnly()
        {
            string toolsRoot = Path.Combine(Application.dataPath, "YokiFrame", "Tools");
            string[] asmdefPaths = Directory.GetFiles(toolsRoot, "*.asmdef", SearchOption.AllDirectories);

            foreach (string asmdefPath in asmdefPaths)
            {
                string normalizedPath = NormalizePath(asmdefPath);
                string relativePath = normalizedPath.Substring(NormalizePath(toolsRoot).Length).TrimStart('/');
                string[] pathSegments = relativePath.Split('/');
                if (pathSegments.Length != 3 || pathSegments[1] != "Runtime")
                {
                    continue;
                }

                string asmdef = File.ReadAllText(asmdefPath);
                string references = GetReferencesBlock(asmdef);
                Assert.IsTrue(references.Contains(CORE_RUNTIME_GUID_REFERENCE), "Tool Runtime 必须只通过 GUID 依赖 Core: " + normalizedPath);
                Assert.IsFalse(references.Contains("YokiFrame."), "Tool Runtime 禁止通过程序集名依赖其他 Tool: " + normalizedPath);
            }
        }

        /// <summary>
        /// 验证包内 asmdef meta GUID 没有重复，避免 Unity 资源数据库引用出现歧义。
        /// </summary>
        [Test]
        public void AssemblyDefinitionMetaGuidsAreUnique()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string[] metaPaths = Directory.GetFiles(packageRoot, "*.asmdef.meta", SearchOption.AllDirectories);
            string[] guids = new string[metaPaths.Length];

            for (int index = 0; index < metaPaths.Length; index++)
            {
                string meta = File.ReadAllText(metaPaths[index]);
                Match match = Regex.Match(meta, "guid:\\s*([a-f0-9]+)");
                Assert.IsTrue(match.Success, "asmdef meta 缺少 guid: " + NormalizePath(metaPaths[index]));
                guids[index] = match.Groups[1].Value;
            }

            for (int index = 0; index < guids.Length; index++)
            {
                for (int nextIndex = index + 1; nextIndex < guids.Length; nextIndex++)
                {
                    Assert.AreNotEqual(
                        guids[index],
                        guids[nextIndex],
                        "asmdef meta GUID 重复: " + NormalizePath(metaPaths[index]) + " 和 " + NormalizePath(metaPaths[nextIndex]));
                }
            }
        }

        /// <summary>
        /// 读取指定 asmdef 和 meta 文件，并校验程序集名、GUID 和 Runtime 引擎依赖开关。
        /// </summary>
        /// <param name="assetPath">Unity 项目内 asmdef 路径。</param>
        /// <param name="assemblyName">期望程序集名。</param>
        /// <param name="expectedGuid">期望旧 GUID；为空时只验证 meta 存在。</param>
        /// <param name="expectNoEngineReferences">是否期望禁止 Unity 引擎引用。</param>
        private static void AssertAssembly(string assetPath, string assemblyName, string expectedGuid, bool expectNoEngineReferences)
        {
            AssertAssembly(assetPath, assemblyName, expectedGuid, expectNoEngineReferences, "YokiFrame");
        }

        /// <summary>
        /// 读取指定 asmdef 和 meta 文件，并校验程序集名、root namespace、GUID 和 Runtime 引擎依赖开关。
        /// </summary>
        /// <param name="assetPath">Unity 项目内 asmdef 路径。</param>
        /// <param name="assemblyName">期望程序集名。</param>
        /// <param name="expectedGuid">期望旧 GUID；为空时只验证 meta 存在。</param>
        /// <param name="expectNoEngineReferences">是否期望禁止 Unity 引擎引用。</param>
        /// <param name="expectedRootNamespace">期望 root namespace。</param>
        private static void AssertAssembly(string assetPath, string assemblyName, string expectedGuid, bool expectNoEngineReferences, string expectedRootNamespace)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            string metaPath = fullPath + ".meta";

            Assert.IsTrue(File.Exists(fullPath), "缺少 asmdef: " + assetPath);
            Assert.IsTrue(File.Exists(metaPath), "缺少 asmdef meta: " + assetPath + ".meta");

            string asmdef = File.ReadAllText(fullPath);
            Assert.IsTrue(asmdef.Contains("\"name\": \"" + assemblyName + "\""), "程序集名不匹配: " + assetPath);
            Assert.IsTrue(asmdef.Contains("\"rootNamespace\": \"" + expectedRootNamespace + "\""), "rootNamespace 必须保持 " + expectedRootNamespace + ": " + assetPath);

            if (!string.IsNullOrEmpty(expectedGuid))
            {
                string meta = File.ReadAllText(metaPath);
                Assert.IsTrue(meta.Contains("guid: " + expectedGuid), "旧版 GUID 未保留: " + assetPath);
            }

            if (expectNoEngineReferences)
            {
                Assert.IsTrue(asmdef.Contains("\"noEngineReferences\": true"), "Runtime asmdef 不应引用 UnityEngine: " + assetPath);
            }
        }

        /// <summary>
        /// 验证指定目录下不存在 asmdef，确保 Core Kit 直接编入 YokiFrame 主程序集。
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
        /// 验证源码文件的第一条有效行和最后一条有效行组成完整条件编译边界。
        /// </summary>
        /// <param name="sourcePath">源码文件路径。</param>
        /// <param name="expectedDefine">期望的起始宏。</param>
        private static void AssertWholeFileGuard(string sourcePath, string expectedDefine)
        {
            string[] lines = File.ReadAllLines(sourcePath);
            string firstLine = FirstNonEmptyLine(lines);
            string lastLine = LastNonEmptyLine(lines);

            bool matchesExpectedDefine = string.Equals(firstLine, expectedDefine, System.StringComparison.Ordinal)
                || firstLine.StartsWith(expectedDefine + " &&", System.StringComparison.Ordinal)
                || string.Equals(expectedDefine, UNITY_ADAPTER_DEFINE, System.StringComparison.Ordinal)
                    && firstLine.StartsWith("#if UNITY_2022_3_OR_NEWER", System.StringComparison.Ordinal);
            Assert.IsTrue(matchesExpectedDefine, "源码文件必须从指定宿主宏开始: " + NormalizePath(sourcePath));
            Assert.AreEqual("#endif", lastLine, "源码文件必须用 #endif 结束，确保整文件被宏包裹: " + NormalizePath(sourcePath));
        }

        /// <summary>
        /// 读取文件的第一条非空行，便于验证宏边界。
        /// </summary>
        /// <param name="sourcePath">源码文件路径。</param>
        /// <returns>第一条非空行；文件为空时返回空字符串。</returns>
        private static string FirstNonEmptyLine(string sourcePath)
        {
            return FirstNonEmptyLine(File.ReadAllLines(sourcePath));
        }

        /// <summary>
        /// 从行数组中获取第一条非空行。
        /// </summary>
        /// <param name="lines">源码行数组。</param>
        /// <returns>第一条非空行；不存在时返回空字符串。</returns>
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

        /// <summary>
        /// 从行数组中获取最后一条非空行。
        /// </summary>
        /// <param name="lines">源码行数组。</param>
        /// <returns>最后一条非空行；不存在时返回空字符串。</returns>
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

        /// <summary>
        /// 从 asmdef 文本中提取 references 数组片段，用于检查程序集依赖边界。
        /// </summary>
        /// <param name="asmdef">asmdef JSON 文本。</param>
        /// <returns>references 数组文本；没有 references 时返回空字符串。</returns>
        private static string GetReferencesBlock(string asmdef)
        {
            Match match = Regex.Match(asmdef, "\"references\"\\s*:\\s*\\[(.*?)\\]", RegexOptions.Singleline);
            return match.Success ? match.Groups[1].Value : string.Empty;
        }

        /// <summary>
        /// 判断源码中是否包含编辑器监控钩子的特征词。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <returns>包含编辑器钩子特征时返回 true。</returns>
        private static bool ContainsEditorHookSignal(string source)
        {
            return source.Contains("EditorHook") ||
                   source.Contains("EditorMonitor") ||
                   source.Contains("CallerFilePath") ||
                   source.Contains("CallerLineNumber") ||
                   source.Contains("CallerMemberName");
        }

        /// <summary>
        /// 判断源码是否直接使用 Unity Runtime API。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <returns>包含 UnityEngine 引用时返回 true。</returns>
        private static bool ContainsUnityRuntimeApi(string source)
        {
            return source.Contains("using UnityEngine;") || source.Contains("UnityEngine.");
        }

        /// <summary>
        /// 判断源码是否直接使用 Unity Editor API。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <returns>包含 UnityEditor 引用时返回 true。</returns>
        private static bool ContainsUnityEditorApi(string source)
        {
            return source.Contains("using UnityEditor;") || source.Contains("UnityEditor.");
        }

        /// <summary>
        /// 判断源码是否直接使用 Godot Runtime API。
        /// </summary>
        /// <param name="source">源码文本。</param>
        /// <returns>包含 Godot 引用时返回 true。</returns>
        private static bool ContainsGodotRuntimeApi(string source)
        {
            return source.Contains("using Godot;") ||
                   Regex.IsMatch(source, "(?<![A-Za-z0-9_.])Godot\\.");
        }

        /// <summary>
        /// 判断源码路径是否属于宿主 SDK 生成的构建缓存，避免把临时 AssemblyInfo 当成框架权威源码。
        /// </summary>
        /// <param name="sourcePath">待检查的 C# 文件路径。</param>
        /// <returns>位于 `.godot`、`bin` 或 `obj` 目录时返回 true。</returns>
        private static bool IsGeneratedBuildSourcePath(string sourcePath)
        {
            string normalizedPath = NormalizePath(sourcePath);
            return normalizedPath.IndexOf("/.godot/", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalizedPath.IndexOf("/bin/", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalizedPath.IndexOf("/obj/", System.StringComparison.OrdinalIgnoreCase) >= 0;
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
