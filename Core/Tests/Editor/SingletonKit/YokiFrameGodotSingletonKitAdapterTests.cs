using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Godot Node 单例适配层的目录、宏边界和关键生命周期代码。
    /// </summary>
    public sealed class YokiFrameGodotSingletonKitAdapterTests
    {
        private const string GODOT_DEFINE = "#if GODOT";

        /// <summary>
        /// 验证 Godot SingletonKit 适配层位于 Godot Runtime Adapter 目录并使用 GODOT 宏包裹。
        /// </summary>
        [Test]
        public void GodotSingletonAdapterSourceLivesUnderGodotRuntimeAdapter()
        {
            string sourcePath = Path.Combine(GetGodotSingletonAdapterRoot(), "GodotSingleton.cs");

            Assert.IsTrue(File.Exists(sourcePath), "缺少 Godot SingletonKit 适配源码: " + sourcePath);

            string[] lines = File.ReadAllLines(sourcePath);
            Assert.AreEqual(GODOT_DEFINE, FirstNonEmptyLine(lines), "Godot 适配源码必须从 GODOT 宏开始: " + sourcePath);
            Assert.AreEqual("#endif", LastNonEmptyLine(lines), "Godot 适配源码必须用 #endif 结束: " + sourcePath);
        }

        /// <summary>
        /// 验证 GodotSingleton 提供 Node 生命周期和重复实例处理，便于 Autoload 或场景根节点接入。
        /// </summary>
        [Test]
        public void GodotSingletonUsesNodeLifecycleAndRegistry()
        {
            string source = File.ReadAllText(Path.Combine(GetGodotSingletonAdapterRoot(), "GodotSingleton.cs"));

            Assert.IsTrue(source.Contains("using Godot;"), "Godot 适配源码必须只在 GODOT 宏内引用 Godot API。");
            Assert.IsTrue(source.Contains("public abstract partial class GodotSingleton<T> : Node, ISingleton"), "GodotSingleton<T> 必须继承 Node 并实现 ISingleton。");
            Assert.IsTrue(source.Contains("public static T Instance"), "GodotSingleton<T> 必须提供 Instance 入口。");
            Assert.IsTrue(source.Contains("public override void _EnterTree()"), "GodotSingleton<T> 必须接入 _EnterTree。");
            Assert.IsTrue(source.Contains("public override void _ExitTree()"), "GodotSingleton<T> 必须接入 _ExitTree。");
            Assert.IsTrue(source.Contains("SingletonRegistry.Register"), "GodotSingleton<T> 必须登记到 SingletonRegistry。");
            Assert.IsTrue(source.Contains("QueueFree()"), "GodotSingleton<T> 必须释放重复或销毁实例。");
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
