using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        private const string UIKIT_RUNTIME_ASMDEF_PATH =
            "Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/YokiFrame.UIKit.Unity.asmdef";
        private const string UIKIT_EDITOR_ASMDEF_PATH =
            "Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Editor/YokiFrame.UIKit.Unity.Editor.asmdef";
        private const string UIKIT_DOTWEEN_ASMDEF_PATH =
            "Assets/YokiFrame/Tools/UIKit/Integrations/Unity/DOTween/Runtime/YokiFrame.UIKit.DOTween.asmdef";

        /// <summary>
        /// 验证 UIKit 只提供 Unity Runtime 与 Unity Editor 两个程序集，不创建共享或 Godot 实现壳。
        /// </summary>
        [Test]
        public void UIKitAssembliesRemainUnityOnly()
        {
            AssertAssembly(UIKIT_RUNTIME_ASMDEF_PATH, "YokiFrame.UIKit.Unity", string.Empty, false);
            AssertAssembly(UIKIT_EDITOR_ASMDEF_PATH, "YokiFrame.UIKit.Unity.Editor", string.Empty, false);
            string kitRoot = Path.Combine(Application.dataPath, "YokiFrame", "Tools", "UIKit");
            Assert.IsFalse(Directory.Exists(Path.Combine(kitRoot, "Runtime")), "UIKit 禁止创建跨宿主 Runtime 根。");
            Assert.IsFalse(Directory.Exists(Path.Combine(kitRoot, "Adapters", "Godot")), "UIKit 禁止创建 Godot Adapter。");
        }

        /// <summary>
        /// 验证 UIKit Runtime 只依赖 Core、Unity Runtime Adapter 与可选 UniTask，不反向依赖其它 Tool 或 Editor。
        /// </summary>
        [Test]
        public void UIKitRuntimeAssemblyHasOneWayUnityDependencies()
        {
            string asmdef = ReadProjectFile(UIKIT_RUNTIME_ASMDEF_PATH);
            string references = GetReferencesBlock(asmdef);
            Assert.IsTrue(references.Contains("\"YokiFrame\""), "UIKit Runtime 必须依赖 Core。");
            Assert.IsTrue(references.Contains("\"YokiFrame.Unity.Runtime\""), "UIKit Runtime 必须复用 Unity MonoSingleton。");
            Assert.IsTrue(references.Contains("\"UniTask\""), "UIKit UniTask 必须使用可缺失程序集名引用。");
            Assert.IsFalse(references.Contains(".Editor"), "UIKit Runtime 禁止依赖 Editor 程序集。");
            Assert.IsFalse(references.Contains("AudioKit") || references.Contains("ActionKit"), "UIKit 禁止依赖其它 Tool。");
        }

        /// <summary>
        /// 验证 UIKit DOTween 集成只依赖 DOTween 核心，避免未执行 DOTween Setup 时硬引用缺失的 Modules 程序集。
        /// </summary>
        [Test]
        public void UIKitDOTweenIntegrationDoesNotRequireOptionalModulesAssembly()
        {
            string asmdef = ReadProjectFile(UIKIT_DOTWEEN_ASMDEF_PATH);
            string references = GetReferencesBlock(asmdef);
            Assert.IsFalse(references.Contains("DOTween.Modules"), "UIKit DOTween 集成不应硬引用可选 Modules 程序集。");
            Assert.IsTrue(asmdef.Contains("YOKIFRAME_DOTWEEN_SUPPORT"), "UIKit DOTween 集成必须受 DOTween 宏保护。");
        }

        /// <summary>
        /// 验证 UIKit Runtime 与 Editor 源码都由整文件宿主宏保护。
        /// </summary>
        [Test]
        public void UIKitSourcesUseWholeFileHostGuards()
        {
            string kitRoot = Path.Combine(Application.dataPath, "YokiFrame", "Tools", "UIKit", "Adapters", "Unity");
            AssertSourceTreeGuard(Path.Combine(kitRoot, "Runtime"), "#if UNITY_2022_3_OR_NEWER");
            AssertSourceTreeGuard(Path.Combine(kitRoot, "Editor"), "#if UNITY_EDITOR");
        }

        /// <summary>
        /// 读取当前项目中的文本文件。
        /// </summary>
        /// <param name="assetPath">Unity 项目相对路径。</param>
        /// <returns>文件完整文本。</returns>
        private static string ReadProjectFile(string assetPath)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "..", assetPath));
        }

        /// <summary>
        /// 验证目录中每个 C# 文件都使用指定整文件宏。
        /// </summary>
        /// <param name="sourceRoot">待扫描源码根。</param>
        /// <param name="expectedGuard">期望的第一条有效行。</param>
        private static void AssertSourceTreeGuard(string sourceRoot, string expectedGuard)
        {
            string[] sourcePaths = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories);
            Assert.Greater(sourcePaths.Length, 0, "UIKit 源码目录不能为空: " + sourceRoot);
            for (var index = 0; index < sourcePaths.Length; index++)
            {
                AssertWholeFileGuard(sourcePaths[index], expectedGuard);
            }
        }
    }
}
