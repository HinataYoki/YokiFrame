#if UNITY_EDITOR && UNITY_INCLUDE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using YokiFrame.Unity;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 守护 SceneKit 在关闭 Domain Reload 后重进 Play Mode 时会清理上一会话的静态场景状态。
    /// </summary>
    public sealed class SceneKitHostResetTests
    {
        /// <summary>宿主钩子的方法名，反射断言与调用共用。</summary>
        private const string HOOK_METHOD_NAME = "ResetSceneKitState";

        /// <summary>
        /// 验证 Unity 适配器存在且注册在 SubsystemRegistration 阶段。
        /// 缺少该钩子时，sSceneCache / sLoadedScenes / sActiveSceneHandler 会跨 Play 会话存活。
        /// </summary>
        [Test]
        public void SubsystemRegistrationHookIsDeclaredOnUnityAdapter()
        {
            MethodInfo hook = GetHookMethod();

            RuntimeInitializeOnLoadMethodAttribute attribute =
                hook.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

            Assert.IsNotNull(attribute, "宿主重置钩子必须带有 RuntimeInitializeOnLoadMethodAttribute");
            Assert.AreEqual(
                RuntimeInitializeLoadType.SubsystemRegistration,
                attribute!.loadType,
                "钩子必须注册在 SubsystemRegistration 阶段，才能在新会话最早时机清理上一会话状态");
        }

        /// <summary>
        /// 验证钩子确实调用 SceneKit.Reset()，这是 C9 的回归断言。
        /// 显式后端是公开可注入且被 Reset 清除的状态，因此用它作为可观测代理。
        /// </summary>
        [Test]
        public void SubsystemRegistrationHookResetsSceneKitState()
        {
            ISceneBackend backend = new StubSceneBackend();
            SceneKit.SetBackend(backend);
            Assert.AreSame(backend, SceneKit.GetBackend(), "前置：显式后端应已生效");

            InvokeHook();

            Assert.AreNotSame(
                backend,
                SceneKit.GetBackend(),
                "钩子必须调用 SceneKit.Reset()，否则显式后端会跨 Play 会话残留");
        }

        /// <summary>
        /// 验证公开的 SceneKit.Reset() 语义未被改动：它仍然清除显式后端。
        /// </summary>
        [Test]
        public void PublicResetStillClearsExplicitBackend()
        {
            ISceneBackend backend = new StubSceneBackend();
            SceneKit.SetBackend(backend);

            SceneKit.Reset();

            Assert.AreNotSame(backend, SceneKit.GetBackend(), "公开 Reset 必须清除显式后端");
        }

        /// <summary>获取私有宿主钩子方法。</summary>
        /// <returns>钩子方法信息。</returns>
        private static MethodInfo GetHookMethod()
        {
            MethodInfo hook = typeof(UnitySceneKitRuntimeInstaller).GetMethod(
                HOOK_METHOD_NAME,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(hook, "UnitySceneKitRuntimeInstaller 缺少宿主重置钩子 " + HOOK_METHOD_NAME);
            return hook!;
        }

        /// <summary>直接调用私有宿主钩子，避免依赖 Play Mode 进入时机。</summary>
        private static void InvokeHook()
        {
            _ = GetHookMethod().Invoke(null, null);
        }

        /// <summary>仅用于注入显式后端的最小场景后端替身。</summary>
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
