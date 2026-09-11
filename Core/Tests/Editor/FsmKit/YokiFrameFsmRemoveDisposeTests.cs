using System;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证状态移除在 Dispose 失败时仍遵守“每次移除只调用一次 Dispose”的 IState 契约。
    /// </summary>
    public sealed class YokiFrameFsmRemoveDisposeTests
    {
        /// <summary>测试使用的最小状态标识。</summary>
        private enum StateId
        {
            A,
            B
        }

        /// <summary>
        /// 验证 Dispose 抛出后状态不会残留在容器中，也不会被状态机二次释放。
        /// </summary>
        [Test]
        public void RemoveReleasesStateWhenDisposeThrows()
        {
            FSM<StateId> fsm = new FSM<StateId>("RemoveDisposeFailure");
            ThrowingDisposeState state = new ThrowingDisposeState();
            fsm.Add(StateId.A, state);

            Assert.Throws<InvalidOperationException>(() => fsm.Remove(StateId.A));

            fsm.Get(StateId.A, out IState remaining);
            Assert.IsNull(remaining, "Dispose 抛出后状态不得残留在状态机容器中，否则无法再次清理");

            // 旧实现下残留项会在 FSM.Dispose → ClearStates 中被二次 Dispose：
            // 既让 Dispose 计数变成 2，也会让整体释放抛出 AggregateException。
            Assert.DoesNotThrow(() => fsm.Dispose());
            Assert.AreEqual(1, state.DisposeCount, "IState 契约保证每次移除只调用一次 Dispose");
        }

        /// <summary>
        /// 验证正常路径仍然只释放一次，确认修复没有引入重复释放。
        /// </summary>
        [Test]
        public void RemoveReleasesStateExactlyOnceOnSuccess()
        {
            FSM<StateId> fsm = new FSM<StateId>("RemoveDisposeSuccess");
            CountingDisposeState state = new CountingDisposeState();
            fsm.Add(StateId.B, state);

            fsm.Remove(StateId.B);

            fsm.Get(StateId.B, out IState remaining);
            Assert.IsNull(remaining, "正常移除后状态不应留在容器中");

            Assert.DoesNotThrow(() => fsm.Dispose());
            Assert.AreEqual(1, state.DisposeCount, "正常路径同样只释放一次");
        }

        /// <summary>Dispose 首次调用即抛出的状态，用于覆盖失败路径。</summary>
        private sealed class ThrowingDisposeState : IState
        {
            /// <summary>获取当前状态被释放的次数。</summary>
            internal int DisposeCount { get; private set; }

            /// <summary>测试状态无需启动逻辑。</summary>
            public void Start()
            {
            }

            /// <summary>测试状态无需暂停逻辑。</summary>
            public void Suspend()
            {
            }

            /// <summary>测试状态无需普通更新逻辑。</summary>
            public void Update()
            {
            }

            /// <summary>测试状态无需固定更新逻辑。</summary>
            public void FixedUpdate()
            {
            }

            /// <summary>测试状态无需自定义更新逻辑。</summary>
            public void CustomUpdate()
            {
            }

            /// <summary>测试状态无需结束逻辑。</summary>
            public void End()
            {
            }

            /// <summary>记录释放次数并模拟释放失败。</summary>
            public void Dispose()
            {
                DisposeCount++;
                throw new InvalidOperationException("状态释放失败");
            }

            /// <summary>测试状态忽略消息。</summary>
            /// <typeparam name="TMsg">消息类型。</typeparam>
            /// <param name="message">消息值。</param>
            public void SendMessage<TMsg>(TMsg message)
            {
            }
        }

        /// <summary>只记录释放次数的状态，用于确认正常路径未重复释放。</summary>
        private sealed class CountingDisposeState : IState
        {
            /// <summary>获取当前状态被释放的次数。</summary>
            internal int DisposeCount { get; private set; }

            /// <summary>测试状态无需启动逻辑。</summary>
            public void Start()
            {
            }

            /// <summary>测试状态无需暂停逻辑。</summary>
            public void Suspend()
            {
            }

            /// <summary>测试状态无需普通更新逻辑。</summary>
            public void Update()
            {
            }

            /// <summary>测试状态无需固定更新逻辑。</summary>
            public void FixedUpdate()
            {
            }

            /// <summary>测试状态无需自定义更新逻辑。</summary>
            public void CustomUpdate()
            {
            }

            /// <summary>测试状态无需结束逻辑。</summary>
            public void End()
            {
            }

            /// <summary>记录一次释放。</summary>
            public void Dispose()
            {
                DisposeCount++;
            }

            /// <summary>测试状态忽略消息。</summary>
            /// <typeparam name="TMsg">消息类型。</typeparam>
            /// <param name="message">消息值。</param>
            public void SendMessage<TMsg>(TMsg message)
            {
            }
        }
    }
}
