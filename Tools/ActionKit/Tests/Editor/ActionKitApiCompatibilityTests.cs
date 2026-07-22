using System;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 锁定 2.0-pre 已公开的 ActionKit 源码契约，避免实现收紧导致旧调用无法重新编译。
    /// </summary>
    public sealed class ActionKitApiCompatibilityTests
    {
        private readonly ActionKitTestLogger mLogger = new();

        /// <summary>每个测试前清空静态调度状态，隔离公开构造实例的运行租约。</summary>
        [SetUp]
        public void SetUp()
        {
            ActionKitScheduler.Cleanup();
            mLogger.Clear();
            LogKit.SetLogger(mLogger);
        }

        /// <summary>每个测试后清理仍活动的动作，避免失败用例污染其它程序集测试。</summary>
        [TearDown]
        public void TearDown()
        {
            try
            {
                ActionKitScheduler.Cleanup();
                mLogger.AssertNoErrors();
            }
            finally { LogKit.ClearLogger(); }
        }

        /// <summary>
        /// 验证自定义 IAction 可以依赖接口默认调试文本，无需额外实现诊断方法。
        /// </summary>
        [Test]
        public void IActionProvidesDefaultDebugInfoImplementation()
        {
            var action = (IAction)new DefaultDebugInfoAction();

            Assert.AreEqual(nameof(DefaultDebugInfoAction), action.GetDebugInfo());
        }

        /// <summary>
        /// 验证 Delay 保持公开构造、可继承、字段和可写进度的 2.0-pre 源码契约。
        /// </summary>
        [Test]
        public void DelayRemainsPubliclyConstructibleInheritableAndMutable()
        {
            DerivedDelay action = new();
            Assign(ref action.DelayTime, 2f);
            action.CurrentSeconds = 0.5f;
            action.OnDelayFinish = Ignore;

            Assert.AreEqual(2f, action.DelayTime);
            Assert.AreEqual(0.5f, action.CurrentSeconds);
            Assert.AreEqual((Action)Ignore, action.OnDelayFinish);
        }

        /// <summary>
        /// 验证 Lerp 保持公开构造、可继承以及可直接配置字段的 2.0-pre 源码契约。
        /// </summary>
        [Test]
        public void LerpRemainsPubliclyConstructibleInheritableAndMutable()
        {
            DerivedLerp action = new();
            Assign(ref action.A, 1f);
            Assign(ref action.B, 3f);
            Assign(ref action.Duration, 2f);
            Assign(ref action.OnLerp, IgnoreValue);
            Assign(ref action.OnLerpFinish, Ignore);

            Assert.AreEqual(1f, action.A);
            Assert.AreEqual(3f, action.B);
            Assert.AreEqual(2f, action.Duration);
            Assert.AreEqual((Action<float>)IgnoreValue, action.OnLerp);
            Assert.AreEqual((Action)Ignore, action.OnLerpFinish);
        }

        /// <summary>验证公开构造的 Delay 释放后不会按内部池租约清空公开配置。</summary>
        [Test]
        public void PublicDelayDoesNotEnterInternalPool()
        {
            Delay action = new() { DelayTime = 2f, OnDelayFinish = Ignore };
            IActionController controller = action.Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);
            ActionKitScheduler.ProcessRecycle();

            Assert.AreEqual(2f, action.DelayTime);
            Assert.IsNull(action.OnDelayFinish, "OnDeinit 仍应释放业务回调。");
        }

        /// <summary>验证公开构造的 Lerp 释放后不会按内部池租约清空公开配置。</summary>
        [Test]
        public void PublicLerpDoesNotEnterInternalPool()
        {
            Lerp action = new() { A = 1f, B = 3f, Duration = 2f, OnLerp = IgnoreValue };
            IActionController controller = action.Start();

            controller.Cancel();
            ActionKitScheduler.Tick(0f, 0f);
            ActionKitScheduler.ProcessRecycle();

            Assert.AreEqual(1f, action.A);
            Assert.AreEqual(3f, action.B);
            Assert.AreEqual(2f, action.Duration);
            Assert.IsNull(action.OnLerp, "OnDeinit 仍应释放业务回调。");
        }

        /// <summary>
        /// 验证恢复可写进度后，运行中写入 NaN 会形成故障终态而不是永久占用调度器。
        /// </summary>
        [Test]
        public void MutableDelayRejectsNonFiniteProgressWithoutStalling()
        {
            Delay action = new() { DelayTime = 1f };
            IActionController controller = action.Start();
            action.CurrentSeconds = float.NaN;

            ActionKitScheduler.Tick(0.1f, 0.1f);

            Assert.IsTrue(controller.IsCompleted);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            Assert.AreEqual(0, ActionKitScheduler.ExecutingCount);
            mLogger.AssertErrors("[ActionKit] Action ");
        }

        /// <summary>
        /// 验证恢复可写时长后，运行中写入无穷值会形成故障终态而不是永久插值。
        /// </summary>
        [Test]
        public void MutableLerpRejectsNonFiniteDurationWithoutStalling()
        {
            Lerp action = new() { A = 0f, B = 1f, Duration = 1f };
            IActionController controller = action.Start();
            action.Duration = float.PositiveInfinity;

            ActionKitScheduler.Tick(0.1f, 0.1f);

            Assert.IsTrue(controller.IsCompleted);
            Assert.AreEqual(1, ActionKitScheduler.FaultedCount);
            Assert.AreEqual(0, ActionKitScheduler.ExecutingCount);
            mLogger.AssertErrors("[ActionKit] Action ");
        }

        /// <summary>
        /// 通过 ref 赋值锁定旧公开字段形态，普通属性无法误通过这组源码契约。
        /// </summary>
        /// <typeparam name="T">待赋值字段的类型。</typeparam>
        /// <param name="target">旧公开字段引用。</param>
        /// <param name="value">写入字段的值。</param>
        private static void Assign<T>(ref T target, T value) => target = value;

        /// <summary>提供稳定的无操作回调，避免测试创建捕获闭包。</summary>
        private static void Ignore() { }

        /// <summary>提供稳定的无操作插值回调，避免测试创建捕获闭包。</summary>
        /// <param name="_">本测试不使用的插值值。</param>
        private static void IgnoreValue(float _) { }

        /// <summary>依赖 Delay 公开无参构造的最小派生类型。</summary>
        private sealed class DerivedDelay : Delay { }

        /// <summary>依赖 Lerp 公开无参构造的最小派生类型。</summary>
        private sealed class DerivedLerp : Lerp { }

        /// <summary>故意不实现 GetDebugInfo，用于编译期锁定 IAction 默认实现。</summary>
        private sealed class DefaultDebugInfoAction : IAction
        {
            /// <summary>获取测试动作的固定非零 ID。</summary>
            public ulong ActionID => 1UL << 61;

            /// <summary>获取或设置测试动作生命周期状态。</summary>
            public ActionStatus ActionState { get; set; }

            /// <summary>获取或设置测试动作暂停状态。</summary>
            public bool Paused { get; set; }

            /// <summary>获取测试动作是否已释放；该编译契约不会启动动作。</summary>
            public bool Deinited => false;

            /// <summary>测试动作无需初始化业务状态。</summary>
            public void OnInit() { }

            /// <summary>测试动作没有待释放资源。</summary>
            public void OnDeinit() { }

            /// <summary>测试动作无需开始逻辑。</summary>
            public void OnStart() { }

            /// <summary>测试动作无需逐帧推进。</summary>
            /// <param name="_">本测试不使用的时间步长。</param>
            public void OnExecute(float _) { }

            /// <summary>测试动作无需完成逻辑。</summary>
            public void OnFinish() { }
        }
    }
}
