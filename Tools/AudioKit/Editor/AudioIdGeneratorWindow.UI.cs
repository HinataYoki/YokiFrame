#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// 音频 ID 生成器 - UI 构建方法
    /// </summary>
    public partial class AudioIdGeneratorWindow
    {
        #region UI 构建方法

        /// <summary>
        /// 构建扫描配置区块
        /// </summary>
        private void BuildScanConfigSection(VisualElement parent)
        {
            var section = CreateSection("扫描配置");
            parent.Add(section);

            // 扫描文件夹
            var scanRow = CreatePathRow("扫描文件夹：", mScanFolder, path =>
            {
                var folder = EditorUtility.OpenFolderPanel("选择音频文件夹", path, "");
                if (!string.IsNullOrEmpty(folder))
                {
                    var assetsIndex = folder.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                    mScanFolder = assetsIndex >= 0 ? folder[assetsIndex..] : folder;
                    mScanFolderField.value = mScanFolder;
                }
            }, out mScanFolderField);
            section.Add(scanRow);

            // 输出路径
            var outputRow = CreatePathRow("输出路径：", mOutputPath, path =>
            {
                var savePath = EditorUtility.SaveFilePanel("保存代码文件", System.IO.Path.GetDirectoryName(path), mClassName, "cs");
                if (!string.IsNullOrEmpty(savePath))
                {
                    var assetsIndex = savePath.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                    mOutputPath = assetsIndex >= 0 ? savePath[assetsIndex..] : savePath;
                    mOutputPathField.value = mOutputPath;
                }
            }, out mOutputPathField);
            section.Add(outputRow);
        }

        /// <summary>
        /// 构建代码配置区块
        /// </summary>
        private void BuildCodeConfigSection(VisualElement parent)
        {
            var section = CreateSection("代码配置");
            parent.Add(section);

            // 命名空间
            var nsRow = CreateFormRow("命名空间：");
            mNamespaceField = new TextField { value = mNamespace };
            mNamespaceField.style.flexGrow = 1;
            mNamespaceField.RegisterValueChangedCallback(evt => mNamespace = evt.newValue);
            nsRow.Add(mNamespaceField);
            section.Add(nsRow);

            // 类名
            var classRow = CreateFormRow("类名：");
            mClassNameField = new TextField { value = mClassName };
            mClassNameField.style.flexGrow = 1;
            mClassNameField.RegisterValueChangedCallback(evt => mClassName = evt.newValue);
            classRow.Add(mClassNameField);
            section.Add(classRow);

            // 起始 ID
            var idRow = CreateFormRow("起始 ID：");
            mStartIdField = new TextField { value = mStartId.ToString() };
            mStartIdField.style.width = 100;
            mStartIdField.RegisterValueChangedCallback(evt =>
            {
                if (int.TryParse(evt.newValue, out int value))
                    mStartId = value;
            });
            idRow.Add(mStartIdField);
            section.Add(idRow);
        }

        /// <summary>
        /// 构建生成选项区块
        /// </summary>
        private void BuildOptionsSection(VisualElement parent)
        {
            var section = CreateSection("生成选项");
            parent.Add(section);

            // 生成路径映射
            var pathMapRow = CreateFormRow("生成路径映射：");
            mGeneratePathMapToggle = new Toggle { value = mGeneratePathMap };
            mGeneratePathMapToggle.RegisterValueChangedCallback(evt => mGeneratePathMap = evt.newValue);
            pathMapRow.Add(mGeneratePathMapToggle);
            section.Add(pathMapRow);

            // 按文件夹分组
            var groupRow = CreateFormRow("按文件夹分组：");
            mGroupByFolderToggle = new Toggle { value = mGroupByFolder };
            mGroupByFolderToggle.RegisterValueChangedCallback(evt => mGroupByFolder = evt.newValue);
            groupRow.Add(mGroupByFolderToggle);
            section.Add(groupRow);
        }

        /// <summary>
        /// 构建按钮区块
        /// </summary>
        private void BuildButtonSection(VisualElement parent)
        {
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 12;
            buttonRow.style.marginBottom = 12;
            parent.Add(buttonRow);

            // 扫描按钮
            mScanButton = new Button(ScanAudioFiles) { text = "扫描音频文件" };
            mScanButton.style.flexGrow = 1;
            mScanButton.style.height = 32;
            mScanButton.style.marginRight = 8;
            mScanButton.style.backgroundColor = new StyleColor(new Color(0.25f, 0.45f, 0.65f));
            mScanButton.style.color = new StyleColor(Color.white);
            buttonRow.Add(mScanButton);

            // 生成按钮
            mGenerateButton = new Button(GenerateCode) { text = "生成代码" };
            mGenerateButton.style.flexGrow = 1;
            mGenerateButton.style.height = 32;
            mGenerateButton.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.3f));
            mGenerateButton.style.color = new StyleColor(Color.white);
            mGenerateButton.SetEnabled(false);
            buttonRow.Add(mGenerateButton);
        }

        /// <summary>
        /// 构建扫描结果区块
        /// </summary>
        private void BuildResultsSection(VisualElement parent)
        {
            mResultsContainer = new VisualElement();
            mResultsContainer.style.flexGrow = 1;
            mResultsContainer.style.display = DisplayStyle.None;
            parent.Add(mResultsContainer);

            // 结果标题
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.alignItems = Align.Center;
            header.style.marginBottom = 8;
            mResultsContainer.Add(header);

            var titleLabel = new Label("扫描结果");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            header.Add(titleLabel);

            mResultsCountLabel = new Label("共 0 个文件");
            mResultsCountLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            header.Add(mResultsCountLabel);

            // 结果列表
            BuildResultsListView();
        }

        private void BuildResultsListView()
        {
            mResultsListView = new ListView();
            mResultsListView.fixedItemHeight = 24;
            mResultsListView.makeItem = MakeResultItem;
            mResultsListView.bindItem = BindResultItem;
            mResultsListView.style.flexGrow = 1;
            mResultsListView.style.minHeight = 200;
            mResultsListView.style.backgroundColor = new StyleColor(new Color(0.15f, 0.15f, 0.15f));
            mResultsListView.style.borderTopLeftRadius = 4;
            mResultsListView.style.borderTopRightRadius = 4;
            mResultsListView.style.borderBottomLeftRadius = 4;
            mResultsListView.style.borderBottomRightRadius = 4;
            mResultsContainer.Add(mResultsListView);
        }

        /// <summary>
        /// 创建结果列表项
        /// </summary>
        private VisualElement MakeResultItem()
        {
            var item = new VisualElement();
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.height = 24;
            item.style.paddingLeft = 8;
            item.style.paddingRight = 8;

            // ID 标签
            var idLabel = new Label();
            idLabel.name = "id";
            idLabel.style.width = 60;
            idLabel.style.color = new StyleColor(new Color(0.5f, 0.8f, 0.5f));
            item.Add(idLabel);

            // 常量名标签
            var nameLabel = new Label();
            nameLabel.name = "name";
            nameLabel.style.width = 200;
            nameLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.5f));
            item.Add(nameLabel);

            // 路径标签
            var pathLabel = new Label();
            pathLabel.name = "path";
            pathLabel.style.flexGrow = 1;
            pathLabel.style.color = new StyleColor(new Color(0.6f, 0.6f, 0.6f));
            pathLabel.style.overflow = Overflow.Hidden;
            pathLabel.style.textOverflow = TextOverflow.Ellipsis;
            item.Add(pathLabel);

            return item;
        }

        /// <summary>
        /// 绑定结果列表项数据
        /// </summary>
        private void BindResultItem(VisualElement element, int index)
        {
            var info = mScannedFiles[index];
            element.Q<Label>("id").text = info.Id.ToString();
            element.Q<Label>("name").text = info.ConstantName;
            element.Q<Label>("path").text = info.Path;
        }

        #endregion

        #region UI 辅助方法

        /// <summary>
        /// 创建区块容器
        /// </summary>
        private VisualElement CreateSection(string title)
        {
            var section = new VisualElement();
            section.style.marginBottom = 12;
            section.style.paddingTop = 8;
            section.style.paddingBottom = 8;
            section.style.paddingLeft = 12;
            section.style.paddingRight = 12;
            section.style.backgroundColor = new StyleColor(new Color(0.2f, 0.2f, 0.2f));
            section.style.borderTopLeftRadius = 4;
            section.style.borderTopRightRadius = 4;
            section.style.borderBottomLeftRadius = 4;
            section.style.borderBottomRightRadius = 4;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 8;
            titleLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.8f));
            section.Add(titleLabel);

            return section;
        }

        /// <summary>
        /// 创建表单行
        /// </summary>
        private VisualElement CreateFormRow(string labelText)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginBottom = 6;

            var label = new Label(labelText);
            label.style.width = LABEL_WIDTH;
            label.style.minWidth = LABEL_WIDTH;
            label.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            row.Add(label);

            return row;
        }

        /// <summary>
        /// 创建路径选择行
        /// </summary>
        private VisualElement CreatePathRow(string labelText, string initialValue, Action<string> onBrowse, out TextField textField)
        {
            var row = CreateFormRow(labelText);

            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.flexGrow = 1;
            row.Add(pathContainer);

            var field = new TextField { value = initialValue };
            field.style.flexGrow = 1;
            field.SetEnabled(false);
            pathContainer.Add(field);

            // 使用局部变量捕获，避免在 lambda 中使用 out 参数
            var browseBtn = new Button(() => onBrowse?.Invoke(field.value)) { text = "..." };
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = 4;
            pathContainer.Add(browseBtn);

            textField = field;
            return row;
        }

        #endregion
    }
}
#endif
