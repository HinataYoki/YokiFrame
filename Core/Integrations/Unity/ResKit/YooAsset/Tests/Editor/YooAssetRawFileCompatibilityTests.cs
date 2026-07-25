#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_3
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Unity.Tests
{
    /// <summary>守护 YooAsset V3 EditorSimulate raw 文件必须走 TextAsset 兼容路径。</summary>
    public sealed class YooAssetRawFileCompatibilityTests
    {
        private const string PROVIDER_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YooAssetResourceProvider.cs";
        private const string INITIALIZER_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/Initialization/YooAssetInitializer.cs";
        private const string EDITOR_PATH =
            "YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Editor/Initialization/YooAssetInitializationBehaviourEditor.cs";

        /// <summary>验证 EditorSimulate raw 读取和 Provider 安装都保留 TextAsset 模式分支。</summary>
        [Test]
        public void EditorSimulateRawLoadingUsesTextAsset()
        {
            string providerSource = ReadSource(PROVIDER_PATH);
            string initializerSource = ReadSource(INITIALIZER_PATH);
            string editorSource = ReadSource(EDITOR_PATH);

            StringAssert.Contains("UseEditorTextAsset(path", providerSource);
            StringAssert.Contains("LoadAssetSync<TextAsset>(path)", providerSource);
            StringAssert.Contains(
                "options.PlayMode == EPlayMode.EditorSimulateMode",
                initializerSource);
            StringAssert.Contains(
                "behaviour.Options.PlayMode == EPlayMode.EditorSimulateMode",
                editorSource);
        }

        /// <summary>将 Assets 相对路径解析为当前 Unity 工程中的源码路径。</summary>
        private static string ReadSource(string relativePath)
        {
            string sourcePath = Path.GetFullPath(Path.Combine(Application.dataPath, relativePath));
            return File.ReadAllText(sourcePath);
        }
    }
}
#endif
