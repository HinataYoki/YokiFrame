using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 工具页面 - UI Toolkit 版本
    /// </summary>
    public class AudioKitToolPage : YokiFrameToolPageBase
    {
        public override string PageName => "AudioKit";
        public override string PageIcon => KitIcons.AUDIOKIT;
        public override int Priority => 40;

        private enum TabType
        {
            CodeGenerator,
            RuntimeMonitor
        }

        private TabType mCurrentTab = TabType.RuntimeMonitor;
        private VisualElement mTabContent;
        
        // 运行时监控
        private ListView mHistoryListView;
        private Label mGlobalVolumeLabel;
        private Label mPlayingCountLabel;
        private VisualElement mChannelStatsContainer;
        private bool mAutoRefresh = true;
        private float mRefreshInterval = 0.5f;
        private float mLastRefreshTime;

        private const string ASSETS_PREFIX = "Assets";
        private static readonly string[] AUDIO_EXTENSIONS = { ".wav", ".mp3", ".ogg", ".aiff", ".aif", ".flac" };

        // 配置参数
        private string mScanFolder = "Assets/Audio";
        private string mOutputPath = "Assets/Scripts/Generated/AudioIds.cs";
        private string mNamespace = "Game";
        private string mClassName = "AudioIds";
        private int mStartId = 1001;
        private bool mGeneratePathMap = true;
        private bool mGroupByFolder = true;

        private const string PREF_SCAN_FOLDER = "AudioIdGenerator_ScanFolder";
        private const string PREF_OUTPUT_PATH = "AudioIdGenerator_OutputPath";
        private const string PREF_NAMESPACE = "AudioIdGenerator_Namespace";
        private const string PREF_CLASS_NAME = "AudioIdGenerator_ClassName";
        private const string PREF_START_ID = "AudioIdGenerator_StartId";
        private const string PREF_GENERATE_PATH_MAP = "AudioIdGenerator_GeneratePathMap";
        private const string PREF_GROUP_BY_FOLDER = "AudioIdGenerator_GroupByFolder";

        private readonly List<AudioFileInfo> mScannedFiles = new();
        private bool mHasScanned;

        // UI 元素引用
        private TextField mScanFolderField;
        private TextField mOutputPathField;
        private TextField mNamespaceField;
        private TextField mClassNameField;
        private IntegerField mStartIdField;
        private Toggle mGeneratePathMapToggle;
        private Toggle mGroupByFolderToggle;
        private ListView mResultsListView;
        private Label mResultsCountLabel;
        private UnityEngine.UIElements.Button mGenerateButton;

        protected override void BuildUI(VisualElement root)
        {
            LoadPrefs();
            
            // 标签页按钮
            var tabBar = new VisualElement();
            tabBar.style.flexDirection = FlexDirection.Row;
            tabBar.style.borderBottomWidth = 1;
            tabBar.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            tabBar.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
            root.Add(tabBar);
            
            var monitorTab = CreateTabButton("运行时监控", TabType.RuntimeMonitor);
            var codeGenTab = CreateTabButton("代码生成器", TabType.CodeGenerator);
            tabBar.Add(monitorTab);
            tabBar.Add(codeGenTab);
            
            // 标签页内容容器
            mTabContent = new VisualElement();
            mTabContent.style.flexGrow = 1;
            root.Add(mTabContent);
            
            // 默认显示运行时监控
            SwitchTab(TabType.RuntimeMonitor);
        }

        private UnityEngine.UIElements.Button CreateTabButton(string text, TabType tabType)
        {
            var btn = new UnityEngine.UIElements.Button(() => SwitchTab(tabType));
            btn.text = text;
            btn.style.paddingLeft = 20;
            btn.style.paddingRight = 20;
            btn.style.paddingTop = 10;
            btn.style.paddingBottom = 10;
            btn.style.borderLeftWidth = 0;
            btn.style.borderRightWidth = 0;
            btn.style.borderTopWidth = 0;
            btn.style.borderBottomWidth = 0;
            btn.style.backgroundColor = StyleKeyword.Null;
            btn.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            return btn;
        }

        private void SwitchTab(TabType tabType)
        {
            mCurrentTab = tabType;
            mTabContent.Clear();
            
            switch (tabType)
            {
                case TabType.CodeGenerator:
                    BuildCodeGeneratorUI(mTabContent);
                    break;
                case TabType.RuntimeMonitor:
                    BuildRuntimeMonitorUI(mTabContent);
                    break;
            }
        }

        #region 代码生成器 UI

        private void BuildCodeGeneratorUI(VisualElement container)
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 16;
            scrollView.style.paddingRight = 16;
            scrollView.style.paddingTop = 16;
            container.Add(scrollView);
            
            var title = new Label("音频 ID 代码生成器");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            scrollView.Add(title);
            
            // 扫描配置
            var scanSection = CreateSection("扫描配置");
            scrollView.Add(scanSection);
            
            scanSection.Add(CreatePathRow("扫描文件夹:", ref mScanFolderField, mScanFolder, path =>
            {
                mScanFolder = path;
                mScanFolderField.value = path;
            }));
            
            scanSection.Add(CreatePathRow("输出路径:", ref mOutputPathField, mOutputPath, path =>
            {
                mOutputPath = path;
                mOutputPathField.value = path;
            }, true));
            
            // 代码配置
            var codeSection = CreateSection("代码配置");
            scrollView.Add(codeSection);
            
            codeSection.Add(CreateTextRow("命名空间:", ref mNamespaceField, mNamespace, v => mNamespace = v));
            codeSection.Add(CreateTextRow("类名:", ref mClassNameField, mClassName, v => mClassName = v));
            codeSection.Add(CreateIntRow("起始 ID:", ref mStartIdField, mStartId, v => mStartId = v));
            
            // 生成选项
            var optionsSection = CreateSection("生成选项");
            scrollView.Add(optionsSection);
            
            mGeneratePathMapToggle = new Toggle("生成路径映射字典");
            mGeneratePathMapToggle.value = mGeneratePathMap;
            mGeneratePathMapToggle.RegisterValueChangedCallback(evt => mGeneratePathMap = evt.newValue);
            optionsSection.Add(mGeneratePathMapToggle);
            
            mGroupByFolderToggle = new Toggle("按文件夹分组");
            mGroupByFolderToggle.value = mGroupByFolder;
            mGroupByFolderToggle.RegisterValueChangedCallback(evt => mGroupByFolder = evt.newValue);
            optionsSection.Add(mGroupByFolderToggle);
            
            // 按钮区域
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 16;
            scrollView.Add(buttonRow);
            
            var scanBtn = new UnityEngine.UIElements.Button(ScanAudioFiles) { text = "扫描音频文件" };
            scanBtn.AddToClassList("action-button");
            scanBtn.style.flexGrow = 1;
            buttonRow.Add(scanBtn);
            
            mGenerateButton = new UnityEngine.UIElements.Button(GenerateCode) { text = "生成代码" };
            mGenerateButton.AddToClassList("action-button");
            mGenerateButton.AddToClassList("primary");
            mGenerateButton.style.flexGrow = 1;
            mGenerateButton.style.marginLeft = 8;
            mGenerateButton.SetEnabled(false);
            buttonRow.Add(mGenerateButton);
            
            // 结果区域
            mResultsCountLabel = new Label("扫描结果");
            mResultsCountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            mResultsCountLabel.style.marginTop = 16;
            mResultsCountLabel.style.marginBottom = 8;
            scrollView.Add(mResultsCountLabel);
            
            mResultsListView = new ListView();
            mResultsListView.makeItem = CreateResultItem;
            mResultsListView.bindItem = BindResultItem;
            mResultsListView.style.height = 300;
            mResultsListView.style.borderTopWidth = 1;
            mResultsListView.style.borderBottomWidth = 1;
            mResultsListView.style.borderLeftWidth = 1;
            mResultsListView.style.borderRightWidth = 1;
            mResultsListView.style.borderTopColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            mResultsListView.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            mResultsListView.style.borderLeftColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            mResultsListView.style.borderRightColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            scrollView.Add(mResultsListView);
        }

        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.style.marginBottom = 16;
            
            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 8;
            section.Add(titleLabel);
            
            return section;
        }

        private VisualElement CreateTextRow(string labelText, ref TextField textField, string initialValue, Action<string> onChanged)
        {
            var row = new VisualElement();
            row.AddToClassList("form-row");
            
            var label = new Label(labelText);
            label.AddToClassList("form-label");
            row.Add(label);
            
            textField = new TextField();
            textField.AddToClassList("form-field");
            textField.value = initialValue;
            textField.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            row.Add(textField);
            
            return row;
        }

        private VisualElement CreateIntRow(string labelText, ref IntegerField intField, int initialValue, Action<int> onChanged)
        {
            var row = new VisualElement();
            row.AddToClassList("form-row");
            
            var label = new Label(labelText);
            label.AddToClassList("form-label");
            row.Add(label);
            
            intField = new IntegerField();
            intField.AddToClassList("form-field");
            intField.value = initialValue;
            intField.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            row.Add(intField);
            
            return row;
        }

        private VisualElement CreatePathRow(string labelText, ref TextField textField, string initialValue, Action<string> onPathChanged, bool isFile = false)
        {
            var row = new VisualElement();
            row.AddToClassList("form-row");
            
            var label = new Label(labelText);
            label.AddToClassList("form-label");
            row.Add(label);
            
            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.flexGrow = 1;
            
            textField = new TextField();
            textField.style.flexGrow = 1;
            textField.value = initialValue;
            textField.RegisterValueChangedCallback(evt => onPathChanged?.Invoke(evt.newValue));
            pathContainer.Add(textField);
            
            var browseBtn = new UnityEngine.UIElements.Button(() =>
            {
                string path;
                if (isFile)
                {
                    path = EditorUtility.SaveFilePanel("保存代码文件", Path.GetDirectoryName(initialValue), mClassName, "cs");
                }
                else
                {
                    path = EditorUtility.OpenFolderPanel(labelText, initialValue, "");
                }
                
                if (!string.IsNullOrEmpty(path))
                {
                    var assetsIndex = path.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                    var newPath = assetsIndex >= 0 ? path[assetsIndex..] : path;
                    onPathChanged?.Invoke(newPath);
                }
            }) { text = "..." };
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = 4;
            pathContainer.Add(browseBtn);
            
            row.Add(pathContainer);
            
            return row;
        }

        private VisualElement CreateResultItem()
        {
            var item = new VisualElement();
            item.AddToClassList("history-item");
            
            var constName = new Label();
            constName.style.width = 200;
            constName.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            item.Add(constName);
            
            var idLabel = new Label();
            idLabel.style.width = 80;
            idLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 0.6f));
            item.Add(idLabel);
            
            var pathLabel = new Label();
            pathLabel.style.flexGrow = 1;
            pathLabel.style.fontSize = 10;
            pathLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            item.Add(pathLabel);
            
            return item;
        }

        private void BindResultItem(VisualElement element, int index)
        {
            var file = mScannedFiles[index];
            var labels = element.Query<Label>().ToList();
            
            if (labels.Count >= 3)
            {
                labels[0].text = file.ConstantName;
                labels[1].text = $"= {file.Id}";
                labels[2].text = file.Path;
            }
        }

        private void LoadPrefs()
        {
            mScanFolder = EditorPrefs.GetString(PREF_SCAN_FOLDER, "Assets/Audio");
            mOutputPath = EditorPrefs.GetString(PREF_OUTPUT_PATH, "Assets/Scripts/Generated/AudioIds.cs");
            mNamespace = EditorPrefs.GetString(PREF_NAMESPACE, "Game");
            mClassName = EditorPrefs.GetString(PREF_CLASS_NAME, "AudioIds");
            mStartId = EditorPrefs.GetInt(PREF_START_ID, 1001);
            mGeneratePathMap = EditorPrefs.GetBool(PREF_GENERATE_PATH_MAP, true);
            mGroupByFolder = EditorPrefs.GetBool(PREF_GROUP_BY_FOLDER, true);
        }

        private void SavePrefs()
        {
            EditorPrefs.SetString(PREF_SCAN_FOLDER, mScanFolder);
            EditorPrefs.SetString(PREF_OUTPUT_PATH, mOutputPath);
            EditorPrefs.SetString(PREF_NAMESPACE, mNamespace);
            EditorPrefs.SetString(PREF_CLASS_NAME, mClassName);
            EditorPrefs.SetInt(PREF_START_ID, mStartId);
            EditorPrefs.SetBool(PREF_GENERATE_PATH_MAP, mGeneratePathMap);
            EditorPrefs.SetBool(PREF_GROUP_BY_FOLDER, mGroupByFolder);
        }

        public override void OnDeactivate()
        {
            SavePrefs();
        }

        private void ScanAudioFiles()
        {
            mScannedFiles.Clear();
            mHasScanned = true;

            if (!Directory.Exists(mScanFolder))
            {
                EditorUtility.DisplayDialog("错误", $"文件夹不存在: {mScanFolder}", "确定");
                RefreshResults();
                return;
            }

            var currentId = mStartId;
            var files = Directory.GetFiles(mScanFolder, "*.*", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file).ToLowerInvariant();
                if (!IsAudioExtension(ext)) continue;

                var relativePath = file.Replace("\\", "/");
                var assetsIndex = relativePath.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                if (assetsIndex >= 0)
                {
                    relativePath = relativePath[assetsIndex..];
                }

                var pathWithoutExt = relativePath[..^ext.Length];

                var fileName = Path.GetFileNameWithoutExtension(file);
                var folderName = GetFolderCategory(relativePath);
                var constantName = GenerateConstantName(fileName, folderName);

                mScannedFiles.Add(new AudioFileInfo
                {
                    Name = fileName,
                    Path = pathWithoutExt,
                    Id = currentId++,
                    ConstantName = constantName,
                    FolderCategory = folderName
                });
            }

            if (mScannedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到音频文件", "确定");
            }

            RefreshResults();
        }

        private void RefreshResults()
        {
            mResultsCountLabel.text = $"扫描结果 ({mScannedFiles.Count} 个文件)";
            mResultsListView.itemsSource = mScannedFiles;
            mResultsListView.RefreshItems();
            mGenerateButton.SetEnabled(mHasScanned && mScannedFiles.Count > 0);
        }

        private static bool IsAudioExtension(string ext)
        {
            foreach (var audioExt in AUDIO_EXTENSIONS)
            {
                if (ext.Equals(audioExt, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private string GetFolderCategory(string path)
        {
            if (!mGroupByFolder) return string.Empty;

            var relativePath = path.Replace(mScanFolder, "").TrimStart('/');
            var parts = relativePath.Split('/');
            return parts.Length > 1 ? parts[0] : string.Empty;
        }

        private static string GenerateConstantName(string fileName, string folderCategory)
        {
            var name = fileName.ToUpperInvariant();

            name = name.Replace(" ", "_")
                       .Replace("-", "_")
                       .Replace(".", "_");

            while (name.Contains("__"))
            {
                name = name.Replace("__", "_");
            }

            if (char.IsDigit(name[0]))
            {
                name = "_" + name;
            }

            if (!string.IsNullOrEmpty(folderCategory))
            {
                var prefix = folderCategory.ToUpperInvariant().Replace(" ", "_").Replace("-", "_");
                name = $"{prefix}_{name}";
            }

            return name;
        }

        private void GenerateCode()
        {
            if (mScannedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有可生成的音频文件", "确定");
                return;
            }

            var directory = Path.GetDirectoryName(mOutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var code = AudioIdCodeGenerator.Generate(
                mScannedFiles,
                mNamespace,
                mClassName,
                mGeneratePathMap,
                mGroupByFolder
            );

            File.WriteAllText(mOutputPath, code);
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("成功", $"代码已生成到:\n{mOutputPath}", "确定");
        }

        #endregion

        #region 运行时监控 UI

        // 通道面板缓存
        private readonly Dictionary<int, ChannelPanelElements> mChannelPanels = new();

        private struct ChannelPanelElements
        {
            public Foldout Foldout;
            public Slider VolumeSlider;
            public Toggle MuteToggle;
            public Label PlayingCountLabel;
            public VisualElement PlayingList;
        }

        private void BuildRuntimeMonitorUI(VisualElement container)
        {
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 16;
            scrollView.style.paddingRight = 16;
            scrollView.style.paddingTop = 16;
            container.Add(scrollView);

            // 标题和控制栏
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.marginBottom = 16;
            scrollView.Add(headerRow);

            var title = new Label("运行时音频监控");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1;
            headerRow.Add(title);

            var autoRefreshToggle = new Toggle("自动刷新");
            autoRefreshToggle.value = mAutoRefresh;
            autoRefreshToggle.RegisterValueChangedCallback(evt => mAutoRefresh = evt.newValue);
            headerRow.Add(autoRefreshToggle);

            var refreshBtn = new UnityEngine.UIElements.Button(RefreshMonitorData) { text = "刷新" };
            refreshBtn.style.marginLeft = 8;
            headerRow.Add(refreshBtn);

            var clearBtn = new UnityEngine.UIElements.Button(() => AudioDebugger.ClearHistory()) { text = "清空历史" };
            clearBtn.style.marginLeft = 8;
            headerRow.Add(clearBtn);

            var stopAllBtn = new UnityEngine.UIElements.Button(() => { if (Application.isPlaying) AudioKit.StopAll(); }) { text = "停止全部" };
            stopAllBtn.style.marginLeft = 8;
            stopAllBtn.style.backgroundColor = new StyleColor(new Color(0.6f, 0.2f, 0.2f));
            headerRow.Add(stopAllBtn);

            // 全局状态卡片
            var globalCard = CreateCard("全局状态");
            scrollView.Add(globalCard);

            var globalContent = new VisualElement();
            globalContent.style.paddingLeft = 12;
            globalContent.style.paddingRight = 12;
            globalContent.style.paddingBottom = 12;
            globalCard.Add(globalContent);

            // 全局音量滑块
            var globalVolumeRow = new VisualElement();
            globalVolumeRow.style.flexDirection = FlexDirection.Row;
            globalVolumeRow.style.alignItems = Align.Center;
            globalVolumeRow.style.marginBottom = 8;
            globalContent.Add(globalVolumeRow);

            var globalVolumeLabel = new Label("全局音量:");
            globalVolumeLabel.style.width = 80;
            globalVolumeRow.Add(globalVolumeLabel);

            var globalVolumeSlider = new Slider(0f, 1f);
            globalVolumeSlider.style.flexGrow = 1;
            globalVolumeSlider.value = Application.isPlaying ? AudioKit.GetGlobalVolume() : 1f;
            globalVolumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (Application.isPlaying) AudioKit.SetGlobalVolume(evt.newValue);
            });
            globalVolumeRow.Add(globalVolumeSlider);

            mGlobalVolumeLabel = new Label("1.00");
            mGlobalVolumeLabel.style.width = 40;
            mGlobalVolumeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            globalVolumeRow.Add(mGlobalVolumeLabel);

            mPlayingCountLabel = new Label("正在播放: 0");
            mPlayingCountLabel.style.marginTop = 4;
            globalContent.Add(mPlayingCountLabel);

            // 通道面板容器
            mChannelStatsContainer = new VisualElement();
            mChannelStatsContainer.style.marginTop = 16;
            scrollView.Add(mChannelStatsContainer);

            // 创建内置通道面板
            CreateChannelPanel(0, "BGM", new Color(0.3f, 0.6f, 0.9f));
            CreateChannelPanel(1, "SFX", new Color(0.9f, 0.6f, 0.3f));
            CreateChannelPanel(2, "Voice", new Color(0.6f, 0.9f, 0.3f));
            CreateChannelPanel(3, "Ambient", new Color(0.6f, 0.3f, 0.9f));
            CreateChannelPanel(4, "UI", new Color(0.9f, 0.3f, 0.6f));

            // 播放历史
            var historyCard = CreateCard("播放历史");
            historyCard.style.marginTop = 16;
            scrollView.Add(historyCard);

            mHistoryListView = new ListView();
            mHistoryListView.makeItem = CreateHistoryItem;
            mHistoryListView.bindItem = BindHistoryItem;
            mHistoryListView.style.height = 150;
            mHistoryListView.style.marginLeft = 12;
            mHistoryListView.style.marginRight = 12;
            mHistoryListView.style.marginBottom = 12;
            historyCard.Add(mHistoryListView);

            // 初始刷新
            RefreshMonitorData();
        }

        private VisualElement CreateCard(string title)
        {
            var card = new VisualElement();
            card.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
            card.style.borderTopLeftRadius = 6;
            card.style.borderTopRightRadius = 6;
            card.style.borderBottomLeftRadius = 6;
            card.style.borderBottomRightRadius = 6;
            card.style.marginBottom = 8;

            var header = new Label(title);
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.paddingLeft = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 8;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.25f, 0.25f, 0.25f));
            card.Add(header);

            return card;
        }

        private void CreateChannelPanel(int channelId, string channelName, Color accentColor)
        {
            var panel = new Foldout();
            panel.text = $"  {channelName}";
            panel.value = channelId == 0; // BGM 默认展开
            panel.style.marginBottom = 8;
            panel.style.minHeight = 36;
            panel.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.18f));
            panel.style.borderTopLeftRadius = 6;
            panel.style.borderTopRightRadius = 6;
            panel.style.borderBottomLeftRadius = 6;
            panel.style.borderBottomRightRadius = 6;
            panel.style.borderLeftWidth = 3;
            panel.style.borderLeftColor = new StyleColor(accentColor);
            panel.style.paddingTop = 4;
            panel.style.paddingBottom = 4;

            var content = new VisualElement();
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingBottom = 8;
            panel.Add(content);

            // 控制行
            var controlRow = new VisualElement();
            controlRow.style.flexDirection = FlexDirection.Row;
            controlRow.style.alignItems = Align.Center;
            controlRow.style.marginBottom = 8;
            content.Add(controlRow);

            var muteToggle = new Toggle("静音");
            muteToggle.style.width = 60;
            muteToggle.RegisterValueChangedCallback(evt =>
            {
                if (Application.isPlaying) AudioKit.MuteChannel(channelId, evt.newValue);
            });
            controlRow.Add(muteToggle);

            var volumeLabel = new Label("音量:");
            volumeLabel.style.marginLeft = 16;
            controlRow.Add(volumeLabel);

            var volumeSlider = new Slider(0f, 1f);
            volumeSlider.style.flexGrow = 1;
            volumeSlider.style.marginLeft = 8;
            volumeSlider.value = Application.isPlaying ? AudioKit.GetChannelVolume(channelId) : 1f;
            volumeSlider.RegisterValueChangedCallback(evt =>
            {
                if (Application.isPlaying) AudioKit.SetChannelVolume(channelId, evt.newValue);
            });
            controlRow.Add(volumeSlider);

            var volumeValueLabel = new Label("1.00");
            volumeValueLabel.style.width = 35;
            volumeValueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            controlRow.Add(volumeValueLabel);

            var stopBtn = new UnityEngine.UIElements.Button(() =>
            {
                if (Application.isPlaying) AudioKit.StopChannel(channelId);
            }) { text = "停止" };
            stopBtn.style.marginLeft = 8;
            stopBtn.style.width = 50;
            controlRow.Add(stopBtn);

            // 播放数量
            var playingCountLabel = new Label("播放中: 0");
            playingCountLabel.style.fontSize = 11;
            playingCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            playingCountLabel.style.marginBottom = 4;
            content.Add(playingCountLabel);

            // 该通道的播放列表
            var playingList = new VisualElement();
            playingList.style.maxHeight = 100;
            content.Add(playingList);

            mChannelStatsContainer.Add(panel);

            // 缓存引用
            mChannelPanels[channelId] = new ChannelPanelElements
            {
                Foldout = panel,
                VolumeSlider = volumeSlider,
                MuteToggle = muteToggle,
                PlayingCountLabel = playingCountLabel,
                PlayingList = playingList
            };
        }

        private VisualElement CreateHistoryItem()
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;
            item.style.paddingTop = 4;
            item.style.paddingBottom = 4;
            item.style.borderBottomWidth = 1;
            item.style.borderBottomColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));

            var pathLabel = new Label();
            pathLabel.style.width = 300;
            pathLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            pathLabel.style.fontSize = 10;
            item.Add(pathLabel);

            var channelLabel = new Label();
            channelLabel.style.width = 80;
            channelLabel.style.color = new StyleColor(new Color(0.5f, 0.7f, 0.9f));
            channelLabel.style.fontSize = 9;
            item.Add(channelLabel);

            var timeLabel = new Label();
            timeLabel.style.flexGrow = 1;
            timeLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            timeLabel.style.fontSize = 9;
            item.Add(timeLabel);

            return item;
        }

        private void BindHistoryItem(VisualElement element, int index)
        {
            var history = AudioDebugger.GetPlayHistory();
            if (index >= history.Count) return;

            var record = history[index];
            var children = element.Children().ToList();

            if (children.Count >= 3)
            {
                (children[0] as Label).text = Path.GetFileName(record.Path);
                (children[1] as Label).text = GetChannelName(record.ChannelId);
                (children[2] as Label).text = $"{Time.time - record.StartTime:F1}s 前";
            }
        }

        private void RefreshMonitorData()
        {
            if (!Application.isPlaying)
            {
                mGlobalVolumeLabel.text = "N/A";
                mPlayingCountLabel.text = "正在播放: N/A (未运行)";
                
                // 清空通道面板播放列表
                foreach (var kvp in mChannelPanels)
                {
                    kvp.Value.PlayingCountLabel.text = "播放中: 0";
                    kvp.Value.PlayingList.Clear();
                }
                
                mHistoryListView.itemsSource = null;
                mHistoryListView.RefreshItems();
                return;
            }

            // 更新全局状态
            var globalVolume = AudioKit.GetGlobalVolume();
            mGlobalVolumeLabel.text = $"{globalVolume:F2}";
            
            var currentPlaying = AudioDebugger.GetCurrentPlaying();
            mPlayingCountLabel.text = $"正在播放: {currentPlaying.Count}";

            // 按通道分组播放列表
            var playingByChannel = new Dictionary<int, List<AudioDebugger.AudioPlayRecord>>();
            foreach (var record in currentPlaying)
            {
                if (!playingByChannel.TryGetValue(record.ChannelId, out var list))
                {
                    list = new List<AudioDebugger.AudioPlayRecord>();
                    playingByChannel[record.ChannelId] = list;
                }
                list.Add(record);
            }

            // 更新每个通道面板
            foreach (var kvp in mChannelPanels)
            {
                var channelId = kvp.Key;
                var panel = kvp.Value;
                
                // 更新音量滑块
                panel.VolumeSlider.SetValueWithoutNotify(AudioKit.GetChannelVolume(channelId));
                
                // 更新播放数量
                var count = playingByChannel.TryGetValue(channelId, out var records) ? records.Count : 0;
                panel.PlayingCountLabel.text = $"播放中: {count}";
                
                // 更新播放列表
                panel.PlayingList.Clear();
                if (records != null)
                {
                    foreach (var record in records)
                    {
                        var item = CreateChannelPlayingItem(record);
                        panel.PlayingList.Add(item);
                    }
                }
            }

            // 检查是否有自定义通道需要动态添加
            foreach (var channelId in playingByChannel.Keys)
            {
                if (channelId >= 5 && !mChannelPanels.ContainsKey(channelId))
                {
                    CreateChannelPanel(channelId, $"Custom{channelId}", new Color(0.5f, 0.5f, 0.5f));
                }
            }

            // 更新历史列表
            mHistoryListView.itemsSource = AudioDebugger.GetPlayHistory();
            mHistoryListView.RefreshItems();
        }

        private VisualElement CreateChannelPlayingItem(AudioDebugger.AudioPlayRecord record)
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.paddingTop = 2;
            item.style.paddingBottom = 2;

            var nameLabel = new Label(Path.GetFileName(record.Path));
            nameLabel.style.width = 180;
            nameLabel.style.fontSize = 10;
            nameLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            item.Add(nameLabel);

            var progressBar = new ProgressBar();
            progressBar.style.flexGrow = 1;
            progressBar.style.height = 8;
            progressBar.value = record.Progress * 100f;
            item.Add(progressBar);

            var timeLabel = new Label($"{record.CurrentTime:F1}s/{record.Duration:F1}s");
            timeLabel.style.width = 70;
            timeLabel.style.fontSize = 9;
            timeLabel.style.marginLeft = 8;
            timeLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            item.Add(timeLabel);

            return item;
        }

        private static string GetChannelName(int channelId)
        {
            return channelId switch
            {
                0 => "BGM",
                1 => "SFX",
                2 => "Voice",
                3 => "Ambient",
                4 => "UI",
                _ => $"Custom{channelId}"
            };
        }

        public override void OnUpdate()
        {
            if (mCurrentTab == TabType.RuntimeMonitor && mAutoRefresh && Application.isPlaying)
            {
                if (Time.realtimeSinceStartup - mLastRefreshTime >= mRefreshInterval)
                {
                    RefreshMonitorData();
                    mLastRefreshTime = Time.realtimeSinceStartup;
                }
            }
        }

        #endregion
    }
}
