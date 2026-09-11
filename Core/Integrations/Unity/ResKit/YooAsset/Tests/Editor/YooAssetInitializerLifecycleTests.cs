#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Unity.Tests
{
    /// <summary>
    /// 守护 YooAsset 门面在关闭 Domain Reload 后重进 Play Mode 时会重新初始化并重新安装 ResKit Provider。
    /// </summary>
    public sealed class YooAssetInitializerLifecycleTests
    {
        /// <summary>宿主钩子的方法名，反射断言与调用共用。</summary>
        private const string HOOK_METHOD_NAME = "ResetRegistrationOnSubsystemRegistration";

        /// <summary>
        /// 验证宿主钩子存在且注册在 SubsystemRegistration 阶段。
        /// 缺少该钩子时，关闭 Domain Reload 后 IsInitialized 会跨会话残留，
        /// 使 InitializeAsync 静默提前返回、ResKit 静默退回 Unity Resources。
        /// </summary>
        [Test]
        public void SubsystemRegistrationHookIsDeclaredOnInitializer()
        {
            MethodInfo hook = GetHookMethod();

            RuntimeInitializeOnLoadMethodAttribute attribute =
                hook.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();

            Assert.IsNotNull(attribute, "宿主重置钩子必须带有 RuntimeInitializeOnLoadMethodAttribute");
            Assert.AreEqual(
                RuntimeInitializeLoadType.SubsystemRegistration,
                attribute!.loadType,
                "钩子必须注册在 SubsystemRegistration 阶段，才能在新会话最早时机清除上一会话状态");
        }

        /// <summary>
        /// 验证钩子确实清除上一会话的 IsInitialized 标志，这是 C36 的回归断言。
        /// </summary>
        [Test]
        public void SubsystemRegistrationHookClearsInitializedFlag()
        {
            SetIsInitialized(true);
            Assert.IsTrue(YooAssetInitializer.IsInitialized, "前置：测试需要先注入上一会话的已初始化状态");

            InvokeHook();

            Assert.IsFalse(
                YokiFrame.Unity.YooAssetInitializer.IsInitialized,
                "钩子必须清除 IsInitialized，否则 InitializeAsync 会静默提前返回");
            Assert.IsNull(YooAssetInitializer.DefaultPackage, "钩子应同时释放上一会话的默认 package 引用");
            Assert.IsNull(YooAssetInitializer.DefaultPackageName, "钩子应同时释放上一会话的默认 package 名称");
            Assert.AreEqual(0, YooAssetInitializer.Packages.Count, "钩子应同时释放上一会话的 package 登记");
        }

        /// <summary>
        /// 验证钩子刻意**不**清除项目配置的三个初始化回调。
        /// Unity 对同一 SubsystemRegistration 阶段多个钩子的调用顺序不作保证，
        /// 若框架清除回调而项目的注册钩子先于本钩子执行，会永久丢失项目配置。
        /// </summary>
        [Test]
        public void SubsystemRegistrationHookPreservesConfiguredInitializationHandlers()
        {
            YooAssetPackageInitializationHandler handler = (package, options) => null;
            YooAssetInitializer.CustomInitializationHandler = handler;
            YooAssetInitializer.HostInitializationHandler = handler;
            YooAssetInitializer.WebInitializationHandler = handler;

            try
            {
                InvokeHook();

                Assert.AreSame(handler, YooAssetInitializer.CustomInitializationHandler, "钩子不得清除 CustomInitializationHandler");
                Assert.AreSame(handler, YooAssetInitializer.HostInitializationHandler, "钩子不得清除 HostInitializationHandler");
                Assert.AreSame(handler, YooAssetInitializer.WebInitializationHandler, "钩子不得清除 WebInitializationHandler");
            }
            finally
            {
                YooAssetInitializer.CustomInitializationHandler = null;
                YooAssetInitializer.HostInitializationHandler = null;
                YooAssetInitializer.WebInitializationHandler = null;
            }
        }

        /// <summary>
        /// 验证公开的 ResetRegistration 语义未被改动：它仍然同时清除会话状态与三个初始化回调。
        /// </summary>
        [Test]
        public void PublicResetRegistrationStillClearsHandlers()
        {
            SetIsInitialized(true);
            YooAssetPackageInitializationHandler handler = (package, options) => null;
            YooAssetInitializer.CustomInitializationHandler = handler;

            YooAssetInitializer.ResetRegistration();

            Assert.IsFalse(YooAssetInitializer.IsInitialized, "公开 API 必须清除 IsInitialized");
            Assert.IsNull(YooAssetInitializer.CustomInitializationHandler, "公开 API 仍应清除初始化回调，保持既有契约");
        }

        /// <summary>获取私有宿主钩子方法。</summary>
        /// <returns>钩子方法信息。</returns>
        private static MethodInfo GetHookMethod()
        {
            MethodInfo hook = typeof(YooAssetInitializer).GetMethod(
                HOOK_METHOD_NAME,
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(hook, "YooAssetInitializer 缺少宿主重置钩子 " + HOOK_METHOD_NAME);
            return hook!;
        }

        /// <summary>直接调用私有宿主钩子，避免依赖 Play Mode 进入时机。</summary>
        private static void InvokeHook()
        {
            _ = GetHookMethod().Invoke(null, null);
        }

        /// <summary>
        /// 通过私有 setter 注入 IsInitialized，模拟上一会话在关闭 Domain Reload 时残留的静态状态。
        /// </summary>
        /// <param name="value">待注入的值。</param>
        private static void SetIsInitialized(bool value)
        {
            PropertyInfo property = typeof(YooAssetInitializer).GetProperty(
                nameof(YooAssetInitializer.IsInitialized),
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(property, "未找到 IsInitialized 属性");
            MethodInfo setter = property!.GetSetMethod(nonPublic: true);
            Assert.IsNotNull(setter, "未找到 IsInitialized 的私有 setter，测试无法注入上一会话状态");
            _ = setter!.Invoke(null, new object[] { value });
        }
    }
}
#endif
