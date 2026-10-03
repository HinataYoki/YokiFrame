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
}
#endif
