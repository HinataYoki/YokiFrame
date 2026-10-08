#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YokiFrame.Unity;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 SceneKit 宿主重置钩子会在会话开始时清除显式后端。
    /// </summary>
    public sealed class SceneKitHostResetTests
    {
        /// <summary>每个测试前清掉上一用例留下的显式后端，并恢复 SceneKit 会话登记。</summary>
        [SetUp]
        public void SetUp()
        {
            UnitySceneKitRuntimeInstaller.RegisterSceneKitResetForTests();
            SceneKit.Reset();
        }

        /// <summary>每个测试后恢复会话登记并清掉显式后端，避免空回调残留到其他测试。</summary>
        [TearDown]
        public void TearDown()
        {
            UnitySceneKitRuntimeInstaller.RegisterSceneKitResetForTests();
            SceneKit.Reset();
        }

        /// <summary>
        /// 验证宿主钩子会把被替换掉的 SceneKit 会话回调重新登记为 Reset。
        /// SceneKit 静态构造也会登记同名回调，因此必须先覆盖成空回调，否则钩子失效时测试仍会通过。
        /// </summary>
        [Test]
        public void RegisterSceneKitResetForTests_覆盖空回调后开始会话_清除显式后端()
        {
            ISceneBackend backend = new StubSceneBackend();
            SceneKit.SetBackend(backend);
            YokiFrameSession.Register(YokiFrameSession.RELEASE_HOSTS_ORDER, "scenekit", static () => { });

            UnitySceneKitRuntimeInstaller.RegisterSceneKitResetForTests();
            YokiFrameSession.Begin();

            Assert.AreNotSame(backend, SceneKit.GetBackend());
        }

        /// <summary>验证公开 Reset 会清除已设置的显式后端。</summary>
        [Test]
        public void Reset_已设置显式后端_清除该后端()
        {
            ISceneBackend backend = new StubSceneBackend();
            SceneKit.SetBackend(backend);

            SceneKit.Reset();

            Assert.AreNotSame(backend, SceneKit.GetBackend());
        }

        /// <summary>仅用于注入显式后端的最小场景后端替身。</summary>
        /// <summary>
        /// 验证会话重置方法仍由 Unity 在 SubsystemRegistration 阶段调用。
        /// 只读取 internal 测试缝上的 Unity 特性，不反射本仓库 private 方法。
        /// </summary>
        [Test]
        public void RegisterSceneKitResetForTests_SubsystemRegistration特性_仍由宿主调用()
        {
            MethodInfo method = typeof(UnitySceneKitRuntimeInstaller).GetMethod(
                nameof(UnitySceneKitRuntimeInstaller.RegisterSceneKitResetForTests),
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(method, "缺少会话重置测试缝");
            RuntimeInitializeOnLoadMethodAttribute attribute =
                method.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute, "会话重置必须带 RuntimeInitializeOnLoadMethodAttribute，否则宿主不会调用");
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attribute.loadType);
        }

        private sealed class StubSceneBackend : ISceneBackend
        {
            /// <summary>获取后端名称。</summary>
            public string BackendName => "StubSceneBackend";

            /// <summary>获取当前激活场景；替身始终返回默认值。</summary>
            public SceneHandle ActiveScene => default;

            /// <summary>替身不执行真实加载。</summary>
            /// <param name="request">加载请求。</param>
            /// <param name="onComplete">完成回调。</param>
            /// <param name="onProgress">进度回调。</param>
            /// <param name="onSuspended">挂起回调。</param>
            /// <returns>始终返回空，测试不会触发加载。</returns>
            public ISceneLoadOperation LoadSceneAsync(
                SceneLoadRequest request,
                Action<SceneLoadResult> onComplete,
                Action<float> onProgress,
                Action onSuspended)
            {
                return null;
            }

            /// <summary>替身不执行真实卸载。</summary>
            /// <param name="scene">目标场景。</param>
            /// <param name="onComplete">完成回调。</param>
            public void UnloadSceneAsync(SceneHandle scene, Action onComplete)
            {
            }

            /// <summary>替身不设置激活场景。</summary>
            /// <param name="scene">目标场景。</param>
            public void SetActiveScene(SceneHandle scene)
            {
            }

            /// <summary>替身不卸载资源。</summary>
            /// <param name="onComplete">完成回调。</param>
            public void UnloadUnusedAssets(Action onComplete)
            {
            }
        }
    }
}
#endif
