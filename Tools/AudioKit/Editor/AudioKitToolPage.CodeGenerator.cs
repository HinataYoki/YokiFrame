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
        private TextField mStartIdField;
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
            var scrollView = new ScrollView();
            scrollView.AddToClassList("yoki-audio-generator");
            container.Add(scrollView);
            
            var title = new Label("音频 ID 代码生成器");
            title.AddToClassList("yoki-audio-generator__title");
            scrollView.Add(title);
            
            // 扫描配置区块
            var scanSection = YokiFrameUIComponents.CreateSection("扫描配置");
            scrollView.Add(scanSection);
            scanSection.Add(CreatePathRow("扫描文件夹:", ref mScanFolderField, mScanFolder, p => { mScanFolder = p; mScanFolderField.value = p; }));
            scanSection.Add(CreatePathRow("输出路径:", ref mOutputPathField, mOutputPath, p => { mOutputPath = p; mOutputPathField.value = p; }, true));
            
            // 代码配置区块
            var codeSection = YokiFrameUIComponents.CreateSection("代码配置");
            scrollView.Add(codeSection);
            codeSection.Add(CreateTextRow("命名空间:", ref mNamespaceField, mNamespace, v => mNamespace = v));
            codeSection.Add(CreateTextRow("类名:", ref mClassNameField, mClassName, v => mClassName = v));
            codeSection.Add(CreateIntRow("起始 ID:", ref mStartIdField, mStartId, v => mStartId = v));
            
            // 生成选项区块
            var optionsSection = YokiFrameUIComponents.CreateSection("生成选项");
            scrollView.Add(optionsSection);
            mGeneratePathMapToggle = YokiFrameUIComponents.CreateModernToggle("生成路径映射字典", mGeneratePathMap, v => mGeneratePathMap = v);
            optionsSection.Add(mGeneratePathMapToggle);
            mGroupByFolderToggle = YokiFrameUIComponents.CreateModernToggle("按文件夹分组", mGroupByFolder, v => mGroupByFolder = v);
            optionsSection.Add(mGroupByFolderToggle);
            
            // 按钮行
            var buttonRow = YokiFrameUIComponents.CreateRow();
            buttonRow.AddToClassList("yoki-audio-generator__button-row");
            scrollView.Add(buttonRow);
            
            var scanBtn = YokiFrameUIComponents.CreateSecondaryButton("扫描音频文件", ScanAudioFiles);
            scanBtn.AddToClassList("yoki-audio-generator__scan-button");
            buttonRow.Add(scanBtn);
            
            mGenerateButton = YokiFrameUIComponents.CreatePrimaryButton("生成代码", GenerateCode);
            mGenerateButton.AddToClassList("yoki-audio-generator__generate-button");
            mGenerateButton.SetEnabled(false);
            buttonRow.Add(mGenerateButton);
            
            mResultsCountLabel = new Label("扫描结果");
            mResultsCountLabel.AddToClassList("yoki-audio-generator__results-label");
            scrollView.Add(mResultsCountLabel);
            
            mResultsListView = new ListView { makeItem = CreateResultItem, bindItem = BindResultItem };
            mResultsListView.AddToClassList("yoki-audio-generator__results-list");
            scrollView.Add(mResultsListView);
        }

        private VisualElement CreateTextRow(string labelText, ref TextField textField, string initialValue, Action<string> onChanged)
        {
            var field = new TextField { value = initialValue };
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            textField = field;
            return YokiFrameUIComponents.CreateCompactFormRow(labelText, field);
        }

        private VisualElement CreateIntRow(string labelText, ref TextField intField, int initialValue, Action<int> onChanged)
        {
            var field = new TextField { value = initialValue.ToString() };
            field.RegisterValueChangedCallback(evt =>
            {
                if (int.TryParse(evt.newValue, out int parsed))
                    onChanged?.Invoke(parsed);
            });
            intField = field;
            return YokiFrameUIComponents.CreateCompactFormRow(labelText, field);
        }

        private VisualElement CreatePathRow(string labelText, ref TextField textField, string initialValue, Action<string> onPathChanged, bool isFile = false)
        {
            var pathContainer = YokiFrameUIComponents.CreateRow();
            pathContainer.AddToClassList("yoki-audio-generator__path-container");
            
            var field = new TextField { value = initialValue };
            field.AddToClassList("yoki-audio-generator__path-field");
            field.RegisterValueChangedCallback(evt => onPathChanged?.Invoke(evt.newValue));
            textField = field;
            pathContainer.Add(field);
            
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
            }) { text = "..." };
            browseBtn.AddToClassList("yoki-audio-generator__browse-button");
            pathContainer.Add(browseBtn);
            
            return YokiFrameUIComponents.CreateCompactFormRow(labelText, pathContainer);
        }

        private VisualElement CreateResultItem()
        {
            var item = YokiFrameUIComponents.CreateListItemRow();
            
            var nameLabel = new Label();
            nameLabel.AddToClassList("yoki-audio-generator__result-name");
            item.Add(nameLabel);
            
            var idLabel = new Label();
            idLabel.AddToClassList("yoki-audio-generator__result-id");
            item.Add(idLabel);
            
            var pathLabel = new Label();
            pathLabel.AddToClassList("yoki-audio-generator__result-path");
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
