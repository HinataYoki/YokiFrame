using System;
using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using JsonDocument = YokiFrame.Json.JsonDocument;
using JsonElement = YokiFrame.Json.JsonElement;
using JsonValueKind = YokiFrame.Json.JsonValueKind;

namespace YokiFrame
{
    /// <summary>
    /// 用结构化 JSON 和 meta GUID 校验 asmdef，避免用文本子串冒充程序集边界。
    /// </summary>
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        /// <summary>
        /// 读取指定 asmdef 和 meta，并校验程序集名、GUID 和 Runtime 引擎依赖开关。
        /// </summary>
        /// <param name="assetPath">Unity 项目内 asmdef 路径。</param>
        /// <param name="assemblyName">期望程序集名。</param>
        /// <param name="expectedGuid">期望旧 GUID；为空时只验证 meta 存在。</param>
        /// <param name="expectNoEngineReferences">为 true 时要求 noEngineReferences 为 true；为 false 时不检查该字段。</param>
        private static void AssertAssembly(string assetPath, string assemblyName, string expectedGuid, bool expectNoEngineReferences)
        {
            AssertAssembly(assetPath, assemblyName, expectedGuid, expectNoEngineReferences, "YokiFrame");
        }

        /// <summary>
        /// 读取指定 asmdef 和 meta，并校验程序集名、root namespace、GUID 和 Runtime 引擎依赖开关。
        /// </summary>
        /// <param name="assetPath">Unity 项目内 asmdef 路径。</param>
        /// <param name="assemblyName">期望程序集名。</param>
        /// <param name="expectedGuid">期望旧 GUID；为空时只验证 meta 存在。</param>
        /// <param name="expectNoEngineReferences">为 true 时要求 noEngineReferences 为 true；为 false 时不检查该字段。</param>
        /// <param name="expectedRootNamespace">期望 root namespace。</param>
        private static void AssertAssembly(
            string assetPath,
            string assemblyName,
            string expectedGuid,
            bool expectNoEngineReferences,
            string expectedRootNamespace)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            string metaPath = fullPath + ".meta";
            Assert.IsTrue(File.Exists(fullPath), "缺少 asmdef: " + assetPath);
            Assert.IsTrue(File.Exists(metaPath), "缺少 asmdef meta: " + assetPath + ".meta");

            using (JsonDocument document = ParseAsmdefFile(fullPath))
            {
                JsonElement root = document.RootElement;
                Assert.AreEqual(assemblyName, ReadRequiredString(root, "name", assetPath), "程序集名不匹配: " + assetPath);
                Assert.AreEqual(
                    expectedRootNamespace,
                    ReadRequiredString(root, "rootNamespace", assetPath),
                    "rootNamespace 必须保持 " + expectedRootNamespace + ": " + assetPath);
                if (expectNoEngineReferences)
                {
                    Assert.IsTrue(
                        ReadRequiredBoolean(root, "noEngineReferences", assetPath),
                        "Runtime asmdef 不应引用 UnityEngine: " + assetPath);
                }
            }

