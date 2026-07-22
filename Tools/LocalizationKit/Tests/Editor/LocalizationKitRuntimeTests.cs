using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证 LocalizationKit JSON/Table Runtime 契约和旧版缺陷修复。</summary>
    public sealed class LocalizationKitRuntimeTests
    {
        [SetUp]
        public void SetUp()
        {
            LocalizationKit.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            LocalizationKit.Reset();
        }

        [Test]
        public void JsonProvider_LoadsKeyedTextPluralAndLanguageInfo()
        {
            var provider = new JsonLocalizationProvider();
            bool loaded = provider.TryLoadFromJson(
                "{\"formatVersion\":1,\"languages\":[" +
                "{\"id\":\"English\",\"displayNameTextId\":10,\"nativeNameTextId\":11,\"iconSpriteId\":12}," +
                "{\"id\":\"ChineseSimplified\",\"displayNameTextId\":20}]," +
                "\"texts\":[" +
                "{\"id\":100,\"values\":{\"ChineseSimplified\":\"开始\",\"English\":\"Start\"}}," +
                "{\"id\":200,\"plural\":{\"English\":{\"One\":\"{0} apple\",\"Other\":\"{0} apples\"}}}]}",
                out string error);

            Assert.IsTrue(loaded, error);
            LocalizationKit.SetProvider(provider);
            LocalizationKit.SetDefaultLanguage(LanguageId.English);
            Assert.IsTrue(LocalizationKit.SetLanguage(LanguageId.ChineseSimplified));
            Assert.AreEqual("开始", LocalizationKit.Get(100));
            Assert.AreEqual("Start", LocalizationKit.Get(LanguageId.English, 100));
            Assert.AreEqual("1 apple", LocalizationKit.GetPlural(200, 1));
            Assert.AreEqual("2 apples", LocalizationKit.GetPlural(200, 2));
            Assert.AreEqual(10, provider.GetLanguageInfo(LanguageId.English).DisplayNameTextId);
        }

        [Test]
        public void JsonProvider_ReloadReplacesPreviousSnapshotAndRejectsInvalidInput()
        {
            var provider = new JsonLocalizationProvider();
            Assert.IsTrue(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":1,\"values\":{\"English\":\"old\"}}]}",
                out string error), error);
            Assert.IsTrue(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":2,\"values\":{\"English\":\"new\"}}]}",
                out error), error);

            LocalizationKit.SetProvider(provider);
            LocalizationKit.SetLanguage(LanguageId.English);
            Assert.AreEqual("[Missing:1]", LocalizationKit.Get(1));
            Assert.AreEqual("new", LocalizationKit.Get(2));

            Assert.IsFalse(provider.TryLoadFromJson("{\"languages\":42}", out error));
            Assert.IsNotEmpty(error);
            Assert.IsTrue(provider.TryGetText(LanguageId.English, 2, out string text));
            Assert.AreEqual("new", text);
        }

        [Test]
        public void JsonProvider_RejectsFractionalTextId()
        {
            var provider = new JsonLocalizationProvider();

            bool loaded = provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":1.5,\"values\":{\"English\":\"invalid\"}}]}",
                out string error);

            Assert.IsFalse(loaded);
            Assert.IsNotEmpty(error);
            Assert.IsEmpty(provider.GetSupportedLanguages());
        }

        /// <summary>重复文本 ID 必须被拒绝，且失败解析不能破坏之前已加载的完整快照。</summary>
        [Test]
        public void JsonProvider_RejectsDuplicateTextIdsWithoutReplacingSnapshot()
        {
            var provider = new JsonLocalizationProvider();
            Assert.IsTrue(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":1,\"values\":{\"English\":\"old\"}}]}",
                out string error), error);

            Assert.IsFalse(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":1,\"values\":{\"English\":\"first\"}},{\"id\":1,\"values\":{\"English\":\"second\"}}]}",
                out error));
            Assert.IsNotEmpty(error);
            Assert.IsTrue(provider.TryGetText(LanguageId.English, 1, out string text));
            Assert.AreEqual("old", text);
        }

        /// <summary>JSON 对象中的重复键必须在解析阶段失败，避免同一语言译文被后写值静默覆盖。</summary>
        [Test]
        public void JsonProvider_RejectsDuplicateObjectKeys()
        {
            var provider = new JsonLocalizationProvider();

            bool loaded = provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":1,\"values\":{\"English\":\"first\",\"English\":\"second\"}}]}",
                out string error);

            Assert.IsFalse(loaded);
            Assert.IsNotEmpty(error);
            Assert.IsEmpty(provider.GetSupportedLanguages());
        }

        [Test]
        public void RuntimeFacade_DoesNotExposeEditorOnlyDiagnosticState()
        {
            Assert.IsNull(typeof(LocalizationKit).GetProperty("DiagnosticVersion"));
        }

        [Test]
        public void TableProvider_FallsBackToOtherPluralCategoryAndExposesReadOnlyLanguages()
        {
            var provider = new TableLocalizationProvider(
                new[] { LanguageId.English },
                (language, id) => id == 1 ? "Hello" : null,
                (language, id, category) => category == PluralCategory.Other ? "{0} items" : null);

            LocalizationKit.SetProvider(provider);
            LocalizationKit.SetLanguage(LanguageId.English);
            Assert.AreEqual("Hello", LocalizationKit.Get(1));
            Assert.AreEqual("2 items", LocalizationKit.GetPlural(2, 2));
            Assert.IsFalse(provider.GetSupportedLanguages() is List<LanguageId>);
        }

        [Test]
        public void ProviderReplacement_RefreshesRegisteredBinder()
        {
            var first = new TableLocalizationProvider(new[] { LanguageId.English }, (language, id) => "first");
            var second = new TableLocalizationProvider(new[] { LanguageId.English }, (language, id) => "second");
            var binder = new CountingBinder();

            LocalizationKit.SetProvider(first);
            LocalizationKit.RegisterBinder(binder);
            LocalizationKit.SetProvider(second);

            Assert.AreEqual(1, binder.RefreshCount);
        }

        /// <summary>fallback 语言会影响未翻译条目的显示，因此切换后必须刷新已注册 Binder。</summary>
        [Test]
        public void DefaultLanguageChange_RefreshesRegisteredBinder()
        {
            var binder = new CountingBinder();
            LocalizationKit.RegisterBinder(binder);

            LocalizationKit.SetDefaultLanguage(LanguageId.English);
            LocalizationKit.SetDefaultLanguage(LanguageId.English);

            Assert.AreEqual(1, binder.RefreshCount);
        }

        [Test]
        public void Formatter_FormatsIndexedNamedAndTags()
        {
            var formatter = new DefaultTextFormatter();
            formatter.RegisterTagHandler("b", value => value.ToUpperInvariant());

            Assert.AreEqual("HP 3/5", formatter.Format("HP {0}/{1}", new object[] { 3, 5 }));
            Assert.AreEqual("Alice has 3.5", formatter.Format(
                "{name} has {count:F1}",
                new Dictionary<string, object> { { "name", "Alice" }, { "count", 3.5f } }));
            Assert.AreEqual("AOKC", formatter.ProcessTags("A<b:ok>C"));
        }

        [Test]
        public void SaveData_CapturesAndAppliesCurrentLanguage()
        {
            LocalizationKit.SetProvider(new TableLocalizationProvider(new[] { LanguageId.Japanese }, (language, id) => "text"));
            Assert.IsTrue(LocalizationKit.SetLanguage(LanguageId.Japanese));
            LocalizationSaveData data = LocalizationSaveData.FromCurrentSettings();

            LocalizationKit.SetLanguage(LanguageId.ChineseSimplified);
            Assert.IsTrue(data.Apply());
            Assert.AreEqual(LanguageId.Japanese, LocalizationKit.GetCurrentLanguage());
        }

        private sealed class CountingBinder : ILocalizationBinder
        {
            /// <inheritdoc />
            public int TextId => 1;
            /// <inheritdoc />
            public bool IsValid => true;
            /// <summary>记录刷新次数。</summary>
            public int RefreshCount { get; private set; }
            /// <inheritdoc />
            public void Refresh() => RefreshCount++;
        }
    }
}
