using System;
using System.Collections.Generic;
using System.Globalization;
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
            formatter.Culture = CultureInfo.InvariantCulture;
            formatter.RegisterTagHandler("b", value => value.ToUpperInvariant());

            Assert.AreEqual("HP 3/5", formatter.Format("HP {0}/{1}", new object[] { 3, 5 }));
            Assert.AreEqual("Alice has 3.5", formatter.Format(
                "{name} has {count:F1}",
                new Dictionary<string, object> { { "name", "Alice" }, { "count", 3.5f } }));
            Assert.AreEqual("AOKC", formatter.ProcessTags("A<b:ok>C"));
        }

        /// <summary>参数格式化必须跟随 Formatter 配置文化，而不是宿主进程区域。</summary>
        [Test]
        public void Formatter_UsesConfiguredCultureForBothPlaceholderBranches()
        {
            var formatter = new DefaultTextFormatter();

            Assert.AreEqual("3.5", formatter.Format("{0}", new object[] { 3.5 }));
            Assert.AreEqual("3.50", formatter.Format("{0:F2}", new object[] { 3.5 }));

            var commaCulture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            commaCulture.NumberFormat.NumberDecimalSeparator = ",";
            formatter.Culture = commaCulture;
            Assert.AreEqual("3,5", formatter.Format("{0}", new object[] { 3.5 }));
            Assert.AreEqual("3,50", formatter.Format("{0:F2}", new object[] { 3.5 }));

            formatter.Culture = null;
            Assert.AreSame(CultureInfo.InvariantCulture, formatter.Culture);
            Assert.AreEqual("3.5", formatter.Format(
                "{count}", new Dictionary<string, object> { { "count", 3.5 } }));
        }

        [Test]
        public void SaveData_CapturesAndAppliesCurrentLanguage()
        {
            LocalizationKit.SetProvider(new TableLocalizationProvider(
                new[] { LanguageId.Japanese, LanguageId.ChineseSimplified }, (language, id) => "text"));
            Assert.IsTrue(LocalizationKit.SetLanguage(LanguageId.Japanese));
            LocalizationSaveData data = LocalizationSaveData.FromCurrentSettings();

            Assert.IsTrue(LocalizationKit.SetLanguage(LanguageId.ChineseSimplified));
            Assert.AreEqual(LanguageId.ChineseSimplified, LocalizationKit.GetCurrentLanguage());

            Assert.IsTrue(data.Apply());
            Assert.AreEqual(LanguageId.Japanese, LocalizationKit.GetCurrentLanguage());
        }

        /// <summary>保存语言不在 Provider 支持列表时，Apply 必须失败且不改变当前语言。</summary>
        [Test]
        public void SaveData_ApplyFailsForUnsupportedLanguage()
        {
            LocalizationKit.SetProvider(new TableLocalizationProvider(
                new[] { LanguageId.ChineseSimplified }, (language, id) => "text"));
            var data = new LocalizationSaveData(LanguageId.Japanese, LocalizationSaveData.CurrentVersion);

            Assert.IsFalse(data.Apply());
            Assert.AreEqual(LanguageId.ChineseSimplified, LocalizationKit.GetCurrentLanguage());
        }

        /// <summary>枚举名称列表会被 Enum.TryParse 按位或成另一个合法值，必须在 schema 层拒绝。</summary>
        [Test]
        public void JsonProvider_RejectsCommaSeparatedEnumNames()
        {
            var provider = new JsonLocalizationProvider();

            Assert.IsFalse(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English,French\"}],\"texts\":[]}", out string error));
            Assert.IsNotEmpty(error);

            Assert.IsFalse(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}]," +
                "\"texts\":[{\"id\":1,\"plural\":{\"English\":{\"One,Two\":\"x\"}}}]}",
                out error));
            Assert.IsNotEmpty(error);
            Assert.IsEmpty(provider.GetSupportedLanguages());
        }

        /// <summary>深层嵌套输入必须被深度上限拒绝，而不是递归耗尽栈终止进程。</summary>
        [Test]
        public void JsonProvider_RejectsDeeplyNestedInput()
        {
            var provider = new JsonLocalizationProvider();

            bool loaded = provider.TryLoadFromJson(new string('[', 200), out string error);

            Assert.IsFalse(loaded);
            Assert.IsNotEmpty(error);
        }

        /// <summary>formatVersion 存在但非整数时必须报错，而不是静默退化成当前版本。</summary>
        [Test]
        public void JsonProvider_RejectsInvalidFormatVersion()
        {
            var provider = new JsonLocalizationProvider();

            Assert.IsFalse(provider.TryLoadFromJson(
                "{\"formatVersion\":\"abc\",\"languages\":[{\"id\":\"English\"}],\"texts\":[]}", out string error));
            Assert.IsNotEmpty(error);

            Assert.IsFalse(provider.TryLoadFromJson(
                "{\"formatVersion\":2.5,\"languages\":[{\"id\":\"English\"}],\"texts\":[]}", out error));
            Assert.IsNotEmpty(error);

            Assert.IsTrue(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[]}", out error), error);
        }

        /// <summary>字符串解析必须覆盖转义、Unicode、控制字符与未闭合四类语义。</summary>
        [Test]
        public void JsonParser_HandlesStringEscapesAndRejectsMalformedStrings()
        {
            var provider = new JsonLocalizationProvider();
            string longText = new string('x', 512);

            Assert.IsTrue(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[" +
                "{\"id\":1,\"values\":{\"English\":\"q\\\"e\"}}," +
                "{\"id\":2,\"values\":{\"English\":\"b\\\\s\"}}," +
                "{\"id\":3,\"values\":{\"English\":\"s\\/l\"}}," +
                "{\"id\":4,\"values\":{\"English\":\"\\b\\f\\n\\r\\t\"}}," +
                "{\"id\":5,\"values\":{\"English\":\"u\\u0041\\u4e2d\"}}," +
                "{\"id\":6,\"values\":{\"English\":\"" + longText + "\"}}]}",
                out string error), error);

            Assert.IsTrue(provider.TryGetText(LanguageId.English, 1, out string text));
            Assert.AreEqual("q\"e", text);
            Assert.IsTrue(provider.TryGetText(LanguageId.English, 2, out text));
            Assert.AreEqual("b\\s", text);
            Assert.IsTrue(provider.TryGetText(LanguageId.English, 3, out text));
            Assert.AreEqual("s/l", text);
            Assert.IsTrue(provider.TryGetText(LanguageId.English, 4, out text));
            Assert.AreEqual("\b\f\n\r\t", text);
            Assert.IsTrue(provider.TryGetText(LanguageId.English, 5, out text));
            Assert.AreEqual("uA中", text);
            Assert.IsTrue(provider.TryGetText(LanguageId.English, 6, out text));
            Assert.AreEqual(longText, text);

            Assert.IsFalse(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":1,\"values\":{\"English\":\"open}]}",
                out error));
            Assert.IsNotEmpty(error);

            Assert.IsFalse(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":1,\"values\":{\"English\":\"raw\u0001\"}}]}",
                out error));
            Assert.IsNotEmpty(error);

            Assert.IsFalse(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":1,\"values\":{\"English\":\"bad\\q\"}}]}",
                out error));
            Assert.IsNotEmpty(error);
        }

        /// <summary>只配普通文本的条目走复数查询时，必须回退普通文本而非返回缺失标记。</summary>
        [Test]
        public void JsonProvider_PluralQueryFallsBackToPlainText()
        {
            var provider = new JsonLocalizationProvider();
            Assert.IsTrue(provider.TryLoadFromJson(
                "{\"languages\":[{\"id\":\"English\"}],\"texts\":[{\"id\":1,\"values\":{\"English\":\"{0} item(s)\"}}]}",
                out string error), error);

            LocalizationKit.SetProvider(provider);
            Assert.IsTrue(LocalizationKit.SetLanguage(LanguageId.English));
            Assert.AreEqual("3 item(s)", LocalizationKit.GetPlural(1, 3));
        }

        /// <summary>手动写入的语言即使没有元数据，GetLanguageInfo 也必须返回正确的语言标识。</summary>
        [Test]
        public void JsonProvider_GetLanguageInfoKeepsIdForSupportedLanguage()
        {
            var provider = new JsonLocalizationProvider();
            provider.AddText(LanguageId.English, 1, "x");

            LanguageInfo info = provider.GetLanguageInfo(LanguageId.English);
            Assert.AreEqual(LanguageId.English, info.Id);
            Assert.IsFalse(info.IsValid);
            Assert.AreEqual(LanguageId.ChineseSimplified, provider.GetLanguageInfo(LanguageId.Korean).Id);
            Assert.IsFalse(provider.GetLanguageInfo(LanguageId.Korean).IsValid);
        }

        /// <summary>验证 LanguageInfo 满足值相等语义，重载运算符与 Equals 行为一致。</summary>
        [Test]
        public void LanguageInfo_EqualityAndOperatorsUseValueSemantics()
        {
            var info1 = new LanguageInfo(LanguageId.English, 10, 20, 30);
            var info2 = new LanguageInfo(LanguageId.English, 10, 20, 30);
            var info3 = new LanguageInfo(LanguageId.English, 10, 20, 31);

            Assert.IsTrue(info1 == info2);
            Assert.IsFalse(info1 != info2);
            Assert.IsTrue(info1.Equals(info2));
            Assert.AreEqual(info1.GetHashCode(), info2.GetHashCode());

            Assert.IsFalse(info1 == info3);
            Assert.IsTrue(info1 != info3);
            Assert.IsFalse(info1.Equals(info3));

            var empty1 = LanguageInfo.Empty;
            var empty2 = default(LanguageInfo);
            Assert.IsTrue(empty1 == empty2);
            Assert.AreEqual(empty1.GetHashCode(), empty2.GetHashCode());
        }

        /// <summary>失效 Binder 必须在刷新时被移除，避免静态注册表无界增长。</summary>
        [Test]
        public void NotifyBinders_RemovesInvalidBinders()
        {
            var binder = new ToggleBinder();
            LocalizationKit.RegisterBinder(binder);
            Assert.AreEqual(1, LocalizationKit.GetBinderCount());

            Assert.IsTrue(LocalizationKit.SetLanguage(LanguageId.English));
            Assert.AreEqual(1, binder.RefreshCount);
            Assert.AreEqual(1, LocalizationKit.GetBinderCount());

            binder.Invalidate();
            Assert.IsTrue(LocalizationKit.SetLanguage(LanguageId.Japanese));
            Assert.AreEqual(1, binder.RefreshCount);
            Assert.AreEqual(0, LocalizationKit.GetBinderCount());
        }

        /// <summary>ResetRules 必须清除自定义复数规则并恢复内置规则。</summary>
        [Test]
        public void PluralRuleFactory_ResetRulesRestoresBuiltinRules()
        {
            try
            {
                PluralRuleFactory.RegisterRule(new AlwaysManyPluralRule());
                Assert.AreEqual(PluralCategory.Many, PluralRuleFactory.GetCategory(LanguageId.English, 1));

                PluralRuleFactory.ResetRules();
                Assert.AreEqual(PluralCategory.One, PluralRuleFactory.GetCategory(LanguageId.English, 1));
                Assert.AreEqual(PluralCategory.Other, PluralRuleFactory.GetCategory(LanguageId.English, 2));
            }
            finally
            {
                PluralRuleFactory.ResetRules();
            }
        }

        private sealed class ToggleBinder : ILocalizationBinder
        {
            private bool mIsValid = true;

            /// <inheritdoc />
            public int TextId => 1;
            /// <inheritdoc />
            public bool IsValid => mIsValid;
            /// <summary>记录刷新次数。</summary>
            public int RefreshCount { get; private set; }
            /// <summary>把 Binder 标记为已销毁。</summary>
            public void Invalidate() => mIsValid = false;
            /// <inheritdoc />
            public void Refresh() => RefreshCount++;
        }

        private sealed class AlwaysManyPluralRule : IPluralRule
        {
            /// <inheritdoc />
            public LanguageId LanguageId => LanguageId.English;
            /// <inheritdoc />
            public PluralCategory GetCategory(int count) => PluralCategory.Many;
            /// <inheritdoc />
            public PluralCategory GetCategory(double count) => PluralCategory.Many;
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
