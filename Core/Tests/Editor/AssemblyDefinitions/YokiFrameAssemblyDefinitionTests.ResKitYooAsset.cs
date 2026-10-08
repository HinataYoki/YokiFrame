using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using JsonDocument = YokiFrame.Json.JsonDocument;
using JsonElement = YokiFrame.Json.JsonElement;
using JsonValueKind = YokiFrame.Json.JsonValueKind;

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

        /// <summary>验证 YooAsset Integration 对 2.3.0 至 4.0.0 之前的版本声明共用兼容宏。</summary>
        [Test]
        public void AssemblyBoundary_YooAsset集成_声明V2与V3兼容版本范围()
        {
            using (JsonDocument document = ParseAsmdefFile(ResolveAsmdefPath(RESKIT_YOOASSET_ASMDEF_PATH)))
            {
                JsonElement versionDefine = FindVersionDefine(document.RootElement, YOOASSET_V2_OR_V3_DEFINE);
                Assert.AreEqual(
                    YOOASSET_PACKAGE_NAME,
                    ReadRequiredString(versionDefine, "name", RESKIT_YOOASSET_ASMDEF_PATH));
                Assert.AreEqual(
                    YOOASSET_V2_OR_V3_RANGE,
                    ReadRequiredString(versionDefine, "expression", RESKIT_YOOASSET_ASMDEF_PATH));
            }
        }

        /// <summary>验证 YooAsset 3.x 具有独立版本宏，使 V2 与 V3 API 差异留在同一程序集内。</summary>
        [Test]
        public void AssemblyBoundary_YooAsset集成_声明V3专属版本范围()
        {
            using (JsonDocument document = ParseAsmdefFile(ResolveAsmdefPath(RESKIT_YOOASSET_ASMDEF_PATH)))
            {
                JsonElement versionDefine = FindVersionDefine(document.RootElement, YOOASSET_V3_DEFINE);
                Assert.AreEqual(
                    YOOASSET_PACKAGE_NAME,
                    ReadRequiredString(versionDefine, "name", RESKIT_YOOASSET_ASMDEF_PATH));
                Assert.AreEqual(
                    YOOASSET_V3_RANGE,
                    ReadRequiredString(versionDefine, "expression", RESKIT_YOOASSET_ASMDEF_PATH));
            }
        }

        /// <summary>验证 YooAsset 程序集同时受存在性宏和 V2/V3 兼容宏约束。</summary>
        [Test]
        public void AssemblyBoundary_YooAsset集成_软依赖约束包含存在性与兼容宏()
        {
            using (JsonDocument document = ParseAsmdefFile(ResolveAsmdefPath(RESKIT_YOOASSET_ASMDEF_PATH)))
            {
                JsonElement root = document.RootElement;
                Assert.IsTrue(
                    ArrayHasExactString(root, "defineConstraints", YOOASSET_SUPPORT_DEFINE),
                    "YooAsset Integration 必须受存在性宏保护。");
                Assert.IsTrue(
                    ArrayHasExactString(root, "defineConstraints", YOOASSET_V2_OR_V3_DEFINE),
                    "YooAsset Integration 必须受 V2/V3 兼容宏保护。");
            }
        }

        /// <summary>把项目内 asmdef 路径解析为绝对路径。</summary>
        /// <param name="assetPath">Unity 项目内 asmdef 路径。</param>
        /// <returns>绝对路径。</returns>
        private static string ResolveAsmdefPath(string assetPath)
        {
            return Path.Combine(Application.dataPath, "..", assetPath);
        }

        /// <summary>按 define 精确查找 versionDefines 条目。</summary>
        /// <param name="root">asmdef 根对象。</param>
        /// <param name="define">版本宏名称。</param>
        /// <returns>匹配的版本宏对象。</returns>
        private static JsonElement FindVersionDefine(JsonElement root, string define)
        {
            JsonElement versionDefines;
            Assert.IsTrue(root.TryGetProperty("versionDefines", out versionDefines), "YooAsset asmdef 必须声明 versionDefines。");
            Assert.AreEqual(JsonValueKind.Array, versionDefines.ValueKind, "versionDefines");
            for (int index = 0; index < versionDefines.GetArrayLength(); index++)
            {
                JsonElement item = versionDefines[index];
                if (TextEquals(item, "define", define))
                {
                    return item;
                }
            }

            Assert.Fail("YooAsset asmdef 缺少版本宏: " + define);
            return default;
        }
    }
}
