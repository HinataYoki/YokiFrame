using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity MonoSingleton 适配层的编译边界，以及编辑模式下的创建与释放契约。
    /// </summary>
    public sealed class YokiFrameUnitySingletonKitAdapterTests
    {
        private const string UNITY_DEFINE = "#if UNITY_5_3_OR_NEWER";
        private const string ENDIF_DIRECTIVE = "#endif";
        private const string GODOT_EXCLUSION_DEFINE = "#if !GODOT";

        /// <summary>
        /// 每个测试前清除退出标记和探针实例，避免上一个用例残留。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            ResetProbe();
        }

        /// <summary>
        /// 每个测试后销毁探针，避免编辑模式对象留在场景中。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            ResetProbe();
        }

        /// <summary>
        /// 验证 Unity SingletonKit 适配源码整文件使用 Unity 宏，且不以排除 Godot 的宏代替。
        /// </summary>
        [Test]
        public void SourceGuard_Unity适配源码_整文件使用Unity宏且不排除Godot()
        {
            string adapterRoot = GetUnitySingletonAdapterRoot();

            Assert.IsTrue(Directory.Exists(adapterRoot), "缺少 Unity SingletonKit Runtime Adapter 目录。");
            AssertUnityAdapterFile(Path.Combine(adapterRoot, "MonoSingleton.cs"));
            AssertUnityAdapterFile(Path.Combine(adapterRoot, "MonoSingletonPathAttribute.cs"));
        }

        /// <summary>
        /// 验证 MonoSingleton 泛型基类位于 Unity Runtime 程序集，并且继承 MonoBehaviour。
        /// </summary>
        [Test]
        public void AssemblyBoundary_MonoSingleton_位于UnityRuntime且继承MonoBehaviour()
        {
            Type type = Type.GetType("YokiFrame.MonoSingleton`1, YokiFrame.Unity.Runtime");

            Assert.IsNotNull(type, "缺少 MonoSingleton<T>，或命名空间 / 程序集未按新架构暴露。");
            Assert.IsTrue(type.IsAbstract);
            Assert.IsTrue(typeof(MonoBehaviour).IsAssignableFrom(type));
        }

        /// <summary>
        /// 验证编辑模式下查询不创建实例，访问 Instance 后得到同一对象，Dispose 后对象被立即销毁。
        /// </summary>
        [Test]
        public void Instance_编辑模式生命周期_查询不创建访问后创建且Dispose销毁()
        {
            Assert.IsFalse(SingletonKitAdapterProbe.HasInstance);
            Assert.IsFalse(SingletonKitAdapterProbe.TryGetInstance(out var absent));
            Assert.IsTrue(absent == default);

            var created = SingletonKitAdapterProbe.Instance;

            Assert.IsTrue(created != default);
            Assert.IsTrue(SingletonKitAdapterProbe.HasInstance);
            Assert.IsTrue(SingletonKitAdapterProbe.TryGetInstance(out var current));
            Assert.AreSame(created, current);

            SingletonKitAdapterProbe.Dispose();

            Assert.IsFalse(SingletonKitAdapterProbe.HasInstance);
            Assert.IsTrue(created == default);
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
        /// 验证 Unity 适配源码存在，并由单一 Unity 环境宏整文件包裹。
        /// </summary>
        /// <param name="sourcePath">待验证源码路径。</param>
        private static void AssertUnityAdapterFile(string sourcePath)
        {
            Assert.IsTrue(File.Exists(sourcePath), "缺少 Unity SingletonKit 适配源码: " + sourcePath);

            string[] lines = File.ReadAllLines(sourcePath);
            Assert.AreEqual(UNITY_DEFINE, FirstNonEmptyLine(lines), "Unity 适配源码必须从 Unity 环境宏开始: " + sourcePath);
            Assert.AreEqual(ENDIF_DIRECTIVE, LastNonEmptyLine(lines), "Unity 适配源码必须用 #endif 结束: " + sourcePath);
            AssertNoExactLine(lines, GODOT_EXCLUSION_DEFINE, sourcePath);
        }

        /// <summary>
        /// 验证源码中不存在与指定预处理指令完全相同的行。
        /// </summary>
        /// <param name="lines">源码行。</param>
        /// <param name="forbiddenLine">禁止出现的完整行。</param>
        /// <param name="sourcePath">源码路径，用于失败消息。</param>
        private static void AssertNoExactLine(string[] lines, string forbiddenLine, string sourcePath)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                Assert.AreNotEqual(
                    forbiddenLine,
                    lines[index].Trim(),
                    "Unity 适配层不能用排除 Godot 的宽泛宏: " + sourcePath);
            }
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

        /// <summary>
        /// 重置退出状态并销毁探针，保证每个测试从没有实例开始。
        /// </summary>
        private static void ResetProbe()
        {
            MonoSingletonExitState.ResetForTests();
            SingletonKitAdapterProbe.Dispose();
            DestroyLeftoverProbes();
            DestroyUnattachedProbeObject();
        }

        /// <summary>
        /// 销毁场景中残留的探针。Dispose 只处理当前静态实例，不能覆盖静态引用已丢失的对象。
        /// </summary>
        private static void DestroyLeftoverProbes()
        {
#if UNITY_7000_0_OR_NEWER
            var probes = UnityEngine.Object.FindObjectsByType<SingletonKitAdapterProbe>(FindObjectsInactive.Include);
#elif UNITY_2023_1_OR_NEWER
            var probes = UnityEngine.Object.FindObjectsByType<SingletonKitAdapterProbe>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
#else
            var probes = UnityEngine.Object.FindObjectsOfType<SingletonKitAdapterProbe>(true);
#endif
            for (var index = 0; index < probes.Length; index++)
            {
                var probe = probes[index];
                if (probe != default)
                {
                    UnityEngine.Object.DestroyImmediate(probe.gameObject);
                }
            }
        }

        /// <summary>
        /// 销毁没有挂上探针组件的同名对象。AddComponent 失败时 MonoSingleton 会留下空 GameObject。
        /// </summary>
        private static void DestroyUnattachedProbeObject()
        {
            var named = GameObject.Find(nameof(SingletonKitAdapterProbe));
            if (named != default)
            {
                UnityEngine.Object.DestroyImmediate(named);
            }
        }
    }
}
