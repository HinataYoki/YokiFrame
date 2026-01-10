#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LocalizationKit 编辑器工具页面
    /// </summary>
    public class LocalizationKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "Localization";
        public override string PageIcon => KitIcons.LOCALIZATIONKIT;
        public override int Priority => 60;

        private ListView mTextListView;
        private Label mStatusLabel;
        private DropdownField mLanguageDropdown;
        private TextField mSearchField;

        private readonly List<TextEntry> mTextEntries = new();
        private readonly List<TextEntry> mFilteredEntries = new();
        private string mSearchFilter = string.Empty;
        private LanguageId mPreviewLanguage = LanguageId.ChineseSimplified;

        protected override void BuildUI(VisualElement root)
        {
            // 工具栏
            var toolbar = CreateToolbar();
            root.Add(toolbar);

            // 语言选择
            mLanguageDropdown = new DropdownField("预览语言");
            mLanguageDropdown.choices = new List<string>
            {
                "简体中文", "繁体中文", "English", "日本語", "한국어"
            };
            mLanguageDropdown.index = 0;
            mLanguageDropdown.RegisterValueChangedCallback(OnLanguageChanged);
            toolbar.Add(mLanguageDropdown);

            // 搜索框
            mSearchField = new TextField("搜索");
            mSearchField.RegisterValueChangedCallback(OnSearchChanged);
            toolbar.Add(mSearchField);

            // 刷新按钮
            var refreshButton = CreateToolbarButton("刷新", RefreshTextList);
            toolbar.Add(refreshButton);

            // 状态栏
            mStatusLabel = new Label("未加载数据");
            mStatusLabel.style.marginLeft = 10;
            mStatusLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
            toolbar.Add(mStatusLabel);

            // 文本列表
            var listContainer = new VisualElement();
            listContainer.style.flexGrow = 1;
            root.Add(listContainer);

            mTextListView = new ListView();
            mTextListView.makeItem = MakeTextItem;
            mTextListView.bindItem = BindTextItem;
            mTextListView.itemsSource = mFilteredEntries;
            mTextListView.fixedItemHeight = 60;
            mTextListView.style.flexGrow = 1;
            listContainer.Add(mTextListView);

            // 帮助信息
            var helpBox = CreateHelpBox(
                "LocalizationKit 编辑器工具\n" +
                "• 在运行时查看已加载的本地化文本\n" +
                "• 切换预览语言查看不同翻译\n" +
                "• 搜索文本ID或内容");
            root.Add(helpBox);
        }

        private VisualElement MakeTextItem()
        {
            var container = new VisualElement();
            container.style.paddingLeft = 10;
            container.style.paddingRight = 10;
            container.style.paddingTop = 5;
            container.style.paddingBottom = 5;
            container.style.borderBottomWidth = 1;
            container.style.borderBottomColor = new Color(0.3f, 0.3f, 0.3f);

            var idLabel = new Label();
            idLabel.name = "id-label";
            idLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            container.Add(idLabel);

            var textLabel = new Label();
            textLabel.name = "text-label";
            textLabel.style.whiteSpace = WhiteSpace.Normal;
            textLabel.style.overflow = Overflow.Hidden;
            container.Add(textLabel);

            var statusLabel = new Label();
            statusLabel.name = "status-label";
            statusLabel.style.fontSize = 10;
            statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            container.Add(statusLabel);

            return container;
        }

        private void BindTextItem(VisualElement element, int index)
        {
            if (index < 0 || index >= mFilteredEntries.Count) return;

            var entry = mFilteredEntries[index];

            var idLabel = element.Q<Label>("id-label");
            idLabel.text = $"ID: {entry.TextId}";

            var textLabel = element.Q<Label>("text-label");
            textLabel.text = entry.Text ?? "[未找到]";

            var statusLabel = element.Q<Label>("status-label");
            var statusIcon = element.Q<Image>("status-icon");
            
            // 确保状态图标存在
            if (statusIcon == null)
            {
                statusIcon = new Image();
                statusIcon.name = "status-icon";
                statusIcon.style.width = 12;
                statusIcon.style.height = 12;
                statusIcon.style.marginRight = 4;
                statusIcon.style.display = DisplayStyle.None;
                // 插入到 statusLabel 之前
                var parent = statusLabel.parent;
                var idx = parent.IndexOf(statusLabel);
                parent.Insert(idx, statusIcon);
            }
            
            if (entry.IsMissing)
            {
                statusIcon.image = KitIcons.GetTexture(KitIcons.WARNING);
                statusIcon.tintColor = new Color(1f, 0.5f, 0f);
                statusIcon.style.display = DisplayStyle.Flex;
                statusLabel.text = "缺失翻译";
                statusLabel.style.color = new Color(1f, 0.5f, 0f);
            }
            else
            {
                statusIcon.style.display = DisplayStyle.None;
                statusLabel.text = $"语言: {entry.LanguageId}";
                statusLabel.style.color = new Color(0.6f, 0.6f, 0.6f);
            }
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
            mSearchFilter = evt.newValue?.ToLower() ?? string.Empty;
            ApplyFilter();
        }

        private void RefreshTextList()
        {
            mTextEntries.Clear();

            var provider = LocalizationKit.GetProvider();
            if (provider == null)
            {
                mStatusLabel.text = "Provider 未设置";
                ApplyFilter();
                return;
            }

            // 从 JsonLocalizationProvider 获取所有文本
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

            var missingCount = 0;
            foreach (var entry in mTextEntries)
            {
                if (entry.IsMissing) missingCount++;
            }

            mStatusLabel.text = $"共 {mTextEntries.Count} 条文本，{missingCount} 条缺失";
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
                    if (entry.TextId.ToString().Contains(mSearchFilter) ||
                        (entry.Text != null && entry.Text.ToLower().Contains(mSearchFilter)))
                    {
                        mFilteredEntries.Add(entry);
                    }
                }
            }

            mTextListView.RefreshItems();
        }

        public override void OnActivate()
        {
            RefreshTextList();
        }

        [System.Obsolete("保留用于运行时刷新")]
        public override void OnUpdate()
        {
            // 运行时自动刷新
            if (IsPlaying)
            {
                // 可以添加定时刷新逻辑
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
