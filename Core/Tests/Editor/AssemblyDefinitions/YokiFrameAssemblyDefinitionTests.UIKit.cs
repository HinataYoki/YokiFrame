using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 UIKit 只保留 Unity 适配程序集，并且可选 DOTween 集成不硬引用 Modules。
    /// </summary>
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        private const string UIKIT_RUNTIME_ASMDEF_PATH =
            "Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/YokiFrame.UIKit.Unity.asmdef";
        private const string UIKIT_EDITOR_ASMDEF_PATH =
            "Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Editor/YokiFrame.UIKit.Unity.Editor.asmdef";
        private const string UIKIT_DOTWEEN_ASMDEF_PATH =
            "Assets/YokiFrame/Tools/UIKit/Integrations/Unity/DOTween/Runtime/YokiFrame.UIKit.DOTween.asmdef";
        private const string UIKIT_DOTWEEN_MODULES_REFERENCE = "DOTween.Modules";
        private const string UIKIT_DOTWEEN_SUPPORT_DEFINE = "YOKIFRAME_DOTWEEN_SUPPORT";

        /// <summary>
        /// 验证 UIKit 只提供 Unity Runtime 与 Unity Editor 程序集，不创建跨宿主 Runtime 或 Godot Adapter。
        /// </summary>
        [Test]
        public void AssemblyBoundary_UIKit程序集_只有Unity适配且没有跨宿主根()
        {
            AssertAssembly(UIKIT_RUNTIME_ASMDEF_PATH, "YokiFrame.UIKit.Unity", string.Empty, false);
            AssertAssembly(UIKIT_EDITOR_ASMDEF_PATH, "YokiFrame.UIKit.Unity.Editor", string.Empty, false);
            string kitRoot = Path.Combine(Application.dataPath, "YokiFrame", "Tools", "UIKit");
            Assert.IsFalse(Directory.Exists(Path.Combine(kitRoot, "Runtime")), "UIKit 禁止创建跨宿主 Runtime 根。");
            Assert.IsFalse(Directory.Exists(Path.Combine(kitRoot, "Adapters", "Godot")), "UIKit 禁止创建 Godot Adapter。");
        }

        /// <summary>
        /// 验证 UIKit Runtime 的引用正好是 Core、Unity Runtime 和可缺失的 UniTask。
        /// </summary>
        [Test]
        public void AssemblyBoundary_UIKitRuntime_只依赖Core与UnityRuntime和UniTask()
        {
            AssertReferencesEqual(
                UIKIT_RUNTIME_ASMDEF_PATH,
                "YokiFrame",
                "YokiFrame.Unity.Runtime",
                "UniTask");
        }

        /// <summary>
        /// 验证 UIKit DOTween 集成不把可选 Modules 程序集写入 references，并且受 DOTween 存在性宏约束。
        /// </summary>
        [Test]
        public void AssemblyBoundary_UIKitDOTween集成_不硬引用Modules且受存在性宏约束()
        {
            string[] references = ReadStringArrayProperty(UIKIT_DOTWEEN_ASMDEF_PATH, "references");
            Assert.IsFalse(
                HasReference(references, UIKIT_DOTWEEN_MODULES_REFERENCE),
                "UIKit DOTween 集成不应硬引用可选 Modules 程序集。");
            string[] defines = ReadStringArrayProperty(UIKIT_DOTWEEN_ASMDEF_PATH, "defineConstraints");
            Assert.IsTrue(
                HasReference(defines, UIKIT_DOTWEEN_SUPPORT_DEFINE),
                "UIKit DOTween 集成必须受 DOTween 宏保护。");
        }

        /// <summary>
        /// 验证 UIKit Unity Runtime 与 Editor 源码都由整文件宿主宏保护。
        /// </summary>
        [Test]
        public void SourceGuard_UIKitUnity适配源码_整文件使用宿主宏()
        {
            string kitRoot = Path.Combine(Application.dataPath, "YokiFrame", "Tools", "UIKit", "Adapters", "Unity");
            AssertSourceTreeGuard(Path.Combine(kitRoot, "Runtime"), "#if UNITY_2022_3_OR_NEWER");
            AssertSourceTreeGuard(Path.Combine(kitRoot, "Editor"), "#if UNITY_EDITOR");
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
            for (int index = 0; index < sourcePaths.Length; index++)
            {
                AssertWholeFileGuard(sourcePaths[index], expectedGuard);
            }
        }
    }
}