            if (!string.IsNullOrEmpty(expectedGuid))
            {
                Assert.AreEqual(expectedGuid, ReadMetaGuid(metaPath), "旧版 GUID 未保留: " + assetPath);
            }
        }

        /// <summary>
        /// 断言 includePlatforms 与期望平台列表完全一致。空列表表示不按平台排除。
        /// </summary>
        /// <param name="assetPath">Unity 项目内 asmdef 路径。</param>
        /// <param name="expected">按顺序期望的平台名。</param>
        private static void AssertIncludePlatformsEqual(string assetPath, params string[] expected)
        {
            string[] actual = ReadStringArrayProperty(assetPath, "includePlatforms");
            int expectedLength = expected == null ? 0 : expected.Length;
            Assert.AreEqual(expectedLength, actual.Length, "includePlatforms 数量不匹配: " + assetPath);
            for (int index = 0; index < expectedLength; index++)
            {
                Assert.AreEqual(expected[index], actual[index], "includePlatforms 不匹配: " + assetPath);
            }
        }

        /// <summary>
        /// 断言 references 与期望列表完全一致，顺序也属于契约。
        /// </summary>
        /// <param name="assetPath">Unity 项目内 asmdef 路径。</param>
        /// <param name="expected">按顺序期望的引用。</param>
        private static void AssertReferencesEqual(string assetPath, params string[] expected)
        {
            string[] actual = ReadStringArrayProperty(assetPath, "references");
            int expectedLength = expected == null ? 0 : expected.Length;
            Assert.AreEqual(expectedLength, actual.Length, "程序集引用数量不匹配: " + assetPath);
            for (int index = 0; index < expectedLength; index++)
            {
                Assert.AreEqual(expected[index], actual[index], "程序集引用不匹配: " + assetPath);
            }
        }

        /// <summary>
        /// 断言引用列表不包含按程序集名书写的 YokiFrame 依赖。GUID 引用不受影响。
        /// </summary>
        /// <param name="references">已解析的 references。</param>
        /// <param name="asmdefPath">失败信息中的 asmdef 路径。</param>
        private static void AssertNoNamedYokiFrameReference(string[] references, string asmdefPath)
        {
            for (int index = 0; index < references.Length; index++)
            {
                string reference = references[index] ?? string.Empty;
                Assert.IsFalse(
                    reference.StartsWith("YokiFrame.", StringComparison.Ordinal),
                    "Tool Runtime 禁止通过程序集名依赖其他 YokiFrame 程序集: " + NormalizePath(asmdefPath));
            }
        }

        /// <summary>
        /// 判断引用数组是否包含精确值。
        /// </summary>
        /// <param name="references">已解析的 references。</param>
        /// <param name="expected">期望的完整引用字符串。</param>
        /// <returns>存在完全相同的引用时返回 true。</returns>
        private static bool HasReference(string[] references, string expected)
        {
            for (int index = 0; index < references.Length; index++)
            {
                if (string.Equals(references[index], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 读取项目内 asmdef 的字符串数组字段。
        /// </summary>
        /// <param name="assetPath">Unity 项目内 asmdef 路径。</param>
        /// <param name="propertyName">数组字段名。</param>
        /// <returns>字段中的字符串；字段缺失或类型不对时断言失败。</returns>
        private static string[] ReadStringArrayProperty(string assetPath, string propertyName)
        {
            string fullPath = Path.Combine(Application.dataPath, "..", assetPath);
            using (JsonDocument document = ParseAsmdefFile(fullPath))
            {
                return ReadStringArray(document.RootElement, propertyName, assetPath);
            }
        }

        /// <summary>
        /// 读取绝对路径 asmdef 的 references。
        /// </summary>
        /// <param name="fullPath">asmdef 绝对路径。</param>
        /// <returns>references 中的字符串。</returns>
        private static string[] ReadReferences(string fullPath)
        {
            using (JsonDocument document = ParseAsmdefFile(fullPath))
            {
                return ReadStringArray(document.RootElement, "references", NormalizePath(fullPath));
            }
        }

        /// <summary>
        /// 判断 asmdef 的已知字符串字段是否精确等于期望值。
        /// </summary>
        /// <param name="fullPath">asmdef 绝对路径。</param>
        /// <param name="expected">期望的完整字符串。</param>
        /// <returns>任一已知字段精确命中时返回 true。</returns>
        private static bool AsmdefContainsExactString(string fullPath, string expected)
        {
            using (JsonDocument document = ParseAsmdefFile(fullPath))
            {
                JsonElement root = document.RootElement;
                return TextEquals(root, "name", expected)
                    || TextEquals(root, "rootNamespace", expected)
                    || ArrayHasExactString(root, "references", expected)
                    || ArrayHasExactString(root, "includePlatforms", expected)
                    || ArrayHasExactString(root, "excludePlatforms", expected)
                    || ArrayHasExactString(root, "precompiledReferences", expected)
                    || ArrayHasExactString(root, "defineConstraints", expected)
                    || VersionDefinesContain(root, expected);
            }
        }

        /// <summary>
        /// 解析 asmdef 文件。调用方负责释放文档。
        /// </summary>
        /// <param name="fullPath">asmdef 绝对路径。</param>
        /// <returns>已解析的 JSON 文档。</returns>
        private static JsonDocument ParseAsmdefFile(string fullPath)
        {
            Assert.IsTrue(File.Exists(fullPath), "缺少 asmdef: " + NormalizePath(fullPath));
            return JsonDocument.Parse(File.ReadAllText(fullPath));
        }

        /// <summary>
        /// 读取必需字符串字段。
        /// </summary>
        /// <param name="root">asmdef 根对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <param name="assetPath">失败信息中的路径。</param>
        /// <returns>字段文本。</returns>
        private static string ReadRequiredString(JsonElement root, string propertyName, string assetPath)
        {
            JsonElement value;
            Assert.IsTrue(root.TryGetProperty(propertyName, out value), "asmdef 缺少 " + propertyName + ": " + assetPath);
            Assert.AreEqual(JsonValueKind.String, value.ValueKind, propertyName + " 必须是字符串: " + assetPath);
            return value.GetString();
        }

        /// <summary>
        /// 读取必需布尔字段。
        /// </summary>
        /// <param name="root">asmdef 根对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <param name="assetPath">失败信息中的路径。</param>
        /// <returns>字段布尔值。</returns>
        private static bool ReadRequiredBoolean(JsonElement root, string propertyName, string assetPath)
        {
            JsonElement value;
            Assert.IsTrue(root.TryGetProperty(propertyName, out value), "asmdef 缺少 " + propertyName + ": " + assetPath);
            Assert.IsTrue(
                value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False,
                propertyName + " 必须是布尔值: " + assetPath);
            return value.GetBoolean();
        }

        /// <summary>
        /// 读取字符串数组字段。
        /// </summary>
        /// <param name="root">asmdef 根对象。</param>
        /// <param name="propertyName">数组字段名。</param>
        /// <param name="assetPath">失败信息中的路径。</param>
        /// <returns>数组中的字符串。</returns>
        private static string[] ReadStringArray(JsonElement root, string propertyName, string assetPath)
        {
            JsonElement array;
            Assert.IsTrue(root.TryGetProperty(propertyName, out array), "asmdef 缺少 " + propertyName + ": " + assetPath);
            Assert.AreEqual(JsonValueKind.Array, array.ValueKind, propertyName + " 必须是数组: " + assetPath);
            int length = array.GetArrayLength();
            string[] values = new string[length];
            for (int index = 0; index < length; index++)
            {
                Assert.AreEqual(JsonValueKind.String, array[index].ValueKind, propertyName + " 含有非字符串元素: " + assetPath);
                values[index] = array[index].GetString();
            }

            return values;
        }

        /// <summary>
        /// 判断对象字段是否为指定字符串。
        /// </summary>
        /// <param name="root">JSON 对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <param name="expected">期望的完整字符串。</param>
        /// <returns>字段存在且精确相等时返回 true。</returns>
        private static bool TextEquals(JsonElement root, string propertyName, string expected)
        {
            JsonElement value;
            return root.TryGetProperty(propertyName, out value)
                && value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString(), expected, StringComparison.Ordinal);
        }

        /// <summary>
        /// 判断字符串数组是否包含精确值。
        /// </summary>
        /// <param name="root">JSON 对象。</param>
        /// <param name="propertyName">数组字段名。</param>
        /// <param name="expected">期望的完整字符串。</param>
        /// <returns>数组中存在精确值时返回 true；字段缺失时返回 false。</returns>
        private static bool ArrayHasExactString(JsonElement root, string propertyName, string expected)
        {
            JsonElement array;
            if (!root.TryGetProperty(propertyName, out array) || array.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            int length = array.GetArrayLength();
            for (int index = 0; index < length; index++)
            {
                JsonElement item = array[index];
                if (item.ValueKind == JsonValueKind.String
                    && string.Equals(item.GetString(), expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断 versionDefines 的 name、expression 或 define 是否精确等于期望值。
        /// </summary>
        /// <param name="root">asmdef 根对象。</param>
        /// <param name="expected">期望的完整字符串。</param>
        /// <returns>任一版本宏字段命中时返回 true。</returns>
        private static bool VersionDefinesContain(JsonElement root, string expected)
        {
            JsonElement versionDefines;
            if (!root.TryGetProperty("versionDefines", out versionDefines) || versionDefines.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            int length = versionDefines.GetArrayLength();
            for (int index = 0; index < length; index++)
            {
                JsonElement item = versionDefines[index];
                if (TextEquals(item, "name", expected)
                    || TextEquals(item, "expression", expected)
                    || TextEquals(item, "define", expected))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 读取 asmdef meta 的 guid 行。只接受独立的 guid 字段，不匹配更长文本中的子串。
        /// </summary>
        /// <param name="metaPath">meta 绝对路径。</param>
        /// <returns>guid 文本。</returns>
        private static string ReadMetaGuid(string metaPath)
        {
            string meta = File.ReadAllText(metaPath);
            Match match = Regex.Match(meta, "^guid:\\s*([a-f0-9]+)\\s*$", RegexOptions.Multiline);
            Assert.IsTrue(match.Success, "asmdef meta 缺少 guid: " + NormalizePath(metaPath));
            return match.Groups[1].Value;
        }
    }
}
