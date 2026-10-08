using System;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 EventKit 使用池化监听节点时的复用、派发和过期令牌安全性。
    /// </summary>
    public sealed class YokiFrameEventKitPoolingTests
    {
        /// <summary>
        /// 验证 EasyEvent 的监听节点由 PooledLinkedList 保存并在注销后进入节点池。
        /// </summary>
        [Test]
        public void EasyEventReturnsUnregisteredNodeToPool()
        {
            var easyEvent = new EasyEvent();
            LinkUnRegister token = easyEvent.Register(() => { });

            token.UnRegister();

            Assert.AreEqual(0, easyEvent.ListenerCount);
            Assert.AreEqual(1, easyEvent.PooledNodeCount);
        }

        /// <summary>
        /// 验证旧令牌副本不能移除已经复用同一节点的新监听器。
        /// </summary>
        [Test]
        public void StaleTokenCopyCannotUnregisterReusedNode()
        {
            var easyEvent = new EasyEvent();
            int received = 0;
            LinkUnRegister firstToken = easyEvent.Register(() => { });
            LinkUnRegister staleCopy = firstToken;
            firstToken.UnRegister();
            easyEvent.Register(() => received++);

            staleCopy.UnRegister();
            easyEvent.Trigger();

            Assert.AreEqual(1, received);
        }

        /// <summary>
        /// 验证派发中清空事件会立即阻止本轮剩余监听器执行。
        /// </summary>
        [Test]
        public void ClearDuringDispatchSkipsRemainingListeners()
        {
            var easyEvent = new EasyEvent();
            string calls = string.Empty;
            easyEvent.Register(() =>
            {
                calls += "A";
                easyEvent.UnRegisterAll();
            });
            easyEvent.Register(() => calls += "B");
            easyEvent.Register(() => calls += "C");

            easyEvent.Trigger();

            Assert.AreEqual("A", calls);
        }

        /// <summary>
        /// 验证尾节点监听器在派发中注册新监听器时，新监听器不参与本轮派发，下一轮才生效。
        /// </summary>
        [Test]
        public void ListenerRegisteredByTailListenerDuringDispatchIsDeferredToNextRound()
        {
            var easyEvent = new EasyEvent();
            string calls = string.Empty;
            easyEvent.Register(() =>
            {
                calls += "A";
                if (calls.Length == 1)
                {
                    easyEvent.Register(() => calls += "B");
                }
            });

            easyEvent.Trigger();
            Assert.AreEqual("A", calls);

            easyEvent.Trigger();
            Assert.AreEqual("AAB", calls);
        }

        /// <summary>
        /// 验证非尾节点监听器在派发中注册新监听器时，新监听器同样不参与本轮派发。
        /// </summary>
        [Test]
        public void ListenerRegisteredByNonTailListenerDuringDispatchIsDeferredToNextRound()
        {
            var easyEvent = new EasyEvent();
            string calls = string.Empty;
            easyEvent.Register(() =>
            {
                calls += "A";
                if (calls.Length == 1)
                {
                    easyEvent.Register(() => calls += "C");
                }
            });
            easyEvent.Register(() => calls += "B");

            easyEvent.Trigger();
            Assert.AreEqual("AB", calls);

            easyEvent.Trigger();
            Assert.AreEqual("ABABC", calls);
        }

        /// <summary>
        /// 验证泛型 EasyEvent 同样保护复用节点不受过期令牌影响。
        /// </summary>
        [Test]
        public void GenericStaleTokenCopyCannotUnregisterReusedNode()
        {
            var easyEvent = new EasyEvent<int>();
            int received = 0;
            LinkUnRegister<int> firstToken = easyEvent.Register(_ => { });
            LinkUnRegister<int> staleCopy = firstToken;
            firstToken.UnRegister();
            easyEvent.Register(value => received += value);

            staleCopy.UnRegister();
            easyEvent.Trigger(7);

            Assert.AreEqual(7, received);
        }

    }
}
