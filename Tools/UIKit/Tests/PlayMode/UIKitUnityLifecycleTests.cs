#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 使用真实 Unity GameObject 验证 UIKit 生命周期、主线程和 Root 重建边界。
    /// </summary>
    public sealed partial class UIKitUnityLifecycleTests
    {
        private GameObject mPrefab;
        private ImmediatePanelLoader mLoader;

        /// <summary>
        /// 每条测试创建独立 Panel 模板与 loader，并清空静态生命周期计数。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            UIKitPlayModePanel.ResetCounters();
            mPrefab = new GameObject("UIKitPlayModePanelPrefab", typeof(RectTransform), typeof(UIKitPlayModePanel));
            mPrefab.SetActive(false);
            UIKitPlayModePanel.ResetCounters();
            mLoader = new ImmediatePanelLoader(mPrefab);
            UIKit.SetPanelLoader(mLoader);
        }

        /// <summary>
        /// 每条测试销毁 Root 和模板，并等待 Unity 提交延迟 Destroy。
        /// </summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            UIRoot.Dispose();
            if (mPrefab != default) UnityEngine.Object.Destroy(mPrefab);
            yield return null;
        }

        /// <summary>
        /// 验证真实 Awake、OnInit、显示、关闭和 OnDestroy 顺序及 lease 释放。
        /// </summary>
        [UnityTest]
        public IEnumerator OpenCloseUsesRealUnityLifecycle()
        {
            UIKitPlayModePanel panel = UIKit.OpenPanel<UIKitPlayModePanel>(
                cachePolicy: PanelCachePolicy.Transient);

            Assert.IsNotNull(panel);
            Assert.AreEqual(1, UIKitPlayModePanel.AwakeCount);
            Assert.AreEqual(1, UIKitPlayModePanel.InitCount);
            Assert.AreEqual(1, UIKitPlayModePanel.OpenCount);
            Assert.AreEqual(1, UIKitPlayModePanel.ShowCount);
            Assert.IsTrue(panel.gameObject.activeInHierarchy);
            Assert.AreEqual(mPrefab.name, panel.gameObject.name, "面板实例不应保留 Unity 自动追加的 (Clone)。");

            UIKit.ClosePanel(panel);
            Assert.AreEqual(1, UIKitPlayModePanel.CloseCount);
            Assert.AreEqual(1, mLoader.ReleaseCount);
            yield return null;

            Assert.AreEqual(1, UIKitPlayModePanel.DestroyCount);
            Assert.IsFalse(UIKit.IsPanelLoaded<UIKitPlayModePanel>());
        }

        /// <summary>
        /// 验证显式绑定有效 Camera 会把默认 Overlay Root 切换为可由该 Camera 渲染的模式。
        /// </summary>
        [Test]
        public void BindRootCameraSwitchesCanvasToScreenSpaceCamera()
        {
            var cameraOwner = new GameObject("UIKitTestCamera", typeof(Camera));
            Camera camera = cameraOwner.GetComponent<Camera>();
            try
            {
                UIKit.BindRootCamera(camera);

                UIRoot root = UIKit.Root;
                Assert.IsNotNull(root);
                Assert.AreEqual(RenderMode.ScreenSpaceCamera, root.Canvas.renderMode);
                Assert.AreSame(camera, root.Canvas.worldCamera);
            }
            finally
            {
                UnityEngine.Object.Destroy(cameraOwner);
            }
        }

        /// <summary>
        /// 验证后台完成 loader Task 后仍在 Unity 主线程 Instantiate，且并发 Open 共享实例。
        /// </summary>
        [Test]
        public async Task ConcurrentOpenResumesOnMainThreadAndSharesInstance()
        {
            int mainThreadId = Thread.CurrentThread.ManagedThreadId;
            var deferredLoader = new DeferredPanelLoader(mPrefab);
            UIKit.SetPanelLoader(deferredLoader);

            var first = UIKit.OpenPanelAsync<UIKitPlayModePanel>();
            var second = UIKit.OpenPanelAsync<UIKitPlayModePanel>();
            Assert.AreEqual(1, deferredLoader.AsyncLoadCount);

            await Task.Run(deferredLoader.Complete);
            UIKitPlayModePanel firstPanel = await first;
            UIKitPlayModePanel secondPanel = await second;

            Assert.AreSame(firstPanel, secondPanel);
            Assert.AreEqual(1, UIKitPlayModePanel.AwakeCount);
            Assert.AreEqual(mainThreadId, firstPanel.CreationThreadId);
        }

        /// <summary>
        /// 验证 Root teardown 会立即取消公开等待者，且忽略取消令牌的迟到 lease 不会复活旧 Panel。
        /// </summary>
        [UnityTest]
        public IEnumerator DisposeCancelsPendingLoadAndIgnoresLateResult()
        {
            var deferredLoader = new IgnoringCancellationPanelLoader(mPrefab);
            UIKit.SetPanelLoader(deferredLoader);
            Task<UIKitPlayModePanel> pending = AsTask(UIKit.OpenPanelAsync<UIKitPlayModePanel>());
            UIRoot firstRoot = UIKit.Root;
            Assert.IsNotNull(firstRoot);
            Assert.AreEqual(1, deferredLoader.AsyncLoadCount);

            UIRoot.Dispose();
            deferredLoader.Complete();
            yield return new WaitUntil(() => pending.IsCompleted);
            // UniTask 公开边界不保留 TPL Canceled 状态位；取消以 OperationCanceledException 故障形式暴露。
            Assert.IsTrue(pending.IsFaulted, "Root teardown 必须立即让无调用方令牌的等待者以取消异常结束。");
            Assert.IsInstanceOf<OperationCanceledException>(pending.Exception?.GetBaseException(), "迟到结果必须以取消异常呈现给调用方。");

            UIKit.SetPanelLoader(mLoader);
            UIRoot secondRoot = UIKit.Root;
            UIKitPlayModePanel currentPanel = UIKit.OpenPanel<UIKitPlayModePanel>();
            Assert.AreNotSame(firstRoot, secondRoot);
            Assert.AreSame(currentPanel, UIKit.GetPanel<UIKitPlayModePanel>());

            yield return null;
            yield return null;

            Assert.AreSame(currentPanel, UIKit.GetPanel<UIKitPlayModePanel>(), "迟到结果不得登记旧 controller 的孤儿 Panel。");
            Assert.AreEqual(1, UIKitPlayModePanel.AwakeCount, "迟到结果不得再次 Instantiate Panel。");
            Assert.AreEqual(1, deferredLoader.LeaseReleaseCount, "迟到成功 lease 必须由旧 flight 释放。");
        }

        /// <summary>
        /// 验证销毁 Root 会释放旧 owner，下一轮操作可以创建全新的 Root 和 Panel。
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyRootResetsStaticStateAndAllowsRecreation()
        {
            UIKitPlayModePanel firstPanel = UIKit.OpenPanel<UIKitPlayModePanel>();
            UIRoot firstRoot = UIKit.Root;
            Assert.IsNotNull(firstPanel);
            Assert.IsNotNull(firstRoot);

            UIRoot.Dispose();
            UIKit.SetPanelLoader(mLoader);
            UIKitPlayModePanel secondPanel = UIKit.OpenPanel<UIKitPlayModePanel>();
            UIRoot secondRoot = UIKit.Root;
            Assert.IsNotNull(secondPanel);
            Assert.AreNotSame(firstRoot, secondRoot, "同帧重建不得重新登记待销毁的旧 Root。");
            Assert.AreEqual(2, UIKitPlayModePanel.AwakeCount);

            yield return null;
            Assert.AreSame(secondRoot, UIKit.Root);
            Assert.IsTrue(ScreenInfo.IsInitialized, "旧 Root 的迟到 OnDestroy 不得关闭新 Root 的屏幕状态会话。");
            Assert.AreEqual(1, mLoader.ReleaseCount);
        }

        /// <summary>
        /// 验证 Root teardown 回调重入时仍暴露已释放 controller，而不会误创建第二个 Root。
        /// </summary>
        [UnityTest]
        public IEnumerator DisposeKeepsDisposedControllerVisibleDuringTeardownCallbacks()
        {
            UIKit.OpenPanel<UIKitPlayModePanel>();
            UIRoot firstRoot = UIKit.Root;
            UIRoot observedRoot = null;
            Exception reentrantException = null;
            UIKitPlayModePanel.BeforeDestroyAction = () =>
            {
                observedRoot = UIKit.Root;
                try
                {
                    UIKit.OpenPanel<UIKitPlayModePanel>();
                }
                catch (Exception exception)
                {
                    reentrantException = exception;
                }
            };

            UIRoot.Dispose();

            Assert.AreSame(firstRoot, observedRoot);
            Assert.IsInstanceOf<ObjectDisposedException>(reentrantException);
            Assert.IsFalse(UIKit.HasRoot);
            yield return null;
        }

        /// <summary>
        /// 验证旧版 UIKit/UIRoot/EventSystem/UICamera 层级可以承载当前运行时，并按需启用输入节点。
        /// </summary>
        [UnityTest]
        public IEnumerator LegacyRootPrefabUsesCurrentRuntimeWithLegacyHierarchy()
        {
            UIRoot.Dispose();
            yield return null;

            GameObject prefab = Resources.Load<GameObject>("UIKit");
            Assert.IsNotNull(prefab, "UIKit 根 Prefab 未进入 Resources。");
            GameObject instance = UnityEngine.Object.Instantiate(prefab);
            try
            {
                yield return null;
                UIRoot root = UIKit.Root;
                Assert.IsNotNull(root);
                Assert.AreEqual("UIRoot", root.name);
                Assert.AreSame(instance.transform, root.transform.parent);
                Assert.AreSame(root.GetComponent<Canvas>(), root.Canvas);

                Transform storage = root.transform.Find("Storage");
                Assert.IsNotNull(storage);
                Assert.IsFalse(storage.gameObject.activeSelf);
                Assert.IsFalse(instance.transform.Find("UICamera").gameObject.activeSelf);

                EventSystem eventSystem = UIKit.EnsureEventSystem();
                Assert.AreSame(instance.transform.Find("EventSystem"), eventSystem.transform);
                Assert.IsTrue(eventSystem.gameObject.activeSelf);
                Assert.IsNotNull(eventSystem.GetComponent<BaseInputModule>());
            }
            finally
            {
                if (UIRoot.HasInstance) UIRoot.Dispose();
                else if (instance != default) UnityEngine.Object.Destroy(instance);
            }

            yield return null;
        }

        /// <summary>
        /// 验证 Play Mode 延迟 Destroy 前模态 blocker 已同步失活，不会继续截获本帧射线。
        /// </summary>
        [UnityTest]
        public IEnumerator RemovingModalBlockerDisablesItBeforeDelayedDestroy()
        {
            UIKitPlayModePanel panel = UIKit.OpenPanel<UIKitPlayModePanel>(
                cachePolicy: PanelCachePolicy.Persistent);
            UIKit.SetPanelModal(panel, true);
            Transform panelTransform = panel.transform;
            GameObject blocker = panelTransform.parent
                .GetChild(panelTransform.GetSiblingIndex() - 1)
                .gameObject;

            UIKit.SetPanelModal(panel, false);

            Assert.IsFalse(blocker.activeSelf);
            Assert.IsFalse(UIKit.HasModalBlocker());
            yield return null;
            Assert.IsTrue(blocker == default);
        }

        /// <summary>
        /// 验证外部只销毁 Panel 组件时，LateUpdate 扫描仍释放 lease、通知 OnBeforeDestroy 并清理实例。
        /// </summary>
        [UnityTest]
        public IEnumerator DestroyingOnlyPanelComponentDoesNotLeakOwnership()
        {
            UIKitPlayModePanel panel = UIKit.OpenPanel<UIKitPlayModePanel>(
                cachePolicy: PanelCachePolicy.Persistent);
            GameObject instance = panel.gameObject;

            UnityEngine.Object.Destroy(panel);
            yield return null;
            yield return null;

            Assert.IsFalse(UIKit.IsPanelLoaded<UIKitPlayModePanel>());
            Assert.AreEqual(1, mLoader.ReleaseCount);
            Assert.AreEqual(1, UIKitPlayModePanel.BeforeDestroyCount);
            Assert.IsTrue(instance == default);
        }

        /// <summary>
        /// 验证通用 MonoSingleton Dispose 后在同帧读取 Instance 会跳过待销毁旧对象。
        /// </summary>
        [UnityTest]
        public IEnumerator MonoSingletonDisposeAndRecreateInSameFrameUsesNewInstance()
        {
            MonoSingletonSameFrameTestComponent first = MonoSingletonSameFrameTestComponent.Instance;
            MonoSingletonSameFrameTestComponent.Dispose();
            MonoSingletonSameFrameTestComponent second = MonoSingletonSameFrameTestComponent.Instance;

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            yield return null;
            Assert.AreSame(second, MonoSingletonSameFrameTestComponent.Instance);

            MonoSingletonSameFrameTestComponent.Dispose();
            yield return null;
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 将公开 UniTask 面板请求转换为 Unity 测试可观察的 Task。
        /// </summary>
        private static Task<T> AsTask<T>(UniTask<T> task)
        {
            return task.AsTask();
        }
#else
        /// <summary>
        /// 在未启用 UniTask 时直接返回公开 Task 面板请求。
        /// </summary>
        private static Task<T> AsTask<T>(Task<T> task)
        {
            return task;
        }
#endif

    }
}
#endif
