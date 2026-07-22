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

        /// <summary>验证纯 ActionKit Runtime 不直接引用 UniTask、Unity Runtime Adapter 或 UnityEngine。</summary>
        [Test]
        public void ActionKitRuntimeRemainsHostAndThirdPartyIndependent()
        {
            string asmdef = ReadActionKitProjectFile(ACTION_KIT_RUNTIME_ASMDEF);
            string references = GetReferencesBlock(asmdef);

            Assert.IsTrue(references.Contains(CORE_RUNTIME_GUID_REFERENCE));
            Assert.IsFalse(references.Contains("UniTask"));
            Assert.IsFalse(references.Contains("YokiFrame.Unity.Runtime"));

            string runtimeRoot = ActionKitProjectPath("Assets/YokiFrame/Tools/ActionKit/Runtime");
            string[] sources = Directory.GetFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories);
            foreach (string sourcePath in sources)
            {
                string source = File.ReadAllText(sourcePath);
                Assert.IsFalse(source.Contains("Cysharp.Threading.Tasks"), NormalizePath(sourcePath));
                Assert.IsFalse(ContainsUnityRuntimeApi(source), NormalizePath(sourcePath));
            }
        }

        /// <summary>验证 UniTask Integration 使用程序集名软引用、依赖宏和单向 ActionKit/Core 引用。</summary>
        [Test]
        public void ActionKitUniTaskIntegrationHasOptionalAssemblyBoundary()
        {
            string asmdef = ReadActionKitProjectFile(ACTION_KIT_UNITASK_ASMDEF);
            string references = GetReferencesBlock(asmdef);

            Assert.IsTrue(references.Contains(CORE_RUNTIME_GUID_REFERENCE));
            Assert.IsTrue(references.Contains(ACTION_KIT_GUID_REFERENCE));
            Assert.IsTrue(references.Contains("\"UniTask\""));
            Assert.IsTrue(asmdef.Contains("\"YOKIFRAME_UNITASK_SUPPORT\""));
            Assert.IsFalse(asmdef.Contains(UNITASK_GUID_REFERENCE));
            AssertActionKitWholeFileGuards(
                "Assets/YokiFrame/Tools/ActionKit/Integrations/Unity/UniTask/Runtime",
                "#if UNITY_5_3_OR_NEWER && YOKIFRAME_UNITASK_SUPPORT");
        }

        /// <summary>验证 Unity Coroutine Adapter 只依赖 Core、Core Unity Runtime 与 ActionKit。</summary>
        [Test]
        public void ActionKitUnityCoroutineHasDedicatedAdapterBoundary()
        {
            string asmdef = ReadActionKitProjectFile(ACTION_KIT_UNITY_ASMDEF);
            string references = GetReferencesBlock(asmdef);

            Assert.IsTrue(references.Contains(CORE_RUNTIME_GUID_REFERENCE));
            Assert.IsTrue(references.Contains(ACTION_KIT_GUID_REFERENCE));
            Assert.IsTrue(references.Contains("GUID:57d76a5674b6f474e97d39484877f63c"));
            Assert.IsFalse(references.Contains(CORE_EDITOR_GUID_REFERENCE));
            Assert.IsFalse(references.Contains("UniTask"));
            AssertActionKitWholeFileGuards(
                "Assets/YokiFrame/Tools/ActionKit/Adapters/Unity/Runtime",
                UNITY_ADAPTER_DEFINE);
        }

        /// <summary>读取项目相对文件，统一约束架构测试的根路径。</summary>
        /// <param name="assetPath">Unity 项目内相对路径。</param>
        /// <returns>文件完整文本。</returns>
        private static string ReadActionKitProjectFile(string assetPath) =>
            File.ReadAllText(ActionKitProjectPath(assetPath));

        /// <summary>把 Unity 项目内路径转换为本机绝对路径。</summary>
        /// <param name="assetPath">Unity 项目内相对路径。</param>
        /// <returns>本机绝对路径。</returns>
        private static string ActionKitProjectPath(string assetPath) =>
            Path.Combine(Application.dataPath, "..", assetPath);

        /// <summary>验证目录内全部业务源码都使用指定整文件宏；asmdef 不参与源码宏检查。</summary>
        /// <param name="assetRoot">待扫描目录。</param>
        /// <param name="expectedDefine">期望第一条有效行。</param>
        private static void AssertActionKitWholeFileGuards(string assetRoot, string expectedDefine)
        {
            string[] sourcePaths = Directory.GetFiles(
                ActionKitProjectPath(assetRoot),
                "*.cs",
                SearchOption.AllDirectories);
            Assert.Greater(sourcePaths.Length, 0, "适配目录必须包含真实源码: " + assetRoot);
            foreach (string sourcePath in sourcePaths)
                AssertWholeFileGuard(sourcePath, expectedDefine);
        }
    }
}
