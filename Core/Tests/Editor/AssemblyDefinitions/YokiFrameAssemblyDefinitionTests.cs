using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 YokiFrame 包内程序集定义、宿主宏和旧版 GUID，避免升级时 asmdef 引用或编译边界漂移。
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
        private const string LIVE_CODE_FACADE_ASMDEF_PATH =
            "Assets/YokiFrame/Tools/RoslynKit/Adapters/Unity/Runtime/LiveCode/YokiFrame.Unity.LiveCode.Facade.asmdef";
        private const string LEGACY_UNITY_EDITOR_ASMDEF_PATH = "Assets/YokiFrame/Core/Editor/YokiFrame.Unity.Editor.asmdef";
        private const string UNITY_RUNTIME_ADAPTER_PATH_FRAGMENT = "/Assets/YokiFrame/Core/Adapters/Unity/Runtime/";
        private const string GODOT_RUNTIME_ADAPTER_PATH_FRAGMENT = "/Assets/YokiFrame/Core/Adapters/Godot/Runtime/";
        private const string UNITY_ADAPTER_DEFINE = "#if UNITY_5_3_OR_NEWER";
        private const string UNITY_EDITOR_ADAPTER_DEFINE = "#if UNITY_EDITOR";
        private const string UNITY_EDITOR_WINDOWS_ADAPTER_DEFINE = "#if UNITY_EDITOR_WIN";
        private const string GODOT_ADAPTER_DEFINE = "#if GODOT";
        private const string EDITOR_CONTEXT_DEFINE = "#if UNITY_EDITOR || (GODOT && TOOLS)";

        /// <summary>
        /// 验证 Core Runtime asmdef 保留旧程序集名、GUID、命名空间，并且禁止引擎引用。
        /// </summary>
        [Test]
        public void AssemblyBoundary_CoreRuntime程序集_保留旧标识且禁止引擎引用()
        {
            AssertAssembly("Assets/YokiFrame/Core/Runtime/YokiFrame.asmdef", "YokiFrame", "224151c835b786a4f8efadaa1799380d", true);
        }

        /// <summary>
        /// 验证已加载的 Core 主程序集没有引用 Unity 或 Godot。这是编译结果，不是 asmdef 文本。
        /// </summary>
        [Test]
        public void AssemblyBoundary_CoreRuntime已加载程序集_不引用宿主()
        {
            System.Reflection.AssemblyName[] referencedAssemblies = typeof(IEngineLogger).Assembly.GetReferencedAssemblies();
            Assert.AreEqual("YokiFrame", typeof(IEngineLogger).Assembly.GetName().Name, "IEngineLogger 必须位于 Core 主程序集。");
            for (int index = 0; index < referencedAssemblies.Length; index++)
            {
                string name = referencedAssemblies[index].Name ?? string.Empty;
                Assert.IsFalse(name.StartsWith("UnityEngine", StringComparison.Ordinal), "Core 主程序集禁止引用 UnityEngine: " + name);
                Assert.IsFalse(name.StartsWith("UnityEditor", StringComparison.Ordinal), "Core 主程序集禁止引用 UnityEditor: " + name);
                Assert.IsFalse(name.StartsWith("Godot", StringComparison.Ordinal), "Core 主程序集禁止引用 Godot: " + name);
            }
        }

        /// <summary>
        /// 验证 LiveCode 原型外观不被 Editor 平台或 Editor 目录排除，因此 Play Mode 可以挂载；源码仍由整文件 Editor 宏剥离出 Player。
        /// </summary>
        [Test]
        public void AssemblyBoundary_LiveCode原型外观_PlayMode可挂载()
        {
            AssertAssembly(LIVE_CODE_FACADE_ASMDEF_PATH, "YokiFrame.Unity.LiveCode.Facade", "7c4e1a9b2d6f4e0a8b3c5d7e9f102346", false);
            AssertIncludePlatformsEqual(LIVE_CODE_FACADE_ASMDEF_PATH);
            AssertReferencesEqual(LIVE_CODE_FACADE_ASMDEF_PATH);
            AssertDirectorySegmentAbsent(LIVE_CODE_FACADE_ASMDEF_PATH, "Editor");
            string facadeDirectory = Path.GetDirectoryName(Path.Combine(Application.dataPath, "..", LIVE_CODE_FACADE_ASMDEF_PATH));
            string[] sources = Directory.GetFiles(facadeDirectory, "*.cs", SearchOption.TopDirectoryOnly);
            Assert.Greater(sources.Length, 0, "LiveCode 原型外观缺少源码。");
            for (int index = 0; index < sources.Length; index++)
            {
                AssertWholeFileGuard(sources[index], UNITY_EDITOR_ADAPTER_DEFINE);
            }
        }

        /// <summary>
        /// 验证 Unity Editor 适配程序集沿用旧版 Editor GUID，并只在 Editor 平台编译。
        /// </summary>
        [Test]
        public void AssemblyBoundary_UnityEditor程序集_保留旧标识且仅Editor平台()
        {
            AssertAssembly(UNITY_EDITOR_ASMDEF_PATH, "YokiFrame.Unity.Editor", "c43cf80ab82b5e04ba14ddf6cd7b676e", false);
            AssertIncludePlatformsEqual(UNITY_EDITOR_ASMDEF_PATH, "Editor");
        }

        /// <summary>
        /// 验证共享 Editor 程序集只依赖 Core Runtime，并且只在 Editor 平台编译。
        /// </summary>
        [Test]
        public void AssemblyBoundary_共享Editor程序集_只依赖Core且仅Editor平台()
        {
            AssertAssembly(CORE_EDITOR_ASMDEF_PATH, "YokiFrame.Editor", "8e62f2f1c4b54a22a65cf2e98aa12920", true);
            AssertReferencesEqual(CORE_EDITOR_ASMDEF_PATH, CORE_RUNTIME_GUID_REFERENCE);
            AssertIncludePlatformsEqual(CORE_EDITOR_ASMDEF_PATH, "Editor");
        }

        /// <summary>
        /// 验证 Unity Runtime 适配程序集沿用旧版 Runtime GUID，依赖 Core，且不反向依赖 Editor、EventKit 或 ActionKit。
        /// </summary>
        [Test]
        public void AssemblyBoundary_UnityRuntime程序集_保留旧标识且不反向依赖Editor或Tool()
        {
            AssertAssembly(UNITY_RUNTIME_ASMDEF_PATH, "YokiFrame.Unity.Runtime", "57d76a5674b6f474e97d39484877f63c", false, "YokiFrame.Unity");
            string[] references = ReadReferences(Path.Combine(Application.dataPath, "..", UNITY_RUNTIME_ASMDEF_PATH));
            Assert.IsTrue(HasReference(references, CORE_RUNTIME_GUID_REFERENCE), "Unity Runtime Adapter 必须通过 GUID 依赖 Core。");
            Assert.IsFalse(HasReference(references, CORE_EDITOR_GUID_REFERENCE), "Unity Runtime Adapter 禁止依赖共享 Editor 程序集。");
            Assert.IsFalse(HasReference(references, EVENT_KIT_GUID_REFERENCE), "Unity Runtime Adapter 不应依赖独立 EventKit 程序集。");
            Assert.IsFalse(HasReference(references, ACTION_KIT_GUID_REFERENCE), "Unity Runtime Adapter 禁止反向依赖 ActionKit Tool。");
        }

        /// <summary>
        /// 验证 UniTask 只以可缺失的程序集名引用，任何 asmdef 字段都不绑定具体 GUID。
        /// </summary>
        [Test]
        public void AssemblyBoundary_UniTask引用_只使用可缺失程序集名()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string[] asmdefPaths = Directory.GetFiles(packageRoot, "*.asmdef", SearchOption.AllDirectories);
            Assert.Greater(asmdefPaths.Length, 0, "包内必须存在 asmdef。");
            for (int index = 0; index < asmdefPaths.Length; index++)
            {
                Assert.IsFalse(
                    AsmdefContainsExactString(asmdefPaths[index], UNITASK_GUID_REFERENCE),
                    "UniTask 软依赖禁止使用具体 asmdef GUID: " + NormalizePath(asmdefPaths[index]));
            }

            string[] references = ReadReferences(Path.Combine(packageRoot, "Core", "Runtime", "YokiFrame.asmdef"));
            Assert.IsTrue(HasReference(references, "UniTask"), "Core 的条件 UniTask API 必须使用可缺失的程序集名引用。");
        }

        /// <summary>
        /// 验证 Unity Editor asmdef 位于 Unity Adapter 目录，不再停留在 Core/Editor 根层。
        /// </summary>
        [Test]
        public void AssemblyBoundary_UnityEditor程序集_位于UnityAdapter目录()
        {
            string legacyPath = Path.Combine(Application.dataPath, "..", LEGACY_UNITY_EDITOR_ASMDEF_PATH);
            string adapterPath = Path.Combine(Application.dataPath, "..", UNITY_EDITOR_ASMDEF_PATH);
            Assert.IsFalse(File.Exists(legacyPath), "Unity Editor asmdef 不应停留在 Core/Editor 根层: " + LEGACY_UNITY_EDITOR_ASMDEF_PATH);
            Assert.IsTrue(File.Exists(adapterPath), "Unity Editor asmdef 必须位于 Unity Adapter 目录: " + UNITY_EDITOR_ASMDEF_PATH);
        }

        /// <summary>
        /// 验证共享 Editor 源码不引用 Unity 或 Godot 宿主 API。按标识符边界判断，避免命中更长的标识符。
        /// </summary>
        [Test]
        public void SourceGuard_共享Editor源码_不引用宿主API()
        {
            string editorRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Editor");
            string[] sourcePaths = Directory.GetFiles(editorRoot, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(sourcePaths.Length, 0, "共享 Editor 目录必须包含源码。");
            for (int index = 0; index < sourcePaths.Length; index++)
            {
                AssertSourceDoesNotUseHostApi(sourcePaths[index]);
            }
        }

        /// <summary>
        /// 验证 Unity Editor Adapter 的全部源码由整文件 Editor 宏保护。Windows Named Pipe 实现允许使用更严格的 UNITY_EDITOR_WIN 宏。
        /// </summary>
        [Test]
        public void SourceGuard_UnityEditor适配源码_整文件使用Editor宏()
        {
            string adapterRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Adapters", "Unity", "Editor");
            string[] sourcePaths = Directory.GetFiles(adapterRoot, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(sourcePaths.Length, 0, "Unity Editor Adapter 目录必须包含源码文件。");
            for (int index = 0; index < sourcePaths.Length; index++)
            {
                string normalizedPath = NormalizePath(sourcePaths[index]);
                string expectedDefine = normalizedPath.IndexOf("/FastChannel/", StringComparison.Ordinal) >= 0
                    ? UNITY_EDITOR_WINDOWS_ADAPTER_DEFINE
                    : UNITY_EDITOR_ADAPTER_DEFINE;
                AssertWholeFileGuard(sourcePaths[index], expectedDefine);
            }
        }

        /// <summary>
        /// 验证 Runtime 宿主 API 只能位于匹配的 Engine Adapter，并由整文件宿主宏保护。
        /// </summary>
        [Test]
        public void SourceGuard_Runtime宿主API_只位于匹配Adapter且整文件宏保护()
        {
            string coreRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core");
            AssertHostRuntimeRoot(Path.Combine(coreRoot, "Runtime"));
            AssertHostRuntimeRoot(Path.Combine(coreRoot, "Adapters", "Unity", "Runtime"));
            AssertHostRuntimeRoot(Path.Combine(coreRoot, "Adapters", "Godot", "Runtime"));
        }

        /// <summary>
        /// 验证 Core Editor 测试程序集沿用旧版测试 GUID，防止测试引用在升级时漂移。
        /// </summary>
        [Test]
        public void AssemblyBoundary_Core测试程序集_保留旧标识()
        {
            AssertAssembly("Assets/YokiFrame/Core/Tests/Editor/YokiFrame.Tests.asmdef", "YokiFrame.Tests", "3b3a9f8cf0b042fb8f4ab0fe3f0f1d91", false);
        }

        /// <summary>
        /// 验证 ActionKit Runtime 程序集继续使用稳定 GUID，并且不引用引擎。引用方向由 ActionKit 边界测试另行证明。
        /// </summary>
        [Test]
        public void AssemblyBoundary_ActionKitRuntime程序集_保留稳定标识()
        {
            AssertAssembly("Assets/YokiFrame/Tools/ActionKit/Runtime/YokiFrame.ActionKit.asmdef", "YokiFrame.ActionKit", "bfd6b57e437b4c940985132bb8b28cf0", true);
        }

        /// <summary>
        /// 验证尚未独立成程序集的 Core Kit 目录下没有 asmdef。FsmKit 由专门的程序集边界测试覆盖，这里不重复。
        /// </summary>
        [Test]
        public void AssemblyBoundary_CoreKit目录_不声明独立程序集()
        {
            AssertNoAsmdefUnder("Assets/YokiFrame/Core/Runtime/Architecture");
            AssertNoAsmdefUnder("Assets/YokiFrame/Core/Runtime/EventKit");
            AssertNoAsmdefUnder("Assets/YokiFrame/Core/Runtime/SingletonKit");
            AssertNoAsmdefUnder("Assets/YokiFrame/Core/Runtime/PoolKit");
        }

        /// <summary>
        /// 验证直接位于 Tool/Runtime 的程序集都通过 GUID 依赖 Core，并且不按程序集名引用其他 YokiFrame 程序集。
        /// </summary>
        [Test]
        public void AssemblyBoundary_ToolRuntime程序集_只通过GUID依赖Core()
        {
            string toolsRoot = Path.Combine(Application.dataPath, "YokiFrame", "Tools");
            string[] asmdefPaths = Directory.GetFiles(toolsRoot, "*.asmdef", SearchOption.AllDirectories);
            int checkedCount = 0;
            for (int index = 0; index < asmdefPaths.Length; index++)
            {
                if (!IsDirectToolRuntimeAsmdef(toolsRoot, asmdefPaths[index]))
                {
                    continue;
                }

                checkedCount++;
                string[] references = ReadReferences(asmdefPaths[index]);
                Assert.IsTrue(
                    HasReference(references, CORE_RUNTIME_GUID_REFERENCE),
                    "Tool Runtime 必须通过 GUID 依赖 Core: " + NormalizePath(asmdefPaths[index]));
                AssertNoNamedYokiFrameReference(references, asmdefPaths[index]);
            }

            Assert.Greater(checkedCount, 0, "Tools 下必须存在直接 Runtime 程序集。");
        }

        /// <summary>
        /// 验证包内 asmdef meta GUID 没有重复，避免 Unity 资源数据库引用出现歧义。
        /// </summary>
        [Test]
        public void AssemblyBoundary_程序集Meta_GUID互不重复()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string[] metaPaths = Directory.GetFiles(packageRoot, "*.asmdef.meta", SearchOption.AllDirectories);
            Assert.Greater(metaPaths.Length, 0, "包内必须存在 asmdef meta。");
            string[] guids = new string[metaPaths.Length];
            for (int index = 0; index < metaPaths.Length; index++)
            {
                guids[index] = ReadMetaGuid(metaPaths[index]);
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
    }
}
