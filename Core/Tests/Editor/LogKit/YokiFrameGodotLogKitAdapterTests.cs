using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Godot LogKit 适配源码整文件位于 GODOT 宏内，避免 Unity 工程编译 Godot API。
    /// </summary>
    public sealed class YokiFrameGodotLogKitAdapterTests
    {
        private const string GODOT_DEFINE = "#if GODOT";

        /// <summary>
        /// 验证三个 Godot LogKit 适配文件都从 GODOT 宏开始，并以 endif 结束。
        /// </summary>
        [Test]
        public void SourceGuard_GodotLogKit适配源码_整文件使用GODOT宏()
        {
            string adapterRoot = GetGodotLogKitAdapterRoot();
            Assert.IsTrue(Directory.Exists(adapterRoot), "缺少 Godot LogKit Runtime Adapter 目录。");
            AssertGodotAdapterFile(Path.Combine(adapterRoot, "GodotEngineLogger.cs"));
            AssertGodotAdapterFile(Path.Combine(adapterRoot, "GodotLogKitRuntimeInstaller.cs"));
            AssertGodotAdapterFile(Path.Combine(adapterRoot, "GodotLogKitPlayerOverlay.cs"));
        }

        /// <summary>
        /// 获取 Godot LogKit Runtime Adapter 的绝对目录路径。
        /// </summary>
        /// <returns>Godot LogKit Runtime Adapter 目录。</returns>
        private static string GetGodotLogKitAdapterRoot()
        {
            return Path.Combine(Application.dataPath, "YokiFrame", "Core", "Adapters", "Godot", "Runtime", "LogKit");
        }

        /// <summary>
        /// 验证单个 Godot 适配源码存在，并且首尾非空行构成整文件 GODOT 宏。
        /// </summary>
        /// <param name="sourcePath">待验证的源码文件路径。</param>
        private static void AssertGodotAdapterFile(string sourcePath)
        {
            Assert.IsTrue(File.Exists(sourcePath), "缺少 Godot LogKit 适配源码: " + sourcePath);

            string[] lines = File.ReadAllLines(sourcePath);
            Assert.AreEqual(GODOT_DEFINE, FirstNonEmptyLine(lines), sourcePath);
            Assert.AreEqual("#endif", LastNonEmptyLine(lines), sourcePath);
        }

        /// <summary>
        /// 读取第一条非空源码行，用于整文件宏守卫。
        /// </summary>
        /// <param name="lines">源码行。</param>
        /// <returns>第一条非空行。</returns>
        private static string FirstNonEmptyLine(string[] lines)
        {
            for (int index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 读取最后一条非空源码行，用于整文件宏守卫。
        /// </summary>
        /// <param name="lines">源码行。</param>
        /// <returns>最后一条非空行。</returns>
        private static string LastNonEmptyLine(string[] lines)
        {
            for (int index = lines.Length - 1; index >= 0; index--)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }
    }
}
