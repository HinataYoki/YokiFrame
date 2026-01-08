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
    /// AudioKitToolPage - 代码生成器部分
    /// </summary>
    public partial class AudioKitToolPage
    {
        #region 代码生成器常量

        private const string PREF_SCAN_FOLDER = "AudioIdGenerator_ScanFolder";
        private const string PREF_OUTPUT_PATH = "AudioIdGenerator_OutputPath";
        private const string PREF_NAMESPACE = "AudioIdGenerator_Namespace";
        private const string PREF_CLASS_NAME = "AudioIdGenerator_ClassName";
        private const string PREF_START_ID = "AudioIdGenerator_StartId";
        private const string PREF_GENERATE_PATH_MAP = "AudioIdGenerator_GeneratePathMap";
        private const string PREF_GROUP_BY_FOLDER = "AudioIdGenerator_GroupByFolder";

        #endregion

        #region 代码生成器字段

        // 配置
        private string mScanFolder = "Assets/Audio";
        private string mOutputPath = "Assets/Scripts/Generated/AudioIds.cs";
        private string mNamespace = "Game";
        private string mClassName = "AudioIds";
        private int mStartId = 1001;
        private bool mGeneratePathMap = true;
        private bool mGroupByFolder = true;

        // UI 元素
        private TextField mScanFolderField;
        private TextField mOutputPathField;
        private TextField mNamespaceField;
        private TextField mClassNameField;
        private IntegerField mStartIdField;
        private VisualElement mGeneratePathMapToggle;
        private VisualElement mGroupByFolderToggle;
        private ListView mResultsListView;
        private Label mResultsCountLabel;
        private Button mGenerateButton;

        // 数据
        private readonly List<AudioFileInfo> mScannedFiles = new();
        private bool mHasScanned;

        #endregion

        #region 代码生成器 UI 构建

        private void BuildCodeGeneratorUI(VisualElement container)
        {
            var scrollView = new ScrollView { style = { flexGrow = 1, paddingLeft = 16, paddingRight = 16, paddingTop = 16 } };
            container.Add(scrollView);
            
            var title = new Label("音频 ID 代码生成器");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            scrollView.Add(title);
            
            var scanSection = CreateSection("扫描配置");
            scrollView.Add(scanSection);
            scanSection.Add(CreatePathRow("扫描文件夹:", ref mScanFolderField, mScanFolder, p => { mScanFolder = p; mScanFolderField.value = p; }));
            scanSection.Add(CreatePathRow("输出路径:", ref mOutputPathField, mOutputPath, p => { mOutputPath = p; mOutputPathField.value = p; }, true));
            
            var codeSection = CreateSection("代码配置");
            scrollView.Add(codeSection);
            codeSection.Add(CreateTextRow("命名空间:", ref mNamespaceField, mNamespace, v => mNamespace = v));
            codeSection.Add(CreateTextRow("类名:", ref mClassNameField, mClassName, v => mClassName = v));
            codeSection.Add(CreateIntRow("起始 ID:", ref mStartIdField, mStartId, v => mStartId = v));
            
            var optionsSection = CreateSection("生成选项");
            scrollView.Add(optionsSection);
            mGeneratePathMapToggle = YokiFrameUIComponents.CreateModernToggle("生成路径映射字典", mGeneratePathMap, v => mGeneratePathMap = v);
            optionsSection.Add(mGeneratePathMapToggle);
            mGroupByFolderToggle = YokiFrameUIComponents.CreateModernToggle("按文件夹分组", mGroupByFolder, v => mGroupByFolder = v);
            optionsSection.Add(mGroupByFolderToggle);
            
            var buttonRow = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 16 } };
            scrollView.Add(buttonRow);
            
            var scanBtn = new Button(ScanAudioFiles) { text = "扫描音频文件" };
            scanBtn.AddToClassList("action-button");
            scanBtn.style.flexGrow = 1;
            buttonRow.Add(scanBtn);
            
            mGenerateButton = new Button(GenerateCode) { text = "生成代码" };
            mGenerateButton.AddToClassList("action-button");
            mGenerateButton.AddToClassList("primary");
            mGenerateButton.style.flexGrow = 1;
            mGenerateButton.style.marginLeft = 8;
            mGenerateButton.SetEnabled(false);
            buttonRow.Add(mGenerateButton);
            
            mResultsCountLabel = new Label("扫描结果");
            mResultsCountLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            mResultsCountLabel.style.marginTop = 16;
            mResultsCountLabel.style.marginBottom = 8;
            scrollView.Add(mResultsCountLabel);
            
            mResultsListView = new ListView { makeItem = CreateResultItem, bindItem = BindResultItem };
            mResultsListView.style.height = 300;
            mResultsListView.style.borderTopWidth = mResultsListView.style.borderBottomWidth = 1;
            mResultsListView.style.borderLeftWidth = mResultsListView.style.borderRightWidth = 1;
            mResultsListView.style.borderTopColor = mResultsListView.style.borderBottomColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            mResultsListView.style.borderLeftColor = mResultsListView.style.borderRightColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            scrollView.Add(mResultsListView);
        }

        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement { style = { marginBottom = 16 } };
            var titleLabel = new Label(title) { style = { unityFontStyleAndWeight = FontStyle.Bold, marginBottom = 8 } };
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
            textField = new TextField { value = initialValue };
            textField.AddToClassList("form-field");
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
            intField = new IntegerField { value = initialValue };
            intField.AddToClassList("form-field");
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
            
            var pathContainer = new VisualElement { style = { flexDirection = FlexDirection.Row, flexGrow = 1 } };
            textField = new TextField { value = initialValue, style = { flexGrow = 1 } };
            textField.RegisterValueChangedCallback(evt => onPathChanged?.Invoke(evt.newValue));
            pathContainer.Add(textField);
            
            var browseBtn = new Button(() =>
            {
                string path = isFile 
                    ? EditorUtility.SaveFilePanel("保存代码文件", Path.GetDirectoryName(initialValue), mClassName, "cs")
                    : EditorUtility.OpenFolderPanel(labelText, initialValue, "");
                if (!string.IsNullOrEmpty(path))
                {
                    var assetsIndex = path.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                    onPathChanged?.Invoke(assetsIndex >= 0 ? path[assetsIndex..] : path);
                }
            }) { text = "...", style = { width = 30, marginLeft = 4 } };
            pathContainer.Add(browseBtn);
            row.Add(pathContainer);
            return row;
        }

        private VisualElement CreateResultItem()
        {
            var item = new VisualElement();
            item.AddToClassList("history-item");
            item.Add(new Label { style = { width = 200, color = new StyleColor(new Color(0.8f, 0.8f, 0.8f)) } });
            item.Add(new Label { style = { width = 80, color = new StyleColor(new Color(0.6f, 0.8f, 0.6f)) } });
            item.Add(new Label { style = { flexGrow = 1, fontSize = 10, color = new StyleColor(new Color(0.5f, 0.5f, 0.5f)) } });
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

        #endregion

        #region 配置持久化

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

        #endregion

        #region 扫描与生成

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
                if (assetsIndex >= 0) relativePath = relativePath[assetsIndex..];

                var pathWithoutExt = relativePath[..^ext.Length];
                var fileName = Path.GetFileNameWithoutExtension(file);
                var folderName = GetFolderCategory(relativePath);

                mScannedFiles.Add(new AudioFileInfo
                {
                    Name = fileName,
                    Path = pathWithoutExt,
                    Id = currentId++,
                    ConstantName = GenerateConstantName(fileName, folderName),
                    FolderCategory = folderName
                });
            }

            if (mScannedFiles.Count == 0)
                EditorUtility.DisplayDialog("提示", "未找到音频文件", "确定");

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
                if (ext.Equals(audioExt, StringComparison.OrdinalIgnoreCase)) return true;
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
            var name = fileName.ToUpperInvariant().Replace(" ", "_").Replace("-", "_").Replace(".", "_");
            while (name.Contains("__")) name = name.Replace("__", "_");
            if (char.IsDigit(name[0])) name = "_" + name;
            if (!string.IsNullOrEmpty(folderCategory))
                name = $"{folderCategory.ToUpperInvariant().Replace(" ", "_").Replace("-", "_")}_{name}";
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
                Directory.CreateDirectory(directory);

            var code = AudioIdCodeGenerator.Generate(mScannedFiles, mNamespace, mClassName, mGeneratePathMap, mGroupByFolder);
            File.WriteAllText(mOutputPath, code);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", $"代码已生成到:\n{mOutputPath}", "确定");
        }

        #endregion
    }
}
