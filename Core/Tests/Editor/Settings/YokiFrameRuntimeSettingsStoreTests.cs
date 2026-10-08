using System;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证跨宿主 Runtime Settings Store 的稀疏覆盖、类型读取与标识安全边界。
    /// </summary>
    public sealed class YokiFrameRuntimeSettingsStoreTests
    {
        /// <summary>
        /// 每个测试前恢复内存 Store，避免全局设置污染其它测试。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            KitSettings.Reset();
        }

        /// <summary>
        /// 验证宿主注入的 Store 可以通过统一 KitSettings API 读取不同标量类型。
        /// </summary>
        [Test]
        public void SetStore_注入标量_按类型读取()
        {
            YokiFrameRuntimeSettingsStore store = new();
            store.SetValue("LogKit", "enabled", "false");
            store.SetValue("LogKit", "maxQueueSize", "4096");
            KitSettings.SetStore(store);

            Assert.IsFalse(KitSettings.GetBool("LogKit", "enabled", true));
            Assert.AreEqual(4096, KitSettings.GetInt("LogKit", "maxQueueSize", 1));
        }

        /// <summary>
        /// 验证默认宿主工厂在首次访问前不创建 Store，首次访问后只创建一次并复用实例。
        /// </summary>
        [Test]
        public void RegisterDefaultStoreFactory_首次访问_只创建一次并复用()
        {
            var factoryCalls = 0;
            KitSettings.RegisterDefaultStoreFactory(() =>
            {
                factoryCalls++;
                YokiFrameRuntimeSettingsStore store = new();
                store.SetValue("LogKit", "enabled", "false");
                return store;
            });

            Assert.AreEqual(0, factoryCalls);
            Assert.IsFalse(KitSettings.GetBool("LogKit", "enabled", true));
            Assert.AreEqual(1, factoryCalls);
            Assert.IsFalse(KitSettings.GetBool("LogKit", "enabled", true));
            Assert.AreEqual(1, factoryCalls);
        }

        /// <summary>
        /// 验证首次访问发生在工厂注册前时先使用内存回退，工厂注册后下一次访问改用工厂 Store。
        /// </summary>
        [Test]
        public void RegisterDefaultStoreFactory_先访问后注册_替换内存回退()
        {
            Assert.AreEqual("fallback", KitSettings.GetString("LogKit", "source", "fallback"));

            KitSettings.RegisterDefaultStoreFactory(() =>
            {
                YokiFrameRuntimeSettingsStore store = new();
                store.SetValue("LogKit", "source", "factory");
                return store;
            });

            Assert.AreEqual("factory", KitSettings.GetString("LogKit", "source", "fallback"));
        }

        /// <summary>
        /// 验证清除显式 Store 后，若默认工厂已注册，下一次访问会重新解析工厂而不是长期钉在内存回退。
        /// </summary>
        [Test]
        public void SetStore_清除显式Store_重新使用已注册工厂()
        {
            KitSettings.RegisterDefaultStoreFactory(() =>
            {
                YokiFrameRuntimeSettingsStore store = new();
                store.SetValue("LogKit", "source", "factory");
                return store;
            });
            YokiFrameRuntimeSettingsStore explicitStore = new();
            explicitStore.SetValue("LogKit", "source", "explicit");
            KitSettings.SetStore(explicitStore);

            Assert.AreEqual("explicit", KitSettings.GetString("LogKit", "source", "fallback"));

            KitSettings.SetStore(null);

            Assert.AreEqual("factory", KitSettings.GetString("LogKit", "source", "fallback"));
        }

        /// <summary>
        /// 验证显式注入 Store 优先于已注册的宿主默认工厂。
        /// </summary>
        [Test]
        public void SetStore_显式Store_优先于默认工厂()
        {
            var factoryCalls = 0;
            KitSettings.RegisterDefaultStoreFactory(() =>
            {
                factoryCalls++;
                return new YokiFrameRuntimeSettingsStore();
            });
            YokiFrameRuntimeSettingsStore explicitStore = new();
            explicitStore.SetValue("LogKit", "enabled", "false");
            KitSettings.SetStore(explicitStore);

            Assert.IsFalse(KitSettings.GetBool("LogKit", "enabled", true));
            Assert.AreEqual(0, factoryCalls);
        }

        /// <summary>
        /// 验证同一设置重复写入时以最后值为准，移除后读取回退到默认值。
        /// </summary>
        [Test]
        public void SetValue_重复写入后移除_最后值生效且移除后回退()
        {
            YokiFrameRuntimeSettingsStore store = new();
            store.SetValue("LogKit", "minimumLevel", "Info");
            store.SetValue("LogKit", "minimumLevel", "Error");
            KitSettings.SetStore(store);

            Assert.AreEqual("Error", KitSettings.GetString("LogKit", "minimumLevel", "Debug"));
            store.RemoveValue("LogKit", "minimumLevel");
            Assert.AreEqual("Debug", KitSettings.GetString("LogKit", "minimumLevel", "Debug"));
        }

        /// <summary>
        /// 验证非法 Kit 或 key 在进入 Store 时立即拒绝，避免宿主配置注入路径式标识。
        /// </summary>
        [Test]
        public void SetValue_非法标识_立即拒绝()
        {
            YokiFrameRuntimeSettingsStore store = new();

            Assert.Throws<ArgumentException>(() => store.SetValue("../LogKit", "enabled", "true"));
            Assert.Throws<ArgumentException>(() => store.SetValue("LogKit", "file/enabled", "true"));
        }
    }
}