using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using NUnit.Framework;
using System.Reflection;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 UIKit 的值契约和只读查询不会产生 Unity 场景副作用。
    /// </summary>
    public sealed class UIKitContractTests
    {
        /// <summary>
        /// 每个契约测试前移除已有 Root，确保查询副作用断言从空状态开始。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            UIRoot.Dispose();
        }

        /// <summary>
        /// 每个契约测试后再次移除 Root，避免失败用例污染其它测试。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UIRoot.Dispose();
        }

        /// <summary>
        /// 验证 default(UILevel) 等价于 Common，且预定义层级严格按排序值升序暴露。
        /// </summary>
        [Test]
        public void DefaultLevelIsCommonAndPredefinedLevelsAreSorted()
        {
            Assert.AreEqual(UILevel.Common, default(UILevel));
            Assert.AreEqual(0, default(UILevel).Order);

            IReadOnlyList<UILevel> levels = UILevel.PredefinedLevels;
            Assert.Greater(levels.Count, 1);
            for (var index = 1; index < levels.Count; index++)
            {
                Assert.Less(
                    levels[index - 1],
                    levels[index],
                    "预定义 UILevel 必须按 Order 严格升序排列。");
            }

            Assert.AreEqual(UILevel.AlwayBottom, levels[0]);
            Assert.AreEqual(UILevel.CanvasPanel, levels[levels.Count - 1]);
        }

        /// <summary>
        /// 验证全部公开查询在空状态返回稳定空值，并且不会隐式创建 UIRoot。
        /// </summary>
        [Test]
        public void ReadOnlyQueriesDoNotCreateRoot()
        {
            Assert.IsNull(UIKit.Root);
            Assert.IsFalse(UIKit.HasRoot);
            Assert.IsNull(UIKit.GetPanel<UIKitLifecycleTestPanel>());
            Assert.IsFalse(UIKit.IsPanelLoaded<UIKitLifecycleTestPanel>());
            Assert.IsFalse(UIKit.IsPanelPreloaded<UIKitLifecycleTestPanel>());
            Assert.IsEmpty(UIKit.GetLoadedPanelTypes());
            Assert.IsEmpty(UIKit.GetLoadedPanels());
            Assert.IsNull(UIKit.GetTopPanelAtLevel(UILevel.Common));
            Assert.IsNull(UIKit.GetGlobalTopPanel());
            Assert.IsEmpty(UIKit.GetPanelsAtLevel(UILevel.Common));
            Assert.AreEqual(0, UIKit.GetStackDepth());
            Assert.IsNull(UIKit.PeekPanel());
            Assert.IsEmpty(UIKit.GetAllStackNames());
            Assert.IsFalse(UIKit.HasModalBlocker());
            Assert.IsNull(UIKit.Root, "纯查询不得实例化 UIRoot 或 Canvas 层级。");
            Assert.IsFalse(UIKit.HasRoot);
        }

        /// <summary>
        /// 验证启动阶段可直接获取默认 loader 并在首次面板物化前配置可寻址 location。
        /// </summary>
        [Test]
        public void GetPanelLoaderCreatesRootForBootstrapConfiguration()
        {
            Assert.IsNull(UIKit.Root);
            Assert.IsFalse(UIKit.HasRoot);

            IPanelLoader loader = UIKit.GetPanelLoader();
            loader.UseAddressableLocation = true;

            Assert.IsNotNull(loader);
            Assert.IsTrue(loader.UseAddressableLocation);
            Assert.IsNotNull(UIKit.Root);
            Assert.IsTrue(UIKit.HasRoot);
        }

        /// <summary>
        /// 验证绑定有效场景 Camera 后，Root Canvas 会切换为由该 Camera 渲染的 Screen Space Camera 模式。
        /// </summary>
        [Test]
        public void BindRootCameraSwitchesCanvasToCameraRenderMode()
        {
            var cameraOwner = new GameObject("UIKitContractTestCamera");
            Camera camera = cameraOwner.AddComponent<Camera>();
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
                UnityEngine.Object.DestroyImmediate(cameraOwner);
            }
        }

        /// <summary>
        /// 验证预取消的 Open 与 Preload 在获取 Controller 前失败，不创建 Root、Canvas 或资源 lease。
        /// </summary>
        [Test]
        public async Task PreCanceledAsyncMutationsDoNotCreateRoot()
        {
            using CancellationTokenSource cancellation = new();
            cancellation.Cancel();

            await ObserveCancellation(AsTask(UIKit.OpenPanelAsync<UIKitLifecycleTestPanel>(
                ct: cancellation.Token)));
            Assert.IsNull(UIKit.Root);

            await ObserveCancellation(AsTask(UIKit.PreloadPanelAsync<UIKitLifecycleTestPanel>(
                ct: cancellation.Token)));
            Assert.IsNull(UIKit.Root);
        }

        /// <summary>验证 Dialog 异步公开入口跟随可选 UniTask 宏切换返回类型。</summary>
        [Test]
        public void DialogAsyncReturnTypeMatchesOptionalDependencyContract()
        {
            MethodInfo method = FindNonGenericDialogAsyncMethod();
            Assert.IsNotNull(method);
#if YOKIFRAME_UNITASK_SUPPORT
            Assert.AreEqual(typeof(UniTask<DialogResultData>), method.ReturnType);
#else
            Assert.AreEqual(typeof(Task<DialogResultData>), method.ReturnType);
#endif
        }

        /// <summary>从泛型和运行时 Type 重载中定位默认 Dialog 的非泛型公开签名。</summary>
        private static MethodInfo FindNonGenericDialogAsyncMethod()
        {
            MethodInfo[] methods = typeof(UIKit).GetMethods(BindingFlags.Public | BindingFlags.Static);
            for (var index = 0; index < methods.Length; index++)
            {
                MethodInfo method = methods[index];
                if (method.Name != nameof(UIKit.ShowDialogAsync) || method.IsGenericMethodDefinition) continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length == 2 && parameters[0].ParameterType == typeof(DialogConfig)) return method;
            }
            return null;
        }

        /// <summary>
        /// 等待预期取消的任务，并拒绝把其它异常误报为通过。
        /// </summary>
        private static async Task ObserveCancellation<T>(Task<T> task)
        {
            bool canceled = false;
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                canceled = true;
            }

            Assert.IsTrue(canceled);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 把公开 UniTask 转换为 NUnit 可等待的 Task。
        /// </summary>
        private static Task<T> AsTask<T>(UniTask<T> task)
        {
            return task.AsTask();
        }
#else
        /// <summary>
        /// 未启用 UniTask 时直接保留公开 Task。
        /// </summary>
        private static Task<T> AsTask<T>(Task<T> task)
        {
            return task;
        }
#endif
    }
}
