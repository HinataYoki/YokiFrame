#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证统一会话会按顺序重置事件、架构和单例，并且单个参与者失败不会挡住后续参与者。
    /// </summary>
    public sealed class YokiFrameSessionTests
    {
        /// <summary>验证后登记的同名参与者替换旧回调，并按顺序执行。</summary>
        [Test]
        public void BeginInvokesParticipantsInOrderAndReplacesSameId()
        {
            string order = string.Empty;
            YokiFrameSession.Register(1, "session-probe-order", () => order += "a");
            YokiFrameSession.Register(1, "session-probe-order", () => order += "b");
            YokiFrameSession.Register(2, "session-probe-later", () => order += "c");

            YokiFrameSession.Begin();

            StringAssert.Contains("bc", order);
            YokiFrameSession.Register(1, "session-probe-order", static () => { });
            YokiFrameSession.Register(2, "session-probe-later", static () => { });
        }

        /// <summary>验证换代会清掉上一会话的类型事件订阅。</summary>
        [Test]
        public void BeginClearsEventSubscriptions()
        {
            bool delivered = false;
            EventKit.Type.Register<YokiFrameSessionProbeEvent>(_ => delivered = true);

            YokiFrameSession.Begin();
            EventKit.Type.Send(new YokiFrameSessionProbeEvent());

            Assert.IsFalse(delivered, "会话换代后不得再调用上一会话的事件监听。");
        }

        /// <summary>验证换代会释放架构静态实例，下一次访问创建新实例。</summary>
        [Test]
        public void BeginDisposesArchitectureInstance()
        {
            IArchitecture first = YokiFrameSessionProbeArchitecture.Interface;

            YokiFrameSession.Begin();
            IArchitecture second = YokiFrameSessionProbeArchitecture.Interface;

            Assert.AreNotSame(first, second);
        }

        /// <summary>验证换代会丢掉纯 C# 单例缓存。</summary>
        [Test]
        public void BeginDisposesSingletonInstance()
        {
            YokiFrameSessionProbeSingleton first = SingletonKit<YokiFrameSessionProbeSingleton>.Instance;
            Assert.IsNotNull(first);

            YokiFrameSession.Begin();

            Assert.IsFalse(SingletonKit<YokiFrameSessionProbeSingleton>.HasInstance);
        }

        /// <summary>验证换代释放全局共享池，但不影响调用方持有的局部池。</summary>
        [Test]
        public void BeginClearsSharedPoolsOnly()
        {
            ObjectPool<YokiFrameSessionProbePoolItem> shared = PoolKit.Shared.Register<YokiFrameSessionProbePoolItem>();
            ObjectPool<YokiFrameSessionProbePoolItem> local = PoolKit.Create<YokiFrameSessionProbePoolItem>();
            YokiFrameSessionProbePoolItem sharedItem = shared.Allocate();
            YokiFrameSessionProbePoolItem localItem = local.Allocate();

            YokiFrameSession.Begin();

            Assert.IsFalse(PoolKit.Shared.TryGet(out ObjectPool<YokiFrameSessionProbePoolItem> _));
            Assert.DoesNotThrow(() => local.Recycle(localItem));
            sharedItem.TouchCount = 1;
        }
    }

    /// <summary>只给会话测试使用的空架构。</summary>
    public sealed class YokiFrameSessionProbeArchitecture : Architecture<YokiFrameSessionProbeArchitecture>
    {
        /// <summary>测试架构不注册服务。</summary>
        protected override void OnInit()
        {
        }
    }

    /// <summary>只给会话测试使用的空单例。</summary>
    public sealed class YokiFrameSessionProbeSingleton : ISingleton
    {
        /// <summary>进程级标记。会话换代不得清掉它，用来证明没有发生 Domain Reload。</summary>
        public static int ProcessMarker;

        /// <summary>实例标记。换代后实例被丢掉，这个值不应再被读到。</summary>
        public int TouchCount;

        /// <summary>测试单例没有初始化副作用。</summary>
        public void OnSingletonInit()
        {
        }
    }

    /// <summary>只给会话测试使用的事件负载。</summary>
    public readonly struct YokiFrameSessionProbeEvent
    {
    }

    /// <summary>只给共享池会话测试使用的可池化对象。</summary>
    public sealed class YokiFrameSessionProbePoolItem : IPoolable
    {
        /// <summary>释放后仍可读写，用来证明局部对象没有被共享池清理连带销毁。</summary>
        public int TouchCount;

        /// <summary>测试对象借出时没有额外初始化。</summary>
        public void OnAllocated()
        {
        }

        /// <summary>测试对象归还时没有额外清理。</summary>
        public void OnRecycled()
        {
        }
    }
}
#endif
