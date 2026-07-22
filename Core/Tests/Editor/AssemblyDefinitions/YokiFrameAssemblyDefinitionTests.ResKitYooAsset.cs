using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        private const string RESKIT_YOOASSET_ASMDEF_PATH =
            "Assets/YokiFrame/Core/Integrations/Unity/ResKit/YooAsset/Runtime/YokiFrame.Unity.ResKit.YooAsset.asmdef";
        private const string YOOASSET_PACKAGE_NAME = "com.tuyoogame.yooasset";
        private const string YOOASSET_V2_OR_V3_RANGE = "[2.3.0,4.0.0)";
        private const string YOOASSET_V3_RANGE = "[3.0.0-beta,4.0.0)";
        private const string YOOASSET_V2_OR_V3_DEFINE = "YOKIFRAME_YOOASSET_2_OR_3";
        private const string YOOASSET_V3_DEFINE = "YOKIFRAME_YOOASSET_3";
        private const string YOOASSET_SUPPORT_DEFINE = "YOKIFRAME_YOOASSET_SUPPORT";

        /// <summary>
        /// 验证 YooAsset Integration 对 2.3.0 至 4.0.0 之前的 V2/V3 版本统一启用兼容宏。
        /// </summary>
        [Test]
        public void ResKitYooAssetAssemblyDefinesV2AndV3CompatibilityRange()
        {
            var document = ReadResKitYooAssetAssemblyDefinition();
            var versionDefine = FindVersionDefine(document, YOOASSET_V2_OR_V3_DEFINE);

            Assert.IsNotNull(versionDefine, "YooAsset asmdef 缺少 V2/V3 共用版本宏定义。");
            Assert.AreEqual(YOOASSET_PACKAGE_NAME, versionDefine.name, "V2/V3 版本宏必须绑定 YooAsset 包名。");
            Assert.AreEqual(YOOASSET_V2_OR_V3_RANGE, versionDefine.expression, "V2/V3 兼容范围必须从 YooAsset 2.3.0 开始并排除 4.0.0。");
        }

        /// <summary>
        /// 验证 YooAsset 3.x 具有独立版本宏，确保 V2 与 V3 API 差异可以在单一程序集内隔离。
        /// </summary>
        [Test]
        public void ResKitYooAssetAssemblyDefinesV3SpecificRange()
        {
            var document = ReadResKitYooAssetAssemblyDefinition();
            var versionDefine = FindVersionDefine(document, YOOASSET_V3_DEFINE);

            Assert.IsNotNull(versionDefine, "YooAsset asmdef 缺少 V3 专属版本宏定义。");
            Assert.AreEqual(YOOASSET_PACKAGE_NAME, versionDefine.name, "V3 版本宏必须绑定 YooAsset 包名。");
            Assert.AreEqual(YOOASSET_V3_RANGE, versionDefine.expression, "V3 版本宏范围必须从 3.0.0-beta 开始并排除 4.0.0。");
        }

        /// <summary>
        /// 验证 YooAsset 存在性宏和 V2/V3 兼容宏均属于程序集约束，保持依赖自动检测下的软依赖行为。
        /// </summary>
        [Test]
        public void ResKitYooAssetAssemblyKeepsSoftDependencyConstraints()
        {
            var document = ReadResKitYooAssetAssemblyDefinition();

            Assert.IsNotNull(document.defineConstraints, "YooAsset asmdef 必须声明 defineConstraints。");
            CollectionAssert.Contains(document.defineConstraints, YOOASSET_SUPPORT_DEFINE, "YooAsset Integration 必须受存在性宏保护。");
            CollectionAssert.Contains(document.defineConstraints, YOOASSET_V2_OR_V3_DEFINE, "YooAsset Integration 必须受 V2/V3 兼容宏保护。");
        }

        /// <summary>
        /// 读取并反序列化 YooAsset Integration 的 asmdef，统一处理测试中的文件路径和 JSON 校验。
        /// </summary>
        /// <returns>已解析的程序集定义文档。</returns>
        private static AssemblyDefinitionDocument ReadResKitYooAssetAssemblyDefinition()
        {
            var fullPath = Path.Combine(Application.dataPath, "..", RESKIT_YOOASSET_ASMDEF_PATH);
            Assert.IsTrue(File.Exists(fullPath), "缺少 YooAsset Integration asmdef: " + RESKIT_YOOASSET_ASMDEF_PATH);

            var json = File.ReadAllText(fullPath);
            var document = JsonUtility.FromJson<AssemblyDefinitionDocument>(json);
            Assert.IsNotNull(document, "YooAsset Integration asmdef 不是有效 JSON: " + RESKIT_YOOASSET_ASMDEF_PATH);
            return document;
        }

        /// <summary>
        /// 从 asmdef 版本宏列表中查找指定宏，便于分别断言版本范围和包名。
        /// </summary>
        /// <param name="document">已解析的程序集定义文档。</param>
        /// <param name="define">待查找的宏名称。</param>
        /// <returns>匹配的版本宏；不存在时返回 null。</returns>
        private static VersionDefine FindVersionDefine(AssemblyDefinitionDocument document, string define)
        {
            Assert.IsNotNull(document.versionDefines, "YooAsset asmdef 必须声明 versionDefines。");
            for (var index = 0; index < document.versionDefines.Length; index++)
            {
                var versionDefine = document.versionDefines[index];
                if (versionDefine != null && string.Equals(versionDefine.define, define, StringComparison.Ordinal))
                {
                    return versionDefine;
                }
            }

            return null;
        }

        /// <summary>承载 asmdef JSON 中与本回归测试相关的字段。</summary>
        [Serializable]
        private sealed class AssemblyDefinitionDocument
        {
            public string[] defineConstraints = Array.Empty<string>();
            public VersionDefine[] versionDefines = Array.Empty<VersionDefine>();
        }

        /// <summary>承载 Unity asmdef versionDefines 数组中的单项版本宏信息。</summary>
        [Serializable]
        private sealed class VersionDefine
        {
            public string name = string.Empty;
            public string expression = string.Empty;
            public string define = string.Empty;
        }
    }
}
