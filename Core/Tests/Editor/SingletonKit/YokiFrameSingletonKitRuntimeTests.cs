using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 SingletonKit 的纯 C# 单例创建、生命周期回调和诊断注册行为。
    /// </summary>
    public sealed class YokiFrameSingletonKitRuntimeTests
    {
        /// <summary>
        /// 每个测试前清理注册表和泛型缓存，避免单例状态跨测试污染。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            SingletonRegistry.Clear();
            SingletonKit<PrivateService>.Dispose();
            InheritedService.Dispose();
            SingletonKit<NoDefaultCtorService>.Dispose();
            SingletonKit<ThrowingInitService>.Dispose();
            SingletonKit<SelfReferencingService>.Dispose();
            PrivateService.ResetCounters();
            InheritedService.ResetCounters();
            SelfReferencingService.ResetCounters();
        }

        /// <summary>
        /// 验证 SingletonKit 能创建私有构造的纯 C# 单例，并且同一类型只初始化一次。
        /// </summary>
        [Test]
        public void SingletonKitCreatesPrivateConstructorInstanceOnce()
        {
            PrivateService first = SingletonKit<PrivateService>.Instance;
            PrivateService second = SingletonKit<PrivateService>.Instance;

            Assert.AreSame(first, second);
            Assert.AreEqual(1, PrivateService.ConstructorCount);
            Assert.AreEqual(1, PrivateService.InitCount);
        }

        /// <summary>
        /// 验证继承式 Singleton 入口复用同一套 SingletonKit 生命周期。
        /// </summary>
        [Test]
        public void SingletonBaseClassUsesSingletonKitInstance()
        {
            InheritedService first = InheritedService.Instance;
            InheritedService second = SingletonKit<InheritedService>.Instance;

            Assert.AreSame(first, second);
            Assert.AreEqual(1, InheritedService.InitCount);
        }

        /// <summary>
        /// 验证 Dispose 只释放指定类型的缓存，并在诊断注册表中标记为非存活。
        /// </summary>
        [Test]
        public void DisposeMarksSingletonAsInactiveInRegistry()
        {
            PrivateService instance = SingletonKit<PrivateService>.Instance;
            SingletonKit<PrivateService>.Dispose();

            var infos = new List<SingletonDebugInfo>();
            SingletonRegistry.GetAll(infos);

            Assert.AreEqual(1, infos.Count);
            Assert.AreEqual(typeof(PrivateService).Name, infos[0].TypeName);
            Assert.AreEqual(typeof(PrivateService).FullName, infos[0].FullName);
            Assert.AreEqual("Base", infos[0].Backend);
            Assert.AreEqual("SingletonKit", infos[0].Source);
            Assert.AreEqual(instance.GetHashCode(), infos[0].InstanceHash);
            Assert.IsFalse(infos[0].IsAlive);
        }

        /// <summary>
        /// 验证无无参构造函数的类型会给出明确错误，而不是吞掉反射异常。
        /// </summary>
        [Test]
        public void SingletonKitRejectsTypeWithoutParameterlessConstructor()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => _ = SingletonKit<NoDefaultCtorService>.Instance);

            Assert.IsTrue(exception.Message.Contains("parameterless constructor"));
            Assert.IsTrue(exception.Message.Contains(nameof(NoDefaultCtorService)));
        }

        /// <summary>
        /// 验证初始化回调失败时不会把未完成初始化的实例登记为存活单例。
        /// </summary>
        [Test]
        public void FailedInitializationDoesNotLeaveAliveRegistryEntry()
        {
            InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                () => _ = SingletonKit<ThrowingInitService>.Instance);

            var infos = new List<SingletonDebugInfo>();
            SingletonRegistry.GetAll(infos);

            Assert.AreEqual("init failed", exception.Message);
            Assert.AreEqual(0, infos.Count);
            Assert.IsFalse(SingletonKit<ThrowingInitService>.HasInstance);
        }

        /// <summary>
        /// 验证初始化回调可以读取当前单例本身，不会因缓存尚未提交而递归创建。
        /// </summary>
        [Test]
        public void InitializationCanReferenceCurrentSingleton()
        {
            SelfReferencingService instance = SingletonKit<SelfReferencingService>.Instance;

            Assert.AreSame(instance, instance.InitializationReference);
            Assert.AreEqual(1, SelfReferencingService.ConstructorCount);
            Assert.AreEqual(1, SelfReferencingService.InitCount);
        }

        /// <summary>
        /// 用于验证私有构造函数和初始化回调的纯 C# 单例。
        /// </summary>
        private sealed class PrivateService : ISingleton
        {
            public static int ConstructorCount;
            public static int InitCount;

            /// <summary>
            /// 私有构造函数用于验证 SingletonKit 反射创建兼容旧版用法。
            /// </summary>
            private PrivateService()
            {
                ConstructorCount++;
            }

            /// <summary>
            /// 记录单例初始化次数。
            /// </summary>
            public void OnSingletonInit()
            {
                InitCount++;
            }

            /// <summary>
            /// 重置测试计数器。
            /// </summary>
            public static void ResetCounters()
            {
                ConstructorCount = 0;
                InitCount = 0;
            }
        }

        /// <summary>
        /// 用于验证继承式 Singleton 入口的测试类型。
        /// </summary>
        private sealed class InheritedService : Singleton<InheritedService>
        {
            public static int InitCount;

            /// <summary>
            /// 私有构造函数用于验证继承式入口同样支持隐藏构造。
            /// </summary>
            private InheritedService()
            {
            }

            /// <summary>
            /// 记录继承式单例初始化次数。
            /// </summary>
            public override void OnSingletonInit()
            {
                InitCount++;
            }

            /// <summary>
            /// 重置测试计数器。
            /// </summary>
            public static void ResetCounters()
            {
                InitCount = 0;
            }
        }

        /// <summary>
        /// 用于验证缺少无参构造函数时的错误提示。
        /// </summary>
        private sealed class NoDefaultCtorService : ISingleton
        {
            /// <summary>
            /// 只有带参构造函数，SingletonKit 不应尝试猜测参数。
            /// </summary>
            /// <param name="value">测试用参数。</param>
            public NoDefaultCtorService(string value)
            {
            }

            /// <summary>
            /// 该类型不会被成功初始化。
            /// </summary>
            public void OnSingletonInit()
            {
            }
        }

        /// <summary>
        /// 用于验证初始化失败不会污染诊断注册表的测试单例。
        /// </summary>
        private sealed class ThrowingInitService : ISingleton
        {
            /// <summary>
            /// 私有构造函数用于保持和普通 SingletonKit 类型一致的创建路径。
            /// </summary>
            private ThrowingInitService()
            {
            }

            /// <summary>
            /// 主动抛出异常，模拟单例初始化过程中失败。
            /// </summary>
            public void OnSingletonInit()
            {
                throw new InvalidOperationException("init failed");
            }
        }

        /// <summary>
        /// 初始化时回读自身的测试单例，用于覆盖 Monitor 可重入导致的递归创建风险。
        /// </summary>
        private sealed class SelfReferencingService : ISingleton
        {
            public static int ConstructorCount;
            public static int InitCount;

            /// <summary>创建实例并记录构造次数。</summary>
            private SelfReferencingService()
            {
                ConstructorCount++;
            }

            /// <summary>获取初始化回调内解析到的同一实例。</summary>
            public SelfReferencingService InitializationReference { get; private set; }

            /// <summary>在初始化回调中回读 SingletonKit，验证实例已经进入缓存。</summary>
            public void OnSingletonInit()
            {
                InitCount++;
                InitializationReference = SingletonKit<SelfReferencingService>.Instance;
            }

            /// <summary>重置跨测试静态计数。</summary>
            public static void ResetCounters()
            {
                ConstructorCount = 0;
                InitCount = 0;
            }
        }
    }
}
