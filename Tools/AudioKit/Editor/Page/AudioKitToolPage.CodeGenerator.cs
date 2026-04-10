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
    /// AudioKit 工具页中的代码生成子模块。
    /// 负责在工作台内完成音频 ID 扫描、配置编辑与代码生成流程。
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

        private string mScanFolder = "Assets/Audio";
        private string mOutputPath = "Assets/Scripts/Generated/AudioIds.cs";
        private string mNamespace = "Game";
        private string mClassName = "AudioIds";
        private int mStartId = 1001;
        private bool mGeneratePathMap = true;
        private bool mGroupByFolder = true;

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
        private Label mGeneratorScanPathMetric;
        private Label mGeneratorOutputMetric;
        private Label mGeneratorFileCountMetric;
        private Label mGeneratorModeMetric;

        private readonly List<AudioFileInfo> mScannedFiles = new();
        private bool mHasScanned;

        #endregion

        #region 代码生成器 UI 构建

        /// <summary>
        /// 构建代码生成标签页主界面。
        /// </summary>
        private void BuildCodeGeneratorUI(VisualElement container)
        {
            var scrollView = new ScrollView();
            scrollView.AddToClassList("yoki-audio-generator");
            scrollView.style.flexGrow = 1;
            container.Add(scrollView);

            var metricStrip = CreateKitMetricStrip();
            var (scanCard, scanValue) = CreateKitMetricCard("扫描目录", mScanFolder, "当前音频资源扫描根目录");
            var (outputCard, outputValue) = CreateKitMetricCard("输出文件", Path.GetFileName(mOutputPath), "代码生成目标文件");
            var (fileCard, fileValue) = CreateKitMetricCard("扫描结果", "0", "已识别的音频文件数量");
            var (modeCard, modeValue) = CreateKitMetricCard("分组模式", mGroupByFolder ? "按目录" : "扁平", "常量命名分组策略");
            metricStrip.Add(scanCard);
            metricStrip.Add(outputCard);
            metricStrip.Add(fileCard);
            metricStrip.Add(modeCard);
            scrollView.Add(metricStrip);

            mGeneratorScanPathMetric = scanValue;
            mGeneratorOutputMetric = outputValue;
            mGeneratorFileCountMetric = fileValue;
            mGeneratorModeMetric = modeValue;

            var (scanSection, scanBody) = CreateKitSectionPanel("扫描配置", "设置音频资源扫描目录与输出路径。", KitIcons.FOLDER);
            scrollView.Add(scanSection);
            scanBody.Add(CreatePathRow("扫描文件夹:", ref mScanFolderField, mScanFolder, path =>
            {
                mScanFolder = path;
                mScanFolderField.value = path;
                RefreshGeneratorMetrics();
            }));
            scanBody.Add(CreatePathRow("输出路径:", ref mOutputPathField, mOutputPath, path =>
            {
                mOutputPath = path;
                mOutputPathField.value = path;
                RefreshGeneratorMetrics();
            }, true));

            var (codeSection, codeBody) = CreateKitSectionPanel("代码配置", "定义命名空间、类名和起始 ID。", KitIcons.CODE);
            scrollView.Add(codeSection);
            codeBody.Add(CreateTextRow("命名空间:", ref mNamespaceField, mNamespace, value => mNamespace = value));
            codeBody.Add(CreateTextRow("类名:", ref mClassNameField, mClassName, value => mClassName = value));
            codeBody.Add(CreateIntRow("起始 ID:", ref mStartIdField, mStartId, value => mStartId = value));

            var (optionSection, optionBody) = CreateKitSectionPanel("生成选项", "控制路径字典生成与按目录分组规则。", KitIcons.SETTINGS);
            scrollView.Add(optionSection);
            mGeneratePathMapToggle = YokiFrameUIComponents.CreateModernToggle("生成路径映射字典", mGeneratePathMap, value => mGeneratePathMap = value);
            optionBody.Add(mGeneratePathMapToggle);
            mGroupByFolderToggle = YokiFrameUIComponents.CreateModernToggle("按文件夹分组", mGroupByFolder, value =>
            {
                mGroupByFolder = value;
                RefreshGeneratorMetrics();
            });
            optionBody.Add(mGroupByFolderToggle);

            var actionRow = YokiFrameUIComponents.CreateRow();
            actionRow.AddToClassList("yoki-audio-generator__button-row");
            scrollView.Add(actionRow);

            var scanBtn = YokiFrameUIComponents.CreateSecondaryButton("扫描音频文件", ScanAudioFiles);
            scanBtn.AddToClassList("yoki-audio-generator__scan-button");
            actionRow.Add(scanBtn);

            mGenerateButton = YokiFrameUIComponents.CreatePrimaryButton("生成代码", GenerateCode);
            mGenerateButton.AddToClassList("yoki-audio-generator__generate-button");
            mGenerateButton.SetEnabled(false);
            actionRow.Add(mGenerateButton);

            var (resultSection, resultBody) = CreateKitSectionPanel("扫描结果", "预览生成后的常量名、编号和资源路径。", KitIcons.DOCUMENTATION);
            scrollView.Add(resultSection);

            mResultsCountLabel = new Label("扫描结果");
            mResultsCountLabel.AddToClassList("yoki-audio-generator__results-label");
            resultBody.Add(mResultsCountLabel);

            mResultsListView = new ListView
            {
                makeItem = CreateResultItem,
                bindItem = BindResultItem
            };
            mResultsListView.AddToClassList("yoki-audio-generator__results-list");
            resultBody.Add(mResultsListView);

            RefreshGeneratorMetrics();
            RefreshResults();
        }

        /// <summary>
        /// 刷新代码生成页顶部指标。
        /// </summary>
        private void RefreshGeneratorMetrics()
        {
            if (mGeneratorScanPathMetric == null)
            {
                return;
            }

            mGeneratorScanPathMetric.text = mScanFolder;
            mGeneratorOutputMetric.text = Path.GetFileName(mOutputPath);
            mGeneratorFileCountMetric.text = mScannedFiles.Count.ToString();
            mGeneratorModeMetric.text = mGroupByFolder ? "按目录" : "扁平";
        }

        /// <summary>
        /// 创建文本输入行。
        /// </summary>
        private VisualElement CreateTextRow(string labelText, ref TextField textField, string initialValue, Action<string> onChanged)
        {
            var field = new TextField { value = initialValue };
            field.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            textField = field;
            return YokiFrameUIComponents.CreateCompactFormRow(labelText, field);
        }

        /// <summary>
        /// 创建整数输入行。
        /// </summary>
        private VisualElement CreateIntRow(string labelText, ref TextField intField, int initialValue, Action<int> onChanged)
        {
            var field = new TextField { value = initialValue.ToString() };
            field.RegisterValueChangedCallback(evt =>
            {
                if (int.TryParse(evt.newValue, out int parsed))
                {
                    onChanged?.Invoke(parsed);
                }
            });
            intField = field;
            return YokiFrameUIComponents.CreateCompactFormRow(labelText, field);
        }

        /// <summary>
        /// 创建带浏览按钮的路径输入行。
        /// </summary>
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
            })
            {
                text = "..."
            };
            browseBtn.AddToClassList("yoki-audio-generator__browse-button");
            pathContainer.Add(browseBtn);

            return YokiFrameUIComponents.CreateCompactFormRow(labelText, pathContainer);
        }

        /// <summary>
        /// 创建扫描结果列表项模板。
        /// </summary>
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

        /// <summary>
        /// 绑定扫描结果到列表项。
        /// </summary>
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

        /// <summary>
        /// 从 EditorPrefs 恢复代码生成器配置。
        /// </summary>
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

        /// <summary>
        /// 将代码生成器配置写回 EditorPrefs。
        /// </summary>
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

        /// <summary>
        /// 扫描目标目录中的音频文件，并刷新结果列表。
        /// </summary>
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
                if (!IsAudioExtension(ext))
                {
                    continue;
                }

                var relativePath = file.Replace("\\", "/");
                var assetsIndex = relativePath.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                if (assetsIndex >= 0)
                {
                    relativePath = relativePath[assetsIndex..];
                }

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
            {
                EditorUtility.DisplayDialog("提示", "未找到音频文件。", "确定");
            }

            RefreshResults();
        }

        /// <summary>
        /// 刷新扫描结果列表与按钮状态。
        /// </summary>
        private void RefreshResults()
        {
            if (mResultsCountLabel != null)
            {
                mResultsCountLabel.text = $"扫描结果 ({mScannedFiles.Count} 个文件)";
            }

            if (mResultsListView != null)
            {
                mResultsListView.itemsSource = mScannedFiles;
                mResultsListView.RefreshItems();
            }

            mGenerateButton?.SetEnabled(mHasScanned && mScannedFiles.Count > 0);
            RefreshGeneratorMetrics();
        }

        /// <summary>
        /// 判断扩展名是否属于音频文件。
        /// </summary>
        private static bool IsAudioExtension(string ext)
        {
            foreach (var audioExt in AUDIO_EXTENSIONS)
            {
                if (ext.Equals(audioExt, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取文件所属的一级目录分类。
        /// </summary>
        private string GetFolderCategory(string path)
        {
            if (!mGroupByFolder)
            {
                return string.Empty;
            }

            var relativePath = path.Replace(mScanFolder, "").TrimStart('/');
            var parts = relativePath.Split('/');
            return parts.Length > 1 ? parts[0] : string.Empty;
        }

        /// <summary>
        /// 根据文件名和分类生成常量名。
        /// </summary>
        private static string GenerateConstantName(string fileName, string folderCategory)
        {
            var name = fileName.ToUpperInvariant().Replace(" ", "_").Replace("-", "_").Replace(".", "_");
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
                name = $"{folderCategory.ToUpperInvariant().Replace(" ", "_").Replace("-", "_")}_{name}";
            }

            return name;
        }

        /// <summary>
        /// 生成音频 ID 代码文件。
        /// </summary>
        private void GenerateCode()
        {
            if (mScannedFiles.Count == 0)
            {
                EditorUtility.DisplayDialog("错误", "没有可生成的音频文件。", "确定");
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
                mGroupByFolder);

            File.WriteAllText(mOutputPath, code);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("成功", $"代码已生成到:\n{mOutputPath}", "确定");
        }

        #endregion
    }
}
