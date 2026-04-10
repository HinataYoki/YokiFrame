#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 编辑器工具页。
    /// </summary>
    [YokiToolPage(
        kit: "LocalizationKit",
        name: "Localization",
        icon: KitIcons.LOCALIZATIONKIT,
        priority: 60,
        category: YokiPageCategory.Tool)]
    public class LocalizationKitToolPage : YokiToolPageBase
    {
        private ListView mTextListView;
        private Label mStatusLabel;
        private Label mCountMetricLabel;
        private Label mMissingMetricLabel;
        private DropdownField mLanguageDropdown;
        private TextField mSearchField;

        private readonly List<TextEntry> mTextEntries = new();
        private readonly List<TextEntry> mFilteredEntries = new();
        private string mSearchFilter = string.Empty;
        private LanguageId mPreviewLanguage = LanguageId.ChineseSimplified;

        /// <summary>
        /// 构建 LocalizationKit 工作台入口。
        /// </summary>
        protected override void BuildUI(VisualElement root)
        {
            var scaffold = CreateKitPageScaffold(
                "LocalizationKit",
                "查看当前 Provider 已加载的本地化文本，切换预览语言并筛选缺失翻译条目。",
                KitIcons.LOCALIZATIONKIT,
                "本地化工作台");
            root.Add(scaffold.Root);

            SetStatusContent(scaffold.StatusBar, CreateKitStatusBanner(
                "数据来源",
                "本页面基于当前 Localization Provider 读取文本数据，支持在编辑器中快速预览多语言内容。"));

            scaffold.Toolbar.Add(BuildToolbar());

            var metricStrip = CreateKitMetricStrip();
            scaffold.Content.Add(metricStrip);

            var (countCard, countValue) = CreateKitMetricCard("文本总数", "0", "当前 Provider 中可读取到的文本条目数量", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mCountMetricLabel = countValue;
            metricStrip.Add(countCard);

            var (missingCard, missingValue) = CreateKitMetricCard("缺失翻译", "0", "当前预览语言下未找到文本内容的条目数量", YokiFrameUIComponents.Colors.WorkbenchPrimary);
            mMissingMetricLabel = missingValue;
            metricStrip.Add(missingCard);

            var (listSection, listBody) = CreateKitSectionPanel(
                "文本列表",
                "按当前预览语言展示文本内容，并支持通过 ID 或文本内容搜索。",
                KitIcons.LOCALIZATIONKIT);
            listSection.style.flexGrow = 1;
            listSection.AddToClassList("yoki-kit-panel--blue");
            scaffold.Content.Add(listSection);

            mTextListView = new ListView
            {
                makeItem = MakeTextItem,
                bindItem = BindTextItem,
                itemsSource = mFilteredEntries,
                fixedItemHeight = 60
            };
            mTextListView.style.flexGrow = 1;
            listBody.Add(mTextListView);

            RefreshTextList();
        }

        private VisualElement BuildToolbar()
        {
            var toolbar = CreateToolbar();
            toolbar.AddToClassList("yoki-localization-selector");

            mLanguageDropdown = new DropdownField("预览语言");
            mLanguageDropdown.AddToClassList("yoki-localization-selector__dropdown");
            mLanguageDropdown.choices = new List<string>
            {
                "简体中文", "繁体中文", "English", "日本語", "한국어"
            };
            mLanguageDropdown.index = 0;
            mLanguageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
            toolbar.Add(mLanguageDropdown);

            mSearchField = new TextField("搜索");
            mSearchField.RegisterValueChangedCallback(OnSearchChanged);
            toolbar.Add(mSearchField);

            toolbar.Add(CreateToolbarButton("刷新", RefreshTextList));
            toolbar.Add(CreateToolbarSpacer());

            mStatusLabel = new Label("未加载数据");
            mStatusLabel.AddToClassList("toolbar-label");
            toolbar.Add(mStatusLabel);

            return toolbar;
        }

        private VisualElement MakeTextItem()
        {
            var container = new VisualElement();
            container.AddToClassList("yoki-localization-entry");

            var idLabel = new Label();
            idLabel.name = "id-label";
            idLabel.AddToClassList("yoki-localization-entry__key");
            container.Add(idLabel);

            var textLabel = new Label();
            textLabel.name = "text-label";
            textLabel.AddToClassList("yoki-localization-entry__value");
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            textLabel.style.overflow = Overflow.Hidden;
            container.Add(textLabel);

            var statusLabel = new Label();
            statusLabel.name = "status-label";
            statusLabel.style.fontSize = 10;
            statusLabel.style.color = YokiFrameUIComponents.Colors.TextSecondary;
            container.Add(statusLabel);

            return container;
        }

        private void BindTextItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mFilteredEntries.Count)
            {
                return;
            }

            var entry = mFilteredEntries[index];

            var idLabel = element.Q<Label>("id-label");
            idLabel.text = $"ID: {entry.TextId}";

            var textLabel = element.Q<Label>("text-label");
            textLabel.text = entry.Text ?? "[未找到]";

            var statusLabel = element.Q<Label>("status-label");
            var statusIcon = element.Q<Image>("status-icon");

            if (statusIcon == null)
            {
                statusIcon = new Image
                {
                    name = "status-icon"
                };
                statusIcon.style.width = 12;
                statusIcon.style.height = 12;
                statusIcon.style.marginRight = 4;
                statusIcon.style.display = DisplayStyle.None;
                var parent = statusLabel.parent;
                int idx = parent.IndexOf(statusLabel);
                parent.Insert(idx, statusIcon);
            }

            if (entry.IsMissing)
            {
                element.EnableInClassList("yoki-localization-entry--missing", true);
                statusIcon.image = KitIcons.GetTexture(KitIcons.WARNING);
                statusIcon.tintColor = YokiFrameUIComponents.Colors.BrandWarning;
                statusIcon.style.display = DisplayStyle.Flex;
                statusLabel.text = "缺失翻译";
                statusLabel.style.color = YokiFrameUIComponents.Colors.BrandWarning;
                return;
            }

            element.EnableInClassList("yoki-localization-entry--missing", false);
            statusIcon.style.display = DisplayStyle.None;
            statusLabel.text = $"语言: {entry.LanguageId}";
            statusLabel.style.color = YokiFrameUIComponents.Colors.TextSecondary;
        }

        private void OnLanguageChanged(ChangeEvent<string> evt)
        {
            mPreviewLanguage = mLanguageDropdown.index switch
            {
                0 => LanguageId.ChineseSimplified,
                1 => LanguageId.ChineseTraditional,
                2 => LanguageId.English,
                3 => LanguageId.Japanese,
                4 => LanguageId.Korean,
                _ => LanguageId.ChineseSimplified
            };
            RefreshTextList();
        }

        private void OnSearchChanged(ChangeEvent<string> evt)
        {
            mSearchFilter = evt.newValue?.ToLowerInvariant() ?? string.Empty;
            ApplyFilter();
        }

        private void RefreshTextList()
        {
            mTextEntries.Clear();

            var provider = LocalizationKit.GetProvider();
            if (provider == null)
            {
                mStatusLabel.text = "Provider 未设置";
                mCountMetricLabel.text = "0";
                mMissingMetricLabel.text = "0";
                ApplyFilter();
                return;
            }

            if (provider is JsonLocalizationProvider jsonProvider)
            {
                var textIds = jsonProvider.GetAllTextIds();
                foreach (var textId in textIds)
                {
                    var entry = new TextEntry
                    {
                        TextId = textId,
                        LanguageId = mPreviewLanguage
                    };

                    if (provider.TryGetText(mPreviewLanguage, textId, out var text))
                    {
                        entry.Text = text;
                        entry.IsMissing = false;
                    }
                    else
                    {
                        entry.Text = null;
                        entry.IsMissing = true;
                    }

                    mTextEntries.Add(entry);
                }
            }

            int missingCount = 0;
            foreach (var entry in mTextEntries)
            {
                if (entry.IsMissing)
                {
                    missingCount++;
                }
            }

            mStatusLabel.text = $"共 {mTextEntries.Count} 条文本，{missingCount} 条缺失";
            mCountMetricLabel.text = mTextEntries.Count.ToString();
            mMissingMetricLabel.text = missingCount.ToString();
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            mFilteredEntries.Clear();

            if (string.IsNullOrEmpty(mSearchFilter))
            {
                mFilteredEntries.AddRange(mTextEntries);
            }
            else
            {
                foreach (var entry in mTextEntries)
                {
                    if (entry.TextId.ToString().ToLowerInvariant().Contains(mSearchFilter) ||
                        (entry.Text != null && entry.Text.ToLowerInvariant().Contains(mSearchFilter)))
                    {
                        mFilteredEntries.Add(entry);
                    }
                }
            }

            mTextListView?.RefreshItems();
        }

        public override void OnActivate()
        {
            RefreshTextList();
        }

        [System.Obsolete("保留用于运行时刷新。")]
        public override void OnUpdate()
        {
            if (IsPlaying)
            {
            }
        }

        private struct TextEntry
        {
            public int TextId;
            public LanguageId LanguageId;
            public string Text;
            public bool IsMissing;
        }
    }
}
#endif
