using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>验证 ActionKit 可选异步能力保持独立 Adapter/Integration 程序集与整文件宏边界。</summary>
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        private const string ACTION_KIT_RUNTIME_ASMDEF =
            "Assets/YokiFrame/Tools/ActionKit/Runtime/YokiFrame.ActionKit.asmdef";
        private const string ACTION_KIT_UNITASK_ASMDEF =
            "Assets/YokiFrame/Tools/ActionKit/Integrations/Unity/UniTask/Runtime/YokiFrame.ActionKit.UniTask.asmdef";
        private const string ACTION_KIT_UNITY_ASMDEF =
            "Assets/YokiFrame/Tools/ActionKit/Adapters/Unity/Runtime/YokiFrame.ActionKit.Unity.asmdef";
        private const string ACTION_KIT_UNITASK_NAMESPACE = "Cysharp.Threading.Tasks";
        private const string ACTION_KIT_UNITASK_DEFINE = "YOKIFRAME_UNITASK_SUPPORT";
        private const string UNITY_RUNTIME_GUID = "GUID:57d76a5674b6f474e97d39484877f63c";

        /// <summary>
        /// 验证纯 ActionKit Runtime 只按 GUID 依赖 Core，源码也不引用 UniTask 或 UnityEngine。
        /// </summary>
        [Test]
        public void AssemblyBoundary_ActionKitRuntime_不引用宿主与UniTask()
        {
            AssertReferencesEqual(ACTION_KIT_RUNTIME_ASMDEF, CORE_RUNTIME_GUID_REFERENCE);
            AssertActionKitSourcesAvoidHostAndUniTask("Assets/YokiFrame/Tools/ActionKit/Runtime");
        }

        /// <summary>
        /// 验证 UniTask Integration 按名称软引用 UniTask，受存在性宏约束，并且不绑定 UniTask GUID。
        /// </summary>
        [Test]
        public void AssemblyBoundary_ActionKitUniTask集成_软引用且受存在性宏约束()
        {
            AssertReferencesEqual(
                ACTION_KIT_UNITASK_ASMDEF,
                CORE_RUNTIME_GUID_REFERENCE,
                ACTION_KIT_GUID_REFERENCE,
                "UniTask");
            string[] defines = ReadStringArrayProperty(ACTION_KIT_UNITASK_ASMDEF, "defineConstraints");
            Assert.AreEqual(1, defines.Length, "UniTask Integration 必须且只能受存在性宏约束。");
            Assert.AreEqual(ACTION_KIT_UNITASK_DEFINE, defines[0], "UniTask Integration 必须使用 YOKIFRAME_UNITASK_SUPPORT。");
            Assert.IsFalse(
                AsmdefContainsExactString(ActionKitProjectPath(ACTION_KIT_UNITASK_ASMDEF), UNITASK_GUID_REFERENCE),
                "UniTask 软依赖禁止绑定具体 asmdef GUID。");
            AssertActionKitWholeFileGuards(
                "Assets/YokiFrame/Tools/ActionKit/Integrations/Unity/UniTask/Runtime",
                "#if UNITY_5_3_OR_NEWER && YOKIFRAME_UNITASK_SUPPORT");
        }

        /// <summary>
        /// 验证 Unity Coroutine Adapter 只依赖 Core、Unity Runtime 与 ActionKit。
        /// </summary>
        [Test]
        public void AssemblyBoundary_ActionKitUnity适配_只依赖Core与UnityRuntime()
        {
            AssertReferencesEqual(
                ACTION_KIT_UNITY_ASMDEF,
                CORE_RUNTIME_GUID_REFERENCE,
                UNITY_RUNTIME_GUID,
                ACTION_KIT_GUID_REFERENCE);
            AssertActionKitWholeFileGuards(
                "Assets/YokiFrame/Tools/ActionKit/Adapters/Unity/Runtime",
                UNITY_ADAPTER_DEFINE);
        }

        /// <summary>
        /// 验证目录内源码没有 UniTask 或 UnityEngine 引用。按行首 using 和独立限定名判断。
        /// </summary>
        /// <param name="assetRoot">Unity 项目内源码根。</param>
        private static void AssertActionKitSourcesAvoidHostAndUniTask(string assetRoot)
        {
            string[] sourcePaths = Directory.GetFiles(ActionKitProjectPath(assetRoot), "*.cs", SearchOption.AllDirectories);
            Assert.Greater(sourcePaths.Length, 0, "ActionKit Runtime 必须包含源码: " + assetRoot);
            for (int index = 0; index < sourcePaths.Length; index++)
            {
                string source = File.ReadAllText(sourcePaths[index]);
                string normalizedPath = NormalizePath(sourcePaths[index]);
                Assert.IsFalse(HasLineStartUsing(source, ACTION_KIT_UNITASK_NAMESPACE), normalizedPath);
                Assert.IsFalse(HasNamespaceQualifier(source, ACTION_KIT_UNITASK_NAMESPACE, true), normalizedPath);
                Assert.IsFalse(ContainsUnityRuntimeApi(source), normalizedPath);
            }
        }

        /// <summary>把 Unity 项目内路径转换为本机绝对路径。</summary>
        /// <param name="assetPath">Unity 项目内相对路径。</param>
        /// <returns>本机绝对路径。</returns>
        private static string ActionKitProjectPath(string assetPath)
        {
            return Path.Combine(Application.dataPath, "..", assetPath);
        }

        /// <summary>验证目录内全部业务源码都使用指定整文件宏。</summary>
        /// <param name="assetRoot">待扫描目录。</param>
        /// <param name="expectedDefine">期望第一条有效行。</param>
        private static void AssertActionKitWholeFileGuards(string assetRoot, string expectedDefine)
        {
            string[] sourcePaths = Directory.GetFiles(
                ActionKitProjectPath(assetRoot),
                "*.cs",
                SearchOption.AllDirectories);
            Assert.Greater(sourcePaths.Length, 0, "适配目录必须包含真实源码: " + assetRoot);
            for (int index = 0; index < sourcePaths.Length; index++)
            {
                AssertWholeFileGuard(sourcePaths[index], expectedDefine);
            }
        }
    }
}
