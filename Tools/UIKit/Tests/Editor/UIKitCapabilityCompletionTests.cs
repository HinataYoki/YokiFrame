using System.Collections.Generic;
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame.Tests
{
    /// <summary>验证 UIKit Dialog、焦点、布局组件、基础动画和 Runtime 诊断入口。</summary>
    public sealed class UIKitCapabilityCompletionTests
    {
        private UIKitTestPanelLoader mLoader;

        /// <summary>每个测试建立独立 UIRoot 与内存 Panel loader。</summary>
        [SetUp]
        public void SetUp()
        {
            UIRoot.Dispose();
            UIKitDialogTestPanel.ResetCounters();
            mLoader = new UIKitTestPanelLoader();
            UIKit.SetPanelLoader(mLoader);
        }

        /// <summary>每个测试释放受管实例和内存 Prefab。</summary>
        [TearDown]
        public void TearDown()
        {
            UIRoot.Dispose();
            mLoader.Dispose();
            mLoader = null;
        }

        /// <summary>验证 Dialog 串行显示、结果提交、模态状态和下一项恢复。</summary>
        [Test]
        public void DialogQueueSerializesResultsAndAdvancesAfterClose()
        {
            var results = new List<DialogResultData>();
            UIKit.SetDefaultDialogType<UIKitDialogTestPanel>();
            UIKit.ShowDialog(DialogConfig.Alert("first"), results.Add);
            UIKit.ShowDialog(DialogConfig.Confirm("second"), results.Add);

            Assert.IsTrue(UIKit.HasActiveDialog);
            Assert.AreEqual(1, UIKit.DialogQueueCount);
            Assert.AreEqual(1, UIKitDialogTestPanel.SetupCount);
            UIKitDialogTestPanel dialog = UIKit.GetPanel<UIKitDialogTestPanel>();
            Assert.IsNotNull(dialog);
            Assert.IsTrue(dialog.IsModal);

            dialog.Complete(DialogResult.OK);

            Assert.AreEqual(1, results.Count);
            Assert.AreEqual(DialogResult.OK, results[0].Result);
            Assert.IsTrue(UIKit.HasActiveDialog);
            Assert.AreEqual(0, UIKit.DialogQueueCount);
            Assert.AreEqual(2, UIKitDialogTestPanel.SetupCount);
            UIKit.GetPanel<UIKitDialogTestPanel>().Complete(DialogResult.Cancel);
            Assert.IsFalse(UIKit.HasActiveDialog);
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(DialogResult.Cancel, results[1].Result);
        }

        /// <summary>验证清空队列会向尚未显示的请求提交 Cancel。</summary>
        [Test]
        public void ClearDialogQueueCancelsWaitingRequests()
        {
            DialogResultData waitingResult = null;
            UIKit.SetDefaultDialogType<UIKitDialogTestPanel>();
            UIKit.ShowDialog(DialogConfig.Alert("active"));
            UIKit.ShowDialog(DialogConfig.Alert("waiting"), result => waitingResult = result);

            UIKit.ClearDialogQueue();

            Assert.IsNotNull(waitingResult);
            Assert.AreEqual(DialogResult.Cancel, waitingResult.Result);
            Assert.AreEqual(0, UIKit.DialogQueueCount);
            UIKit.GetPanel<UIKitDialogTestPanel>().Complete(DialogResult.Cancel);
        }

        /// <summary>验证 EventSystem 缺失时可创建，并能设置与清除 Selectable 焦点。</summary>
        [Test]
        public void FocusApiCreatesEventSystemAndTracksSelection()
        {
            GameObject target = new("FocusTarget", typeof(RectTransform), typeof(Button));
            try
            {
                target.transform.SetParent(UIKit.Root.Canvas.transform, false);
                Button button = target.GetComponent<Button>();
                EventSystem eventSystem = UIKit.EnsureEventSystem();

                Assert.IsNotNull(eventSystem);
                Assert.IsTrue(UIKit.SetFocus(button));
                Assert.AreSame(target, UIKit.CurrentFocus);
                Assert.AreEqual(UIInputMode.Navigation, UIKit.InputMode);
                UIKit.ClearFocus();
                Assert.IsNull(UIKit.CurrentFocus);
                Assert.AreEqual(UIInputMode.Pointer, UIKit.InputMode);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }

#if YOKIFRAME_INPUTSYSTEM_SUPPORT && !ENABLE_LEGACY_INPUT_MANAGER
        /// <summary>验证 Input System-only 项目在 Root 初始化时安装新输入模块。</summary>
        [Test]
        public void RootInitializationUsesInputSystemUiModuleWhenLegacyInputIsDisabled()
        {
            UIRoot.Dispose();
            Type installerType = Type.GetType(
                "YokiFrame.UIKitInputSystemModuleInstaller, YokiFrame.UIKit.InputSystem");
            Assert.IsNotNull(installerType, "UIKit Input System Integration 未加载输入模块安装器。");
            MethodInfo install = installerType.GetMethod(
                "Install",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(install);
            install.Invoke(null, null);

            UIRoot root = UIRoot.Instance;
            BaseInputModule inputModule = root.EventSystem.GetComponent<BaseInputModule>();

            Assert.IsNotNull(inputModule);
            Assert.AreEqual(
                "UnityEngine.InputSystem.UI.InputSystemUIInputModule",
                inputModule.GetType().FullName);
            PropertyInfo actionsAssetProperty = inputModule.GetType().GetProperty("actionsAsset");
            Assert.IsNotNull(actionsAssetProperty);
            Assert.IsNotNull(actionsAssetProperty.GetValue(inputModule));
        }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
        /// <summary>验证启用 Legacy Input Manager 时 Root 初始化使用 StandaloneInputModule。</summary>
        [Test]
        public void RootInitializationUsesStandaloneModuleWhenLegacyInputIsEnabled()
        {
            UIRoot.Dispose();

            UIRoot root = UIRoot.Instance;
            BaseInputModule inputModule = root.EventSystem.GetComponent<BaseInputModule>();

            Assert.IsInstanceOf<StandaloneInputModule>(inputModule);
        }
#endif

        /// <summary>验证动态 Canvas、批处理提示和零时长基础动画的公开行为。</summary>
        [Test]
        public void LayoutHelpersAndBaseAnimationApplyExpectedState()
        {
            GameObject root = new("Dynamic", typeof(RectTransform));
            GameObject child = new("Button", typeof(RectTransform), typeof(Button));
            try
            {
                root.transform.SetParent(UIKit.Root.Canvas.transform, false);
                child.transform.SetParent(root.transform, false);
                UIDynamicElement dynamicElement = root.AddComponent<UIDynamicElement>();
                dynamicElement.Initialize();
                Assert.IsNotNull(dynamicElement.Canvas);
                Assert.IsNotNull(root.GetComponent<GraphicRaycaster>());

                CanvasBatchHint hint = root.AddComponent<CanvasBatchHint>();
                hint.SetSortingOrder(12);
                Assert.AreEqual(12, hint.Canvas.sortingOrder);
                Assert.IsTrue(hint.Canvas.overrideSorting);

                bool completed = false;
                var animation = new FadeAnimation(0f, 0f, 1f);
                animation.Play(root.GetComponent<RectTransform>(), () => completed = true);
                Assert.IsTrue(completed);
                Assert.AreEqual(1f, root.GetComponent<CanvasGroup>().alpha);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证多个 Editor 安全区模拟不会通过静态缓存互相覆盖。</summary>
        [Test]
        public void SafeAreaEditorSimulationsRemainInstanceSpecific()
        {
            GameObject firstObject = new("FirstSafeArea", typeof(RectTransform), typeof(SafeAreaAdapter));
            GameObject secondObject = new("SecondSafeArea", typeof(RectTransform), typeof(SafeAreaAdapter));
            try
            {
                SafeAreaAdapter first = firstObject.GetComponent<SafeAreaAdapter>();
                SafeAreaAdapter second = secondObject.GetComponent<SafeAreaAdapter>();
                ConfigureEditorSafeAreaSimulation(first, new Vector4(10f, 20f, 30f, 40f));
                ConfigureEditorSafeAreaSimulation(second, new Vector4(50f, 60f, 70f, 80f));

                Rect firstSafeArea = first.CurrentSafeArea;
                Rect secondSafeArea = second.CurrentSafeArea;

                Assert.AreEqual(10f, firstSafeArea.x);
                Assert.AreEqual(40f, firstSafeArea.y);
                Assert.AreEqual(50f, secondSafeArea.x);
                Assert.AreEqual(80f, secondSafeArea.y);
                Assert.AreNotEqual(firstSafeArea, secondSafeArea);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }

        /// <summary>验证 Runtime 诊断快照不创建额外 Root，并反映已打开面板。</summary>
        [Test]
        public void RuntimeDiagnosticsReflectLoadedPanels()
        {
            UIKitRuntimeSnapshot before = UIKit.CaptureRuntimeDiagnostics();
            Assert.IsTrue(before.HasRoot, "SetPanelLoader 已按变更语义创建测试 Root。");
            UIKit.OpenPanel<UIKitNavigationFirstTestPanel>();

            UIKitRuntimeSnapshot snapshot = UIKit.CaptureRuntimeDiagnostics();

            Assert.AreEqual(1, snapshot.LoadedPanelCount);
            Assert.AreEqual(1, snapshot.VisiblePanelCount);
            StringAssert.Contains(nameof(UIKitNavigationFirstTestPanel), snapshot.Panels[0]);
        }

        /// <summary>验证 Show/Hide 动画进入过渡状态，且反向操作拒绝旧 generation 回调。</summary>
        [Test]
        public void PanelTransitionsIgnoreLateCallbacksAfterDirectionChanges()
        {
            UIKitNavigationFirstTestPanel panel = UIKit.OpenPanel<UIKitNavigationFirstTestPanel>();
            panel.Hide();
            var showAnimation = new ManualAnimation();
            var hideAnimation = new ManualAnimation();
            panel.SetShowAnimation(showAnimation);
            panel.SetHideAnimation(hideAnimation);

            panel.Show();
            Assert.AreEqual(PanelState.Opening, panel.State);
            showAnimation.Complete();
            Assert.AreEqual(PanelState.Open, panel.State);

            panel.Hide();
            Assert.AreEqual(PanelState.Hiding, panel.State);
            panel.Show();
            Assert.AreEqual(PanelState.Opening, panel.State);
            hideAnimation.Complete();
            Assert.AreEqual(PanelState.Opening, panel.State, "旧 Hide 回调不能覆盖新的 Show generation。");
            showAnimation.Complete();
            Assert.AreEqual(PanelState.Open, panel.State);
        }

        /// <summary>验证配置工厂与组合动画保留时长、类型和顺序/并行完成语义。</summary>
        [Test]
        public void AnimationFactoryCreatesConfiguredAndCompositeAnimations()
        {
            var fadeConfig = new FadeAnimationConfig
            {
                Duration = 0.2f,
                Curve = AnimationCurve.Linear(0f, 0f, 1f, 1f),
                FromAlpha = 0.1f,
                ToAlpha = 0.9f
            };
            IUIAnimation configured = UIAnimationFactory.Create(fadeConfig);
            Assert.IsInstanceOf<FadeAnimation>(configured);
            Assert.AreEqual(0.2f, configured.Duration);

            var parallelFirst = new ManualAnimation(0.2f);
            var parallelSecond = new ManualAnimation(0.4f);
            CompositeAnimation parallel = UIAnimationFactory.CreateParallel()
                .Add(parallelFirst)
                .Add(parallelSecond);
            bool parallelCompleted = false;
            parallel.Play(null, () => parallelCompleted = true);
            Assert.IsTrue(parallelCompleted, "空目标应同步提交组合动画终态。");
            Assert.AreEqual(0.4f, parallel.Duration);

            var target = new GameObject("CompositeTarget", typeof(RectTransform));
            try
            {
                var first = new ManualAnimation(0.2f);
                var second = new ManualAnimation(0.4f);
                CompositeAnimation sequential = UIAnimationFactory.CreateSequential()
                    .Add(first)
                    .Add(second);
                bool sequentialCompleted = false;
                sequential.Play(target.GetComponent<RectTransform>(), () => sequentialCompleted = true);
                Assert.IsTrue(first.IsPlaying);
                Assert.IsFalse(second.IsPlaying);
                first.Complete();
                Assert.IsTrue(second.IsPlaying);
                second.Complete();
                Assert.IsTrue(sequentialCompleted);
                sequential.Recycle();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
                configured.Recycle();
                parallel.Recycle();
            }
        }

        /// <summary>验证隐藏面板会记住子控件焦点，并在导航模式下重新显示时恢复。</summary>
        [Test]
        public void PanelFocusMemoryRestoresSelectionAfterHideAndShow()
        {
            UIKitNavigationFirstTestPanel panel = UIKit.OpenPanel<UIKitNavigationFirstTestPanel>();
            var buttonObject = new GameObject("RememberedButton", typeof(RectTransform), typeof(Button));
            buttonObject.transform.SetParent(panel.transform, false);
            Button button = buttonObject.GetComponent<Button>();
            panel.SetAutoFocusOnShow(true);
            panel.SetDefaultSelectable(button);

            UIKit.SetInputMode(UIInputMode.Navigation);
            Assert.IsTrue(UIKit.SetFocus(button));
            panel.Hide();
            Assert.IsNull(UIKit.CurrentFocus);
            Assert.AreEqual(UIInputMode.Navigation, UIKit.InputMode);

            panel.Show();
            Assert.AreSame(buttonObject, UIKit.CurrentFocus);
        }

        /// <summary>验证共享导航状态机处理死区、首次输入、重复延迟、持续重复和非法轴。</summary>
        [Test]
        public void NavigationRepeatStateUsesConfiguredDeadzoneAndTiming()
        {
            var state = new UINavigationRepeatState();
            GamepadConfig config = GamepadConfig.Default;

            Assert.IsTrue(state.TryGetMove(Vector2.right, 0f, config, out MoveDirection first));
            Assert.AreEqual(MoveDirection.Right, first);
            Assert.IsFalse(state.TryGetMove(Vector2.right, 0.2f, config, out _));
            Assert.IsTrue(state.TryGetMove(Vector2.right, 0.2f, config, out MoveDirection repeated));
            Assert.AreEqual(MoveDirection.Right, repeated);
            Assert.IsFalse(state.TryGetMove(Vector2.right, 0.05f, config, out _));
            Assert.IsTrue(state.TryGetMove(Vector2.right, 0.06f, config, out _));
            Assert.IsFalse(state.TryGetMove(Vector2.one * 0.1f, 1f, config, out _));
            Assert.IsTrue(state.TryGetMove(Vector2.up, 0f, config, out MoveDirection changed));
            Assert.AreEqual(MoveDirection.Up, changed);
            Assert.IsFalse(state.TryGetMove(new Vector2(float.NaN, 1f), 1f, config, out _));
        }

        /// <summary>验证可注入 GamepadNavigator 的移动、确认和按钮边沿契约。</summary>
        [Test]
        public void GamepadNavigatorProcessesInjectedInputWithoutInputSystemDependency()
        {
            GameObject leftObject = new("Left", typeof(RectTransform), typeof(Button));
            GameObject rightObject = new("Right", typeof(RectTransform), typeof(Button));
            try
            {
                leftObject.transform.SetParent(UIKit.Root.Canvas.transform, false);
                rightObject.transform.SetParent(UIKit.Root.Canvas.transform, false);
                Button left = leftObject.GetComponent<Button>();
                Button right = rightObject.GetComponent<Button>();
                Navigation navigation = left.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnRight = right;
                left.navigation = navigation;

                var input = new TestGamepadInput { NavigationAxis = Vector2.right };
                using var navigator = new GamepadNavigator(
                    input,
                    GamepadConfig.Default,
                    UIKit.EnsureEventSystem());
                int navigateCount = 0;
                int submitCount = 0;
                int cancelCount = 0;
                navigator.OnNavigate += _ => navigateCount++;
                navigator.OnSubmit += () => submitCount++;
                navigator.OnCancel += () => cancelCount++;
                navigator.SetFocus(left);
                navigator.Enable();

                navigator.Update(0f);
                Assert.AreSame(rightObject, navigator.CurrentFocus);
                Assert.AreEqual(1, navigateCount);
                input.NavigationAxis = Vector2.zero;
                input.SubmitPressed = true;
                input.CancelPressed = true;
                navigator.Update(0f);
                navigator.Update(0f);
                Assert.AreEqual(1, submitCount, "持续按下只能提交一次确认边沿。");
                Assert.AreEqual(1, cancelCount, "持续按下只能提交一次取消边沿。");
                navigator.Disable();
                Assert.IsTrue(input.EnabledOnce);
                Assert.IsTrue(input.DisabledOnce);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(leftObject);
                UnityEngine.Object.DestroyImmediate(rightObject);
            }
        }

        /// <summary>通过 SerializedObject 配置仅 Editor 可用的安全区模拟字段。</summary>
        private static void ConfigureEditorSafeAreaSimulation(
            SafeAreaAdapter adapter,
            Vector4 insets)
        {
            SerializedObject serialized = new(adapter);
            serialized.FindProperty("mSimulateInEditor").boolValue = true;
            serialized.FindProperty("mSimulatedInsets").vector4Value = insets;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>由测试显式完成的动画，用于稳定验证 transition generation。</summary>
        private sealed class ManualAnimation : IUIAnimation
        {
            private Action mCompletion;

            /// <summary>创建指定时长的手动动画。</summary>
            internal ManualAnimation(float duration = 1f)
            {
                Duration = duration;
            }

            /// <inheritdoc />
            public float Duration { get; }

            /// <inheritdoc />
            public bool IsPlaying { get; private set; }

            /// <inheritdoc />
            public void Play(RectTransform target, Action onComplete = null)
            {
                IsPlaying = true;
                mCompletion = onComplete;
            }

            /// <inheritdoc />
            public void Stop()
            {
                IsPlaying = false;
                mCompletion = null;
            }

            /// <inheritdoc />
            public void Reset(RectTransform target) { }

            /// <inheritdoc />
            public void SetToEndState(RectTransform target) { }

            /// <inheritdoc />
            public void Recycle()
            {
                Stop();
            }

            /// <summary>触发最近一次播放保存的完成回调。</summary>
            internal void Complete()
            {
                IsPlaying = false;
                Action completion = mCompletion;
                mCompletion = null;
                if (completion != null) completion();
            }
        }

        /// <summary>为 GamepadNavigator 测试提供可变且无第三方依赖的输入快照。</summary>
        private sealed class TestGamepadInput : IGamepadInput
        {
            public Vector2 NavigationAxis { get; set; }
            public bool SubmitPressed { get; set; }
            public bool CancelPressed { get; set; }
            public bool TabLeftPressed { get; set; }
            public bool TabRightPressed { get; set; }
            public bool TriggerLeftPressed { get; set; }
            public bool TriggerRightPressed { get; set; }
            public bool MenuPressed { get; set; }
            public Vector2 MouseDelta { get; set; }
            public bool MouseLeftPressed { get; set; }
            public bool IsGamepadConnected { get; set; }
            public bool EnabledOnce { get; private set; }
            public bool DisabledOnce { get; private set; }

            /// <summary>记录导航器已启用输入。</summary>
            public void Enable()
            {
                EnabledOnce = true;
            }

            /// <summary>记录导航器已禁用输入。</summary>
            public void Disable()
            {
                DisabledOnce = true;
            }
        }
    }
}
