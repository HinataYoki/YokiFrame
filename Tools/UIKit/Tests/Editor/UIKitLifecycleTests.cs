using System;
using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 UIKit 公开打开、预加载、缓存和 single-flight 生命周期。
    /// </summary>
    public sealed class UIKitLifecycleTests
    {
        private UIKitTestPanelLoader mLoader;

        /// <summary>
        /// 每个测试安装隔离 loader 和全新 UIRoot，避免静态面板状态串扰。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            UIRoot.Dispose();
            mLoader = new UIKitTestPanelLoader();
            UIKit.SetPanelLoader(mLoader);
            UIKit.ReusableCacheCapacity = 8;
        }

        /// <summary>
        /// 每个测试先由 UIRoot 释放实例 lease，再销毁 loader 持有的内存 Prefab。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UIRoot.Dispose();
            mLoader.Dispose();
            mLoader = null;
        }

        /// <summary>
        /// 验证首次物化保留 Prefab 名称，以及重复 Open、Hide、Show、Close 的钩子顺序计数和 Persistent 显式卸载。
        /// </summary>
        [Test]
        public void OpenHideShowCloseDispatchesLifecycleAndPersistentUnloadReleasesLease()
        {
            var firstData = new UIKitTestData("first");
            var secondData = new UIKitTestData("second");
            UIKitLifecycleTestPanel panel = UIKit.OpenPanel<UIKitLifecycleTestPanel>(
                UILevel.Pop,
                firstData,
                "initial",
                PanelCachePolicy.Persistent);
            IPanel panelContract = panel;

            Assert.AreEqual(PanelState.Open, panel.State);
            Assert.AreEqual(1, panel.InitCount);
            Assert.AreEqual(1, panel.OpenCount);
            Assert.AreEqual(1, panel.WillShowCount);
            Assert.AreEqual(1, panel.ShowCount);
            Assert.AreEqual(1, panel.DidShowCount);
            Assert.AreSame(firstData, panel.LastInitData);
            Assert.AreSame(firstData, panelContract.Data);
            Assert.AreEqual("initial", panel.Tag);
            Assert.AreEqual(
                typeof(UIKitLifecycleTestPanel).Name + ".Prefab",
                panel.gameObject.name,
                "面板实例不应保留 Unity 自动追加的 (Clone)。");

            UIKitLifecycleTestPanel duplicate = UIKit.OpenPanel<UIKitLifecycleTestPanel>(
                UILevel.Toast,
                secondData,
                "updated",
                PanelCachePolicy.Persistent);

            Assert.AreSame(panel, duplicate);
            Assert.AreEqual(1, panel.InitCount);
            Assert.AreEqual(2, panel.OpenCount);
            Assert.AreEqual(1, panel.ShowCount, "可见面板重复 Open 不应重放显示转换。");
            Assert.AreSame(secondData, panel.LastOpenData);
            Assert.AreSame(secondData, panelContract.Data);
            Assert.AreEqual(UILevel.Toast, panel.Level);
            Assert.AreEqual("updated", panel.Tag);

            UIKitTestData assignedData = new("assigned");
            panelContract.Data = assignedData;
            Assert.AreSame(assignedData, panelContract.Data);
            Assert.AreSame(secondData, panel.LastOpenData, "IPanel.Data 赋值不应重放 OnOpen。");

            panel.Hide();
            Assert.AreEqual(PanelState.Hide, panel.State);
            Assert.AreEqual(1, panel.WillHideCount);
            Assert.AreEqual(1, panel.HideCount);
            Assert.AreEqual(1, panel.DidHideCount);

            panel.Show();
            Assert.AreEqual(PanelState.Open, panel.State);
            Assert.AreEqual(2, panel.WillShowCount);
            Assert.AreEqual(2, panel.ShowCount);
            Assert.AreEqual(2, panel.DidShowCount);

            panel.Close();
            Assert.AreEqual(PanelState.Cached, panel.State);
            Assert.AreEqual(2, panel.WillHideCount);
            Assert.AreEqual(2, panel.HideCount);
            Assert.AreEqual(2, panel.DidHideCount);
            Assert.AreEqual(1, panel.CloseCount);
            Assert.IsNull(panelContract.Data);
            Assert.IsNull(panel.Tag);
            Assert.AreEqual(0, mLoader.LeaseDisposeCount);
            Assert.AreEqual(0, UIKit.ClearReusableCache(), "Persistent 不属于 Reusable LRU。");

            Assert.IsTrue(UIKit.UnloadPanel<UIKitLifecycleTestPanel>());
            Assert.AreEqual(1, mLoader.LeaseDisposeCount);
            Assert.IsFalse(UIKit.IsPanelLoaded<UIKitLifecycleTestPanel>());
        }

        /// <summary>
        /// 验证 Reusable 关闭后复用同一实例，并可由后续 Transient 打开轮次立即释放。
        /// </summary>
        [Test]
        public void ReusableReopensSameInstanceAndTransientCloseReleasesIt()
        {
            UIKitLifecycleTestPanel first = UIKit.OpenPanel<UIKitLifecycleTestPanel>(
                cachePolicy: PanelCachePolicy.Reusable);

            first.Close();
            Assert.AreEqual(PanelState.Cached, first.State);
            Assert.AreEqual(0, mLoader.LeaseDisposeCount);

            UIKitLifecycleTestPanel reopened = UIKit.OpenPanel<UIKitLifecycleTestPanel>(
                cachePolicy: PanelCachePolicy.Transient);

            Assert.AreSame(first, reopened);
            Assert.AreEqual(1, mLoader.SyncLoadCount);
            Assert.AreEqual(1, reopened.InitCount);
            Assert.AreEqual(2, reopened.OpenCount);
            Assert.AreEqual(PanelCachePolicy.Transient, reopened.CachePolicy);

            reopened.Close();
            Assert.AreEqual(1, mLoader.LeaseDisposeCount);
            Assert.IsFalse(UIKit.IsPanelLoaded<UIKitLifecycleTestPanel>());
        }

        /// <summary>
        /// 验证 Preload 只执行物化与 OnInit，首次 Open 复用实例且不再次触发 loader。
        /// </summary>
        [Test]
        public async Task PreloadDoesNotOpenAndFirstOpenReusesMaterializedPanel()
        {
            bool loaded = await AsTask(UIKit.PreloadPanelAsync<UIKitLifecycleTestPanel>(
                cachePolicy: PanelCachePolicy.Persistent));
            UIKitLifecycleTestPanel preloaded = UIKit.GetPanel<UIKitLifecycleTestPanel>();

            Assert.IsTrue(loaded);
            Assert.IsNotNull(preloaded);
            Assert.AreEqual(PanelState.Preloaded, preloaded.State);
            Assert.AreEqual(1, preloaded.InitCount);
            Assert.AreEqual(0, preloaded.OpenCount);
            Assert.AreEqual(0, preloaded.ShowCount);
            Assert.IsTrue(UIKit.IsPanelPreloaded<UIKitLifecycleTestPanel>());
            Assert.AreEqual(1, mLoader.AsyncLoadCount);
            Assert.AreEqual(0, mLoader.SyncLoadCount);

            UIKitLifecycleTestPanel opened = UIKit.OpenPanel<UIKitLifecycleTestPanel>(
                cachePolicy: PanelCachePolicy.Persistent);

            Assert.AreSame(preloaded, opened);
            Assert.AreEqual(1, opened.InitCount);
            Assert.AreEqual(1, opened.OpenCount);
            Assert.AreEqual(1, opened.ShowCount);
            Assert.IsFalse(UIKit.IsPanelPreloaded<UIKitLifecycleTestPanel>());
            Assert.AreEqual(1, mLoader.AsyncLoadCount);
            Assert.AreEqual(0, mLoader.SyncLoadCount);
        }

        /// <summary>
        /// 验证同类型异步 Open 只物化一次，且取消一个等待者不影响仍在等待的调用方。
        /// </summary>
        [Test]
        public async Task ConcurrentOpenUsesSingleFlightAndWaiterCancellationIsIndependent()
        {
            mLoader.BeginAsyncGate();
            using CancellationTokenSource cancellation = new();
            Task<UIKitLifecycleTestPanel> cancelledTask = AsTask(
                UIKit.OpenPanelAsync<UIKitLifecycleTestPanel>(
                    ct: cancellation.Token,
                    cachePolicy: PanelCachePolicy.Transient));
            Task<UIKitLifecycleTestPanel> survivingTask = AsTask(
                UIKit.OpenPanelAsync<UIKitLifecycleTestPanel>(
                    cachePolicy: PanelCachePolicy.Transient));

            Assert.AreEqual(1, mLoader.AsyncLoadCount, "同类型并发打开必须共享一次 loader 调用。");
            Assert.AreEqual(0, mLoader.LeaseCount);
            cancellation.Cancel();
            await ObserveCancellation(cancelledTask);
            Assert.IsFalse(survivingTask.IsCompleted, "一个等待者取消不得取消共享加载。");

            mLoader.CompleteAsyncGate();
            UIKitLifecycleTestPanel surviving = await survivingTask;

            Assert.IsNotNull(surviving);
            Assert.AreEqual(1, mLoader.AsyncLoadCount);
            Assert.AreEqual(1, mLoader.LeaseCount);
            Assert.AreEqual(1, surviving.InitCount);
            Assert.AreEqual(1, surviving.OpenCount);
            Assert.AreSame(surviving, UIKit.GetPanel<UIKitLifecycleTestPanel>());

            surviving.Close();
            Assert.AreEqual(1, mLoader.LeaseDisposeCount);
        }

        /// <summary>
        /// 验证全部等待者取消后旧 flight 立即退出索引，后续请求不会加入已取消任务。
        /// </summary>
        [Test]
        public async Task AllWaitersCancelAllowsFreshSingleFlightImmediately()
        {
            mLoader.BeginAsyncGate();
            using CancellationTokenSource firstCancellation = new();
            using CancellationTokenSource secondCancellation = new();
            Task<UIKitLifecycleTestPanel> first = AsTask(UIKit.OpenPanelAsync<UIKitLifecycleTestPanel>(
                ct: firstCancellation.Token,
                cachePolicy: PanelCachePolicy.Transient));
            Task<UIKitLifecycleTestPanel> second = AsTask(UIKit.OpenPanelAsync<UIKitLifecycleTestPanel>(
                ct: secondCancellation.Token,
                cachePolicy: PanelCachePolicy.Transient));

            firstCancellation.Cancel();
            secondCancellation.Cancel();
            await ObserveCancellation(first);
            await ObserveCancellation(second);

            Task<UIKitLifecycleTestPanel> fresh = AsTask(UIKit.OpenPanelAsync<UIKitLifecycleTestPanel>(
                cachePolicy: PanelCachePolicy.Transient));
            Assert.AreEqual(2, mLoader.AsyncLoadCount, "新请求必须建立独立 flight，而不是加入已取消任务。");

            mLoader.CompleteAsyncGate();
            UIKitLifecycleTestPanel panel = await fresh;
            Assert.IsNotNull(panel);
            Assert.AreEqual(1, mLoader.LeaseCount, "旧 flight 取消后不得留下晚到 lease。");
            panel.Close();
            Assert.AreEqual(1, mLoader.LeaseDisposeCount);
        }

        /// <summary>
        /// 验证 loader 同步重入同类型异步 Open 时，共享 Task 在底层加载启动前已经可见。
        /// </summary>
        [Test]
        public async Task LoaderReentryJoinsAlreadyPublishedSingleFlightTask()
        {
            Task<UIKitLifecycleTestPanel> reentrant = null;
            mLoader.AsyncLoadStarted = () =>
                reentrant = AsTask(UIKit.OpenPanelAsync<UIKitLifecycleTestPanel>(
                    cachePolicy: PanelCachePolicy.Persistent));

            Task<UIKitLifecycleTestPanel> first = AsTask(UIKit.OpenPanelAsync<UIKitLifecycleTestPanel>(
                cachePolicy: PanelCachePolicy.Persistent));
            UIKitLifecycleTestPanel firstPanel = await first;
            UIKitLifecycleTestPanel reentrantPanel = await reentrant;

            Assert.AreSame(firstPanel, reentrantPanel);
            Assert.AreEqual(1, mLoader.AsyncLoadCount);
            Assert.AreEqual(1, firstPanel.InitCount);
            Assert.AreEqual(2, firstPanel.OpenCount);
        }

        /// <summary>
        /// 验证 Transient 在执行 OnClosed 前已完成反索引和 lease 释放，回调可立即重开同类型。
        /// </summary>
        [Test]
        public void TransientOnClosedCanReopenSamePanelType()
        {
            UIKitLifecycleTestPanel reopened = null;
            UIKitLifecycleTestPanel first = UIKit.OpenPanel<UIKitLifecycleTestPanel>(
                cachePolicy: PanelCachePolicy.Transient);
            first.OnClosed(() => reopened = UIKit.OpenPanel<UIKitLifecycleTestPanel>(
                cachePolicy: PanelCachePolicy.Transient));

            first.Close();

            Assert.IsNotNull(reopened);
            Assert.AreNotSame(first, reopened);
            Assert.AreSame(reopened, UIKit.GetPanel<UIKitLifecycleTestPanel>());
            Assert.AreEqual(2, mLoader.SyncLoadCount);
            Assert.AreEqual(1, mLoader.LeaseDisposeCount);
            reopened.Close();
            Assert.AreEqual(2, mLoader.LeaseDisposeCount);
        }

        /// <summary>
        /// 等待异步面板调用进入取消终态，并拒绝吞掉非取消异常。
        /// </summary>
        /// <typeparam name="T">异步调用结果类型。</typeparam>
        /// <param name="task">预期由当前等待者令牌取消的任务。</param>
        private static async Task ObserveCancellation<T>(Task<T> task)
        {
            bool cancelled = false;
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 把公开 UniTask 入口转换为 NUnit 可直接等待的 Task。
        /// </summary>
        /// <typeparam name="T">异步结果类型。</typeparam>
        /// <param name="task">待转换的 UniTask。</param>
        /// <returns>保留原结果和异常语义的 Task。</returns>
        private static Task<T> AsTask<T>(UniTask<T> task)
        {
            return task.AsTask();
        }
#else
        /// <summary>
        /// 在未启用 UniTask 时直接返回公开 Task 入口。
        /// </summary>
        /// <typeparam name="T">异步结果类型。</typeparam>
        /// <param name="task">待等待的 Task。</param>
        /// <returns>原始 Task。</returns>
        private static Task<T> AsTask<T>(Task<T> task)
        {
            return task;
        }
#endif

        /// <summary>
        /// 表示生命周期测试传入面板的一份不可变业务数据。
        /// </summary>
        private sealed class UIKitTestData : IUIData
        {
            /// <summary>
            /// 创建带稳定标识的测试数据。
            /// </summary>
            /// <param name="id">用于区分打开轮次的标识。</param>
            internal UIKitTestData(string id)
            {
                Id = id;
            }

            /// <summary>获取当前测试数据标识。</summary>
            internal string Id { get; }
        }
    }
}
