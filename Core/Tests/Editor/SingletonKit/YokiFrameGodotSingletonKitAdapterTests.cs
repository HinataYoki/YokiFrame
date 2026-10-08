using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Godot SingletonKit 适配源码位于 Godot Runtime Adapter，并由整文件 GODOT 宏包裹。
    /// </summary>
    public sealed class YokiFrameGodotSingletonKitAdapterTests
    {
        private const string GODOT_DEFINE = "#if GODOT";
        private const string ENDIF_DIRECTIVE = "#endif";

        /// <summary>
        /// 验证 GodotSingleton 源码从 GODOT 宏开始，并以 #endif 结束。
        /// </summary>
        [Test]
        public void SourceGuard_Godot适配源码_整文件使用Godot宏()
        {
            string sourcePath = Path.Combine(GetGodotSingletonAdapterRoot(), "GodotSingleton.cs");

            Assert.IsTrue(File.Exists(sourcePath), "缺少 Godot SingletonKit 适配源码: " + sourcePath);

            string[] lines = File.ReadAllLines(sourcePath);
            Assert.AreEqual(GODOT_DEFINE, FirstNonEmptyLine(lines), "Godot 适配源码必须从 GODOT 宏开始: " + sourcePath);
            Assert.AreEqual(ENDIF_DIRECTIVE, LastNonEmptyLine(lines), "Godot 适配源码必须用 #endif 结束: " + sourcePath);
        }

        /// <summary>
        /// 获取 Godot SingletonKit Runtime Adapter 的绝对目录路径。
        /// </summary>
        /// <returns>Godot SingletonKit Runtime Adapter 目录。</returns>
        private static string GetGodotSingletonAdapterRoot()
        {
            return Path.Combine(Application.dataPath, "YokiFrame", "Core", "Adapters", "Godot", "Runtime", "SingletonKit");
        }

        /// <summary>
        /// 从行数组中获取第一条非空行。
        /// </summary>
        /// <param name="lines">源码行数组。</param>
        /// <returns>第一条非空行；不存在时返回空字符串。</returns>
        private static string FirstNonEmptyLine(string[] lines)
        {
            for (var index = 0; index < lines.Length; index++)
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
        /// 从行数组中获取最后一条非空行。
        /// </summary>
        /// <param name="lines">源码行数组。</param>
        /// <returns>最后一条非空行；不存在时返回空字符串。</returns>
        private static string LastNonEmptyLine(string[] lines)
        {
            for (var index = lines.Length - 1; index >= 0; index--)
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
