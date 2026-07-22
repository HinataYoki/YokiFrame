using System;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity MonoSingleton 适配层的目录、宏边界和公开入口。
    /// </summary>
    public sealed class YokiFrameUnitySingletonKitAdapterTests
    {
        private const string UNITY_DEFINE = "#if UNITY_5_3_OR_NEWER";

        /// <summary>
        /// 验证 Unity SingletonKit 适配层位于 Unity Runtime Adapter 目录并使用单一 Unity 宏包裹。
        /// </summary>
        [Test]
        public void UnitySingletonAdapterSourcesLiveUnderUnityRuntimeAdapter()
        {
            string adapterRoot = GetUnitySingletonAdapterRoot();

            Assert.IsTrue(Directory.Exists(adapterRoot), "缺少 Unity SingletonKit Runtime Adapter 目录。");
            AssertUnityAdapterFile(Path.Combine(adapterRoot, "MonoSingleton.cs"));
            AssertUnityAdapterFile(Path.Combine(adapterRoot, "MonoSingletonPathAttribute.cs"));
        }

        /// <summary>
        /// 验证 MonoSingleton 泛型基类被编入 Unity Runtime Adapter 程序集。
        /// </summary>
        [Test]
        public void MonoSingletonTypeExistsInUnityRuntimeAssembly()
        {
            Type type = Type.GetType("YokiFrame.MonoSingleton`1, YokiFrame.Unity.Runtime");

            Assert.IsNotNull(type, "缺少 MonoSingleton<T>，或命名空间 / 程序集未按新架构暴露。");
            Assert.IsTrue(type.IsAbstract);
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(type));
        }

        /// <summary>
        /// 验证 MonoSingleton 提供旧版常用的 Instance、HasInstance、TryGetInstance 和 Dispose 入口。
        /// </summary>
        [Test]
        public void MonoSingletonExposesExpectedStaticLifecycleApi()
        {
            Type type = Type.GetType("YokiFrame.MonoSingleton`1, YokiFrame.Unity.Runtime");

            Assert.IsNotNull(type);
            Assert.IsNotNull(type.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(type.GetProperty("HasInstance", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(type.GetMethod("TryGetInstance", BindingFlags.Public | BindingFlags.Static));
            Assert.IsNotNull(type.GetMethod("Dispose", BindingFlags.Public | BindingFlags.Static));
        }

        /// <summary>
        /// 获取 Unity SingletonKit Runtime Adapter 的绝对目录路径。
        /// </summary>
        /// <returns>Unity SingletonKit Runtime Adapter 目录。</returns>
        private static string GetUnitySingletonAdapterRoot()
        {
            return Path.Combine(Application.dataPath, "YokiFrame", "Core", "Adapters", "Unity", "Runtime", "SingletonKit");
        }

        /// <summary>
        /// 验证 Unity 适配源码存在并由单一 Unity 环境宏包裹。
        /// </summary>
        /// <param name="sourcePath">待验证源码路径。</param>
        private static void AssertUnityAdapterFile(string sourcePath)
        {
            Assert.IsTrue(File.Exists(sourcePath), "缺少 Unity SingletonKit 适配源码: " + sourcePath);

            string[] lines = File.ReadAllLines(sourcePath);
            Assert.AreEqual(UNITY_DEFINE, FirstNonEmptyLine(lines), "Unity 适配源码必须从 Unity 环境宏开始: " + sourcePath);
            Assert.AreEqual("#endif", LastNonEmptyLine(lines), "Unity 适配源码必须用 #endif 结束: " + sourcePath);

            string source = File.ReadAllText(sourcePath);
            Assert.IsFalse(source.Contains("#if !GODOT"), "Unity 适配层不能用排除 Godot 的宽泛宏: " + sourcePath);
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
