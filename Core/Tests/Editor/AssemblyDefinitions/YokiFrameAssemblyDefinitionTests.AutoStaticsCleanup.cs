using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using JsonDocument = YokiFrame.Json.JsonDocument;

namespace YokiFrame
{
    /// <summary>
    /// 约束包内每个程序集用同目录 globalconfig 关闭 UAL0010/UAL0013。
    /// Unity 7000 关闭域重载后要求静态类型标注生命周期特性；包最低支持 2022.3，不能无条件引用。
    /// </summary>
    public sealed partial class YokiFrameAssemblyDefinitionTests
    {
        private const string AUTO_STATICS_ANALYSIS_KEY = "build_property.UnityEnableAutoStaticsCleanupAnalysis";
        private const string AUTO_STATICS_CODEGEN_KEY = "build_property.UnityEnableAutoStaticsCleanupCodeGen";
        private const string UAL0010_SEVERITY_KEY = "dotnet_diagnostic.UAL0010.severity";
        private const string UAL0013_SEVERITY_KEY = "dotnet_diagnostic.UAL0013.severity";

        /// <summary>
        /// 验证包内每个程序集都关闭自动静态清理分析，并抑制 UAL0010/UAL0013。
        /// 不逐类型添加 Unity 生命周期特性，避免 2022.3 无法编译。
        /// </summary>
        [Test]
        public void SourceGuard_包内程序集_关闭自动静态清理分析()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string[] asmdefPaths = Directory.GetFiles(packageRoot, "*.asmdef", SearchOption.AllDirectories);
            int assemblyCount = 0;
            for (int index = 0; index < asmdefPaths.Length; index++)
            {
                if (IsHiddenPackagePath(asmdefPaths[index]))
                {
                    continue;
                }

                assemblyCount++;
                AssertAutoStaticsAnalysisDisabled(asmdefPaths[index]);
            }

            Assert.Greater(assemblyCount, 0, "包内必须存在程序集定义。");
        }

        /// <summary>
        /// 跳过 Unity 不导入的 ~ 目录，避免工具链源码混入程序集扫描。
        /// </summary>
        /// <param name="fullPath">待判断的绝对路径。</param>
        /// <returns>路径中任一目录以 ~ 结尾时返回 true。</returns>
        private static bool IsHiddenPackagePath(string fullPath)
        {
            string[] segments = NormalizePath(fullPath).Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                if (segments[index].EndsWith("~", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 断言程序集目录只有一份同名 globalconfig，分析关闭、诊断抑制，代码生成保持开启。
        /// </summary>
        /// <param name="asmdefPath">asmdef 绝对路径。</param>
        private static void AssertAutoStaticsAnalysisDisabled(string asmdefPath)
        {
            string assemblyName = ReadAsmdefName(asmdefPath);
            string directory = Path.GetDirectoryName(asmdefPath);
            string configPath = Path.Combine(directory, assemblyName + ".globalconfig");
            string[] configs = Directory.GetFiles(directory, "*.globalconfig", SearchOption.TopDirectoryOnly);
            Assert.AreEqual(1, configs.Length, "程序集目录必须只有一份 globalconfig: " + NormalizePath(directory));
            Assert.IsTrue(File.Exists(configPath), "缺少与程序集同名的 globalconfig: " + assemblyName);
            Assert.IsTrue(File.Exists(configPath + ".meta"), "缺少 globalconfig meta: " + assemblyName);

            Dictionary<string, string> values = ReadGlobalConfig(configPath);
            string isGlobal;
            string codeGen;
            string analysis;
            string ual0010;
            string ual0013;
            Assert.IsTrue(values.TryGetValue("is_global", out isGlobal), "globalconfig 缺少 is_global: " + assemblyName);
            Assert.IsTrue(values.TryGetValue(AUTO_STATICS_CODEGEN_KEY, out codeGen), "globalconfig 缺少代码生成开关: " + assemblyName);
            Assert.IsTrue(values.TryGetValue(AUTO_STATICS_ANALYSIS_KEY, out analysis), "globalconfig 缺少分析开关: " + assemblyName);
            Assert.IsTrue(values.TryGetValue(UAL0010_SEVERITY_KEY, out ual0010), "globalconfig 缺少 UAL0010 严重级别: " + assemblyName);
            Assert.IsTrue(values.TryGetValue(UAL0013_SEVERITY_KEY, out ual0013), "globalconfig 缺少 UAL0013 严重级别: " + assemblyName);
            Assert.AreEqual("true", isGlobal, "is_global 必须为 true: " + assemblyName);
            Assert.AreEqual("true", codeGen, "代码生成必须保持开启: " + assemblyName);
            Assert.AreEqual("false", analysis, "该程序集必须关闭自动静态清理分析: " + assemblyName);
            Assert.AreEqual("none", ual0010, "UAL0010 必须抑制: " + assemblyName);
            Assert.AreEqual("none", ual0013, "UAL0013 必须抑制: " + assemblyName);
        }

        /// <summary>
        /// 读取 asmdef 的 name 字段。
        /// </summary>
        /// <param name="fullPath">asmdef 绝对路径。</param>
        /// <returns>程序集名。</returns>
        private static string ReadAsmdefName(string fullPath)
        {
            using (JsonDocument document = ParseAsmdefFile(fullPath))
            {
                return ReadRequiredString(document.RootElement, "name", NormalizePath(fullPath));
            }
        }

        /// <summary>
        /// 读取 globalconfig 的键值。注释和空行忽略，重复键视为无效。
        /// </summary>
        /// <param name="fullPath">globalconfig 绝对路径。</param>
        /// <returns>按原文大小写保存的键值。</returns>
        private static Dictionary<string, string> ReadGlobalConfig(string fullPath)
        {
            string[] lines = File.ReadAllLines(fullPath);
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length == 0 || line[0] == '#')
                {
                    continue;
                }

                int separator = line.IndexOf('=');
                Assert.Greater(separator, 0, "globalconfig 行缺少等号: " + NormalizePath(fullPath));
                string key = line.Substring(0, separator).Trim();
                string value = line.Substring(separator + 1).Trim();
                Assert.IsFalse(values.ContainsKey(key), "globalconfig 键重复: " + key + " " + NormalizePath(fullPath));
                values.Add(key, value);
            }

            return values;
        }
    }
}
