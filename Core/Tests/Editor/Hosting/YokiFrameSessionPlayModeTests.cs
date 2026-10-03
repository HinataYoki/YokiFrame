#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 在关闭 Domain Reload 和 Scene Reload 的工程里，连续进入两次 Play Mode。
    /// 退出后静态状态必须还在，再次进入必须由会话换代清掉。
    /// </summary>
    public sealed class YokiFrameSessionPlayModeTests
    {
        /// <summary>
        /// 验证当前工程的进入 Play Mode 不会卸载域，但每次进入都会重置已登记的会话状态。
        /// </summary>
        /// <returns>UnityTest 在进入和退出 Play Mode 时继续执行的迭代器。</returns>
        [UnityTest]
        public IEnumerator DisabledDomainReloadResetsSessionOnEachPlayEnter()
        {
            SessionObservation first = CreateDirtySession();
            yield return new EnterPlayMode();
            AssertSessionWasReset(first);

            SessionObservation playing = CreateDirtySession();
            yield return new ExitPlayMode();
            AssertSessionSurvivedExit(playing);

            yield return new EnterPlayMode();
            AssertSessionWasReset(playing);

            yield return new ExitPlayMode();
        }

        /// <summary>创建一份额外静态状态，供下一次进入 Play Mode 验证。</summary>
        /// <returns>进入前观察到的代际、单例和事件监听。</returns>
        private static SessionObservation CreateDirtySession()
        {
            YokiFrameSessionProbeSingleton.ProcessMarker = 41;
            YokiFrameSessionProbeSingleton singleton = SingletonKit<YokiFrameSessionProbeSingleton>.Instance;
            singleton.TouchCount = 11;
            SessionObservation observation = new SessionObservation
            {
                Generation = YokiFrameSession.Generation,
                Singleton = singleton
            };
            EventKit.Type.Register<YokiFrameSessionProbeEvent>(_ => observation.Delivered = true);
            return observation;
        }

        /// <summary>退出后域仍在，单例和监听都必须还在，代际不得变化。</summary>
        /// <param name="observation">退出前的观察结果。</param>
        private static void AssertSessionSurvivedExit(SessionObservation observation)
        {
            Assert.AreEqual(observation.Generation, YokiFrameSession.Generation, "退出 Play Mode 不应换代。");
            Assert.IsTrue(SingletonKit<YokiFrameSessionProbeSingleton>.HasInstance);
            Assert.AreSame(observation.Singleton, SingletonKit<YokiFrameSessionProbeSingleton>.Instance);
            Assert.AreEqual(11, observation.Singleton.TouchCount);
            Assert.AreEqual(41, YokiFrameSessionProbeSingleton.ProcessMarker);
            EventKit.Type.Send(new YokiFrameSessionProbeEvent());
            Assert.IsTrue(observation.Delivered, "退出 Play Mode 后上一会话的监听必须还在。");
        }

        /// <summary>进入后已登记状态被换掉，未登记的进程标记仍保留。</summary>
        /// <param name="observation">进入前的观察结果。</param>
        private static void AssertSessionWasReset(SessionObservation observation)
        {
            observation.Delivered = false;
            Assert.Greater(YokiFrameSession.Generation, observation.Generation, "进入 Play Mode 必须换代。");
            Assert.AreEqual(41, YokiFrameSessionProbeSingleton.ProcessMarker, "未登记字段仍在，说明没有 Domain Reload。");
            Assert.IsFalse(SingletonKit<YokiFrameSessionProbeSingleton>.HasInstance);
            EventKit.Type.Send(new YokiFrameSessionProbeEvent());
            Assert.IsFalse(observation.Delivered, "进入 Play Mode 后不得再调用上一会话的监听。");
        }

        /// <summary>保存一次会话观察，避免测试迭代器直接散落多项断言输入。</summary>
        private sealed class SessionObservation
        {
            /// <summary>观察时的会话代际。</summary>
            public long Generation;

            /// <summary>观察时的单例实例。</summary>
            public YokiFrameSessionProbeSingleton Singleton;

            /// <summary>观察期间登记的监听是否被调用。</summary>
            public bool Delivered;
        }
    }
}
#endif
