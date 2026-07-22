using System;
using System.Threading.Tasks;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证动作树深度、活动 ID 和单宿主线程约束。</summary>
    public sealed class ActionKitOwnershipBoundaryTests
    {
        /// <summary>每个测试前绑定当前 Unity 测试线程并清空静态状态。</summary>
        [SetUp]
        public void SetUp()
        {
            ActionKitScheduler.Cleanup();
            ActionStackTraceService.Enabled = false;
        }

        /// <summary>每个测试后释放活动树与诊断记录。</summary>
        [TearDown]
        public void TearDown()
        {
            ActionStackTraceService.Enabled = false;
            ActionKitScheduler.Cleanup();
        }

        /// <summary>验证最大可释放深度可组装，额外一层会在写入所有权前被拒绝。</summary>
        [Test]
        public void AppendRejectsTreeBeyondLifecycleDepth()
        {
            ISequence root = ActionKit.Sequence();
            ISequence current = root;
            for (var depth = 1; depth < ActionTreeLimits.MAX_DEPTH; depth++)
            {
                ISequence child = ActionKit.Sequence();
                current.Append(child);
                current = child;
            }

            ISequence overflow = ActionKit.Sequence();
            Assert.Throws<InvalidOperationException>(() => current.Append(overflow));

            Assert.DoesNotThrow(() => ActionKitScheduler.DiscardUnscheduled(root));
            Assert.IsTrue(root.Deinited);
            ActionKitScheduler.DiscardUnscheduled(overflow);
            ActionKitScheduler.ProcessRecycle();
        }

        /// <summary>验证两个活动自定义根不能共享 ID，失败启动也不会删除原根堆栈。</summary>
        [Test]
        public void ActiveCustomActionsRejectDuplicateIds()
        {
            const ulong sharedId = 1UL << 61;
            FixedIdAction first = new(sharedId);
            FixedIdAction duplicate = new(sharedId);
            ActionStackTraceService.Enabled = true;
            IActionController firstController = first.Start();

            Assert.Throws<InvalidOperationException>(() => duplicate.Start());

            Assert.AreEqual(1, ActionStackTraceService.Count);
            Assert.AreEqual(1, ActionKitScheduler.ExecutingCount);
            Assert.IsFalse(firstController.IsCompleted);
            Assert.IsTrue(duplicate.Deinited);
        }

        /// <summary>验证后台线程 Start 被拒绝且不会占用所有权，随后仍可在宿主线程启动。</summary>
        [Test]
        public void StartRejectsBackgroundThreadWithoutClaimingAction()
        {
            IAction action = ActionKit.Delay(10f);
            Task<Exception> attempt = System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    action.Start();
                    return null;
                }
                catch (Exception exception)
                {
                    return exception;
                }
            });

            Exception backgroundException = attempt.GetAwaiter().GetResult();
            Assert.IsInstanceOf<InvalidOperationException>(backgroundException);

            IActionController controller = action.Start();
            Assert.IsFalse(controller.IsCompleted);
            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);
        }

        /// <summary>提供调用方指定的非零 ID，并保持活动直到测试清理。</summary>
        private sealed class FixedIdAction : ActionBase
        {
            /// <summary>创建具有指定稳定 ID 的自定义 Action。</summary>
            /// <param name="actionId">用于覆盖冲突场景的非零 ID。</param>
            internal FixedIdAction(ulong actionId) => ActionID = actionId;

            /// <summary>保持运行，不主动进入正常完成。</summary>
            /// <param name="dt">当前宿主时间步长。</param>
            public override void OnExecute(float dt) { }
        }
    }
}
