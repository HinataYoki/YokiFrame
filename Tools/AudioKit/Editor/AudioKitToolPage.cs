using System;
using System.Collections.Generic;
using System.IO;
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
        public override int Priority => 40;

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
            
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 16;
            scrollView.style.paddingRight = 16;
            scrollView.style.paddingTop = 16;
            root.Add(scrollView);
            
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
    }
}
