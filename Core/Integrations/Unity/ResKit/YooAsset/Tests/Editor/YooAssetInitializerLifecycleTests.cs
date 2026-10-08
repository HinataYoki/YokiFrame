#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Unity.Tests
{
    /// <summary>
    /// 守护 YooAsset 门面在关闭 Domain Reload 后重进 Play Mode 时会重新初始化并重新安装 ResKit Provider。
    /// </summary>
    public sealed class YooAssetInitializerLifecycleTests
    {
        /// <summary>
        /// 验证关闭 Domain Reload 的宿主钩子登记在 SubsystemRegistration。
        /// 通过声明类型的 internal 测试缝读取真实特性，不反射本仓库 private。
        /// </summary>
        [Test]
        public void ResetRegistrationOnSubsystemRegistration_关闭域重载钩子_登记SubsystemRegistration()
        {
            bool found = YooAssetInitializer.TryGetSubsystemRegistrationHookLoadTypeForTests(
                out RuntimeInitializeLoadType loadType);

            Assert.IsTrue(found, "宿主重置钩子必须带有 RuntimeInitializeOnLoadMethodAttribute");
            Assert.AreEqual(
                RuntimeInitializeLoadType.SubsystemRegistration,
                loadType,
                "钩子必须先在 SubsystemRegistration 阶段登记，统一会话才能在之后清除上一会话状态");
        }

        /// <summary>验证会话重置会清除上一会话注入的已初始化标志。</summary>
        [Test]
        public void RegisterSessionResetForTests_登记后开始会话_清除已初始化标志()
        {
            YooAssetInitializer.SetInitializedForTests(true);
            Assert.IsTrue(YooAssetInitializer.IsInitialized, "前置：测试需要先注入上一会话的已初始化状态");

            try
            {
                YooAssetInitializer.RegisterSessionResetForTests();
                YokiFrameSession.Begin();

                Assert.IsFalse(
                    YooAssetInitializer.IsInitialized,
                    "钩子必须清除 IsInitialized，否则 InitializeAsync 会静默提前返回");
                Assert.IsNull(YooAssetInitializer.DefaultPackage, "钩子应同时释放上一会话的默认 package 引用");
                Assert.IsNull(YooAssetInitializer.DefaultPackageName, "钩子应同时释放上一会话的默认 package 名称");
                Assert.AreEqual(0, YooAssetInitializer.Packages.Count, "钩子应同时释放上一会话的 package 登记");
            }
            finally
            {
                YooAssetInitializer.SetInitializedForTests(false);
            }
        }

        /// <summary>验证会话重置保留项目配置的三个初始化回调。</summary>
        [Test]
        public void RegisterSessionResetForTests_已配置初始化回调_保留三个回调()
        {
            YooAssetPackageInitializationHandler handler = (package, options) => null;
            YooAssetInitializer.CustomInitializationHandler = handler;
            YooAssetInitializer.HostInitializationHandler = handler;
            YooAssetInitializer.WebInitializationHandler = handler;

            try
            {
                YooAssetInitializer.RegisterSessionResetForTests();
                YokiFrameSession.Begin();

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

        /// <summary>验证公开 ResetRegistration 会清除已初始化标志，并清除三个初始化回调。</summary>
        [Test]
        public void ResetRegistration_已初始化且已配置回调_清除标志和回调()
        {
            YooAssetPackageInitializationHandler handler = (package, options) => null;
            YooAssetInitializer.SetInitializedForTests(true);
            YooAssetInitializer.CustomInitializationHandler = handler;
            YooAssetInitializer.HostInitializationHandler = handler;
            YooAssetInitializer.WebInitializationHandler = handler;

            try
            {
                YooAssetInitializer.ResetRegistration();

                Assert.IsFalse(YooAssetInitializer.IsInitialized, "公开 API 必须清除 IsInitialized");
                Assert.IsNull(YooAssetInitializer.CustomInitializationHandler, "公开 API 必须清除 CustomInitializationHandler");
                Assert.IsNull(YooAssetInitializer.HostInitializationHandler, "公开 API 必须清除 HostInitializationHandler");
                Assert.IsNull(YooAssetInitializer.WebInitializationHandler, "公开 API 必须清除 WebInitializationHandler");
            }
            finally
            {
                YooAssetInitializer.CustomInitializationHandler = null;
                YooAssetInitializer.HostInitializationHandler = null;
                YooAssetInitializer.WebInitializationHandler = null;
                YooAssetInitializer.SetInitializedForTests(false);
            }
        }
    }
}
#endif
