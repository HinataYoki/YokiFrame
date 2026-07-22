using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using NUnit.Framework;
using UnityEngine;
using YokiFrame.Unity;

namespace YokiFrame.Tests
{
    /// <summary>验证 ResKit Interaction、诊断边界和 Unity 默认后端组合。</summary>
    public sealed class YokiFrameResKitInteractionTests
    {
        private SnapshotResourceProvider mProvider;

        /// <summary>为每个用例安装隔离 Provider。</summary>
        [SetUp]
        public void SetUp()
        {
            ResKit.ResetForTests();
            mProvider = new SnapshotResourceProvider();
            ResKit.SetProvider(mProvider);
        }

        /// <summary>清理静态资源和诊断开关。</summary>
        [TearDown]
        public void TearDown()
        {
            ResKit.ResetForTests();
        }

        /// <summary>验证 Provider 声明六个只读与两个显式用户操作命令。</summary>
        [Test]
        public void ProviderDeclaresStableCommandsAndRiskKinds()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();

            CollectionAssert.AreEqual(
                new[]
                {
                    "stats", "get_workbench_snapshot", "list_resources", "get_resource_detail",
                    "diagnose_resource", "get_unload_history", "clear_history", "set_tracking"
                },
                provider.Commands.Select(static command => command.Action).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    YokiFrameCommandKind.ReadOnly, YokiFrameCommandKind.ReadOnly,
                    YokiFrameCommandKind.ReadOnly, YokiFrameCommandKind.ReadOnly,
                    YokiFrameCommandKind.ReadOnly, YokiFrameCommandKind.ReadOnly,
                    YokiFrameCommandKind.UserAction, YokiFrameCommandKind.UserAction
                },
                provider.Commands.Select(static command => command.Kind).ToArray());
            CollectionAssert.AreEqual(new[] { "state" }, provider.SnapshotNames);
        }

        /// <summary>验证 state 有界、包含真实聚合计数，并明确报告资源/历史裁剪。</summary>
        [Test]
        public void StateSnapshotIsBoundedAndReportsTruncation()
        {
            List<ResHandle<SnapshotAsset>> handles = new();
            for (var index = 0; index < 60; index++)
            {
                handles.Add(ResKit.LoadAsset<SnapshotAsset>("资源/" + index + new string('长', 160)));
            }

            string json = GetProvider().CreateSnapshot("state");

            StringAssert.StartsWith("{\"schemaVersion\":1", json);
            StringAssert.Contains("\"loadedCount\":60", json);
            StringAssert.Contains("\"totalCount\":60,\"truncated\":true", json);
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
            for (var index = 0; index < handles.Count; index++) handles[index].Dispose();
        }

        /// <summary>验证有界 state 即使裁剪也始终保留 lease 数量最高的资源。</summary>
        [Test]
        public void StateSnapshotRetainsHighestLeaseResourceAfterTruncation()
        {
            List<ResHandle<SnapshotAsset>> handles = new();
            try
            {
                for (var index = 0; index < 60; index++)
                {
                    handles.Add(ResKit.LoadAsset<SnapshotAsset>("Z/Resource-" + index));
                }

                handles.Add(ResKit.LoadAsset<SnapshotAsset>("A/Highest"));
                handles.Add(ResKit.LoadAsset<SnapshotAsset>("A/Highest"));

                string json = GetProvider().CreateSnapshot("state");

                StringAssert.Contains("\"path\":\"A/Highest\"", json);
                StringAssert.Contains("\"leaseCount\":2", json);
            }
            finally
            {
                for (var index = 0; index < handles.Count; index++) handles[index].Dispose();
            }
        }

        /// <summary>验证来源默认不采集，开启后缓存命中的每个独立 lease 都会记录来源。</summary>
        [Test]
        public void SourceTrackingCoversEveryLeaseOnlyWhenEnabled()
        {
            var untracked = ResKit.LoadAsset<SnapshotAsset>("Configs/Untracked");
            List<ResDebugInfo> resources = new();
            ResKit.GetLoadedAssets(resources);
            Assert.AreEqual(0, resources[0].TrackedSourceCount);
            untracked.Dispose();

            ResKit.EnableLoadLocationTracking = true;
            var first = ResKit.LoadAsset<SnapshotAsset>("Configs/Tracked");
            var second = ResKit.LoadAsset<SnapshotAsset>("Configs/Tracked");
            ResKit.GetLoadedAssets(resources);

            Assert.AreEqual(2, resources[0].TrackedSourceCount);
            Assert.AreEqual(2, resources[0].SourceTotalCount);
            Assert.IsNotEmpty(resources[0].Source);
            first.Dispose();
            second.Dispose();
        }

        /// <summary>验证周期 state 携带一条来源预览，避免短生命周期资源只能依赖二次详情查询。</summary>
        [Test]
        public void StateSnapshotIncludesOneTrackedSourcePreview()
        {
            ResKit.EnableLoadLocationTracking = true;
            var first = ResKit.LoadAsset<SnapshotAsset>("Configs/Preview");
            var second = ResKit.LoadAsset<SnapshotAsset>("Configs/Preview");

            string json = GetProvider().CreateSnapshot("state");

            StringAssert.Contains("\"trackedSourceCount\":2", json);
            StringAssert.Contains("\"sources\":[{", json);
            StringAssert.Contains("\"sourceTotal\":2", json);
            StringAssert.Contains("\"sourcesTruncated\":true", json);
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
            first.Dispose();
            second.Dispose();
        }

        /// <summary>验证 set_tracking 拒绝缺字段和重复字段，只接受唯一布尔值。</summary>
        [Test]
        public void SetTrackingRequiresOneBooleanField()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();
            YokiFrameCommandResult missing = provider.Handle(CreateRequest("set_tracking", "{}"));
            YokiFrameCommandResult duplicate = provider.Handle(CreateRequest(
                "set_tracking",
                "{\"loadLocationTrackingEnabled\":true,\"loadLocationTrackingEnabled\":false}"));
            YokiFrameCommandResult quoted = provider.Handle(CreateRequest(
                "set_tracking", "{\"loadLocationTrackingEnabled\":\"true\"}"));
            YokiFrameCommandResult nested = provider.Handle(CreateRequest(
                "set_tracking", "{\"settings\":{\"loadLocationTrackingEnabled\":true}}"));
            YokiFrameCommandResult extra = provider.Handle(CreateRequest(
                "set_tracking", "{\"loadLocationTrackingEnabled\":true,\"extra\":false}"));
            YokiFrameCommandResult valid = provider.Handle(CreateRequest(
                "set_tracking", " { \"loadLocationTrackingEnabled\" : true } "));

            Assert.AreEqual("InvalidPayload", missing.ErrorCode);
            Assert.AreEqual("InvalidPayload", duplicate.ErrorCode);
            Assert.AreEqual("InvalidPayload", quoted.ErrorCode);
            Assert.AreEqual("InvalidPayload", nested.ErrorCode);
            Assert.AreEqual("InvalidPayload", extra.ErrorCode);
            Assert.IsTrue(valid.IsSuccess);
            Assert.IsTrue(ResKit.EnableLoadLocationTracking);
        }

        /// <summary>验证六个只读 action 与 clear_history 都执行真实 handler 并返回稳定 payload。</summary>
        [Test]
        public void DiagnosticCommandsExecuteAgainstCurrentState()
        {
            const string PATH = "Configs/Command";
            var handle = ResKit.LoadAsset<SnapshotAsset>(PATH);
            string query = "{\"path\":\"" + PATH + "\",\"typeName\":\""
                + JsonHelper.EscapeString(typeof(SnapshotAsset).FullName) + "\"}";
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();
            YokiFrameCommandResult[] loadedResults =
            {
                provider.Handle(CreateRequest("stats", "{}")),
                provider.Handle(CreateRequest("get_workbench_snapshot", "{}")),
                provider.Handle(CreateRequest("list_resources", "{\"offset\":0,\"limit\":1}")),
                provider.Handle(CreateRequest("get_resource_detail", query)),
                provider.Handle(CreateRequest("diagnose_resource", query))
            };

            for (var index = 0; index < loadedResults.Length; index++) Assert.IsTrue(loadedResults[index].IsSuccess);
            StringAssert.Contains("\"loadedCount\":1", loadedResults[0].ResultJson);
            StringAssert.Contains(PATH, loadedResults[2].ResultJson);
            handle.Dispose();

            YokiFrameCommandResult history = provider.Handle(CreateRequest("get_unload_history", "{}"));
            YokiFrameCommandResult cleared = provider.Handle(CreateRequest("clear_history", "{}"));
            Assert.IsTrue(history.IsSuccess);
            StringAssert.Contains(PATH, history.ResultJson);
            Assert.IsTrue(cleared.IsSuccess);
            Assert.AreEqual(0, ResKit.UnloadHistoryCount);
        }

        /// <summary>验证分页拒绝过期版本，并安全处理 int 最大 offset 与非法边界。</summary>
        [Test]
        public void ResourcePagingEnforcesVersionAndBounds()
        {
            var first = ResKit.LoadAsset<SnapshotAsset>("Configs/First");
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();
            long version = provider.StateVersion;
            YokiFrameCommandResult current = provider.Handle(CreateRequest(
                "list_resources", "{\"offset\":0,\"limit\":1,\"expectedVersion\":" + version + "}"));
            var second = ResKit.LoadAsset<SnapshotAsset>("Configs/Second");
            YokiFrameCommandResult stale = provider.Handle(CreateRequest(
                "list_resources", "{\"offset\":0,\"limit\":1,\"expectedVersion\":" + version + "}"));
            YokiFrameCommandResult maximumOffset = provider.Handle(CreateRequest(
                "list_resources", "{\"offset\":2147483647,\"limit\":64}"));
            YokiFrameCommandResult invalid = provider.Handle(CreateRequest(
                "list_resources", "{\"offset\":-1,\"limit\":1}"));

            Assert.IsTrue(current.IsSuccess);
            Assert.AreEqual("StateChanged", stale.ErrorCode);
            Assert.IsTrue(maximumOffset.IsSuccess);
            StringAssert.Contains("\"count\":0", maximumOffset.ResultJson);
            Assert.AreEqual("InvalidPayload", invalid.ErrorCode);
            first.Dispose();
            second.Dispose();
        }

        /// <summary>验证 Unity Adapter 只注册工厂，第一次资源调用才创建 Resources Provider。</summary>
        [Test]
        public void UnityDefaultProviderIsCreatedOnFirstResourceCall()
        {
            ResKit.ResetForTests();

            UnityResKitRuntimeInstaller.RegisterDefaultProviderFactory();

            Assert.IsNull(ResKit.GetProvider());
            Assert.IsNull(ResKit.Load<TextAsset>("YokiFrame/MissingLazyProviderProbe"));
            Assert.IsInstanceOf<UnityResourceProvider>(ResKit.GetProvider());
            Assert.IsTrue(UnityResKitRuntimeInstaller.IsInstalled);
        }

        /// <summary>验证 YooAsset 2.3+/3.x Provider 共用构造契约，并拒绝未就绪的 ResourcePackage。</summary>
        [Test]
        public void YooAssetProviderRejectsUnreadyPackage()
        {
            Type packageType = Type.GetType("YooAsset.ResourcePackage, YooAsset", false);
            Type providerType = Type.GetType(
                "YokiFrame.Unity.YooAssetResourceProvider, YokiFrame.Unity.ResKit.YooAsset", false);
            if (packageType == null || providerType == null)
            {
                Assert.Ignore("当前环境未编译受支持版本的 YooAsset ResKit Integration。");
            }

            ConstructorInfo packageConstructor = packageType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.IsNotNull(packageConstructor);
            object package = packageConstructor.Invoke(new object[] { "ResKit-Unready-Test" });

            TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
                () => Activator.CreateInstance(providerType, package));
            Assert.IsInstanceOf<InvalidOperationException>(exception.InnerException);
        }

        /// <summary>验证 capability descriptor 与 Editor/Tools Provider 命令面保持一致。</summary>
        [Test]
        public void CapabilityDescriptorMatchesProviderCatalog()
        {
            string path = Path.Combine(
                Application.dataPath,
                "YokiFrame", "Core", "Editor", "ResKit", "Capabilities", "capability.json");
            string descriptor = File.ReadAllText(path);

            foreach (YokiFrameCommandDescriptor command in GetProvider().Commands)
            {
                StringAssert.Contains("\"" + command.Action + "\"", descriptor);
            }

            StringAssert.DoesNotContain("clear_cache", descriptor);
            StringAssert.DoesNotContain("set_provider", descriptor);
        }

        /// <summary>从默认 Registry 获取唯一 ResKit Provider。</summary>
        private static IYokiFrameVersionedKitInteractionProvider GetProvider()
        {
            IYokiFrameKitInteractionProvider provider = YokiFrameCoreKitInteractions.CreateDefault()
                .Providers.First(item => string.Equals(item.Kit, "ResKit", StringComparison.Ordinal));
            var versioned = provider as IYokiFrameVersionedKitInteractionProvider;
            Assert.IsNotNull(versioned);
            return versioned;
        }

        /// <summary>创建 ResKit 命令请求。</summary>
        private static YokiFrameCommandRequest CreateRequest(string action, string payload)
        {
            return new YokiFrameCommandRequest("reskit-test", "ResKit", action, payload, 1000, payload.Length);
        }

        /// <summary>用于诊断 payload 的普通测试资源。</summary>
        private sealed class SnapshotAsset
        {
        }

        /// <summary>提供可缓存普通对象的最小测试 Provider。</summary>
        private sealed class SnapshotResourceProvider : IResourceProvider
        {
            /// <summary>获取稳定测试 Provider 名称。</summary>
            public string ProviderName => "SnapshotProvider";

            /// <summary>同步创建测试资源。</summary>
            public T Load<T>(string path) where T : class => new SnapshotAsset() as T;

#if YOKIFRAME_UNITASK_SUPPORT
            /// <summary>异步创建测试资源。</summary>
            public UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
                => UniTask.FromResult(Load<T>(path));
#else
            /// <summary>异步创建测试资源。</summary>
            public Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
                => Task.FromResult(Load<T>(path));
#endif

            /// <summary>普通测试对象无需底层释放。</summary>
            public void Release(object asset)
            {
            }
        }
    }
}
