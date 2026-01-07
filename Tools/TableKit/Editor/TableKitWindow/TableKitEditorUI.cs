#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using SimpleJSON;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKit 编辑器 UI 核心逻辑
    /// 可被独立窗口或 YokiFrame 工具页面复用
    /// </summary>
    public class TableKitEditorUI
    {
        #region 配置参数

        private string mEditorDataPath;
        private string mRuntimePathPattern;
        private string mLubanWorkDir;
        private string mLubanDllPath;
        private string mTarget;
        private string mCodeTarget;
        private string mDataTarget;
        private string mOutputDataDir;
        private string mOutputCodeDir;
        private bool mUseAssemblyDefinition;
        private string mAssemblyName;
        private bool mGenerateExternalTypeUtil;

        #endregion

        #region UI 元素引用

        private TextField mEditorDataPathField;
        private TextField mRuntimePathPatternField;
        private TextField mLubanWorkDirField;
        private TextField mLubanDllPathField;
        private DropdownField mTargetDropdown;
        private DropdownField mCodeTargetDropdown;
        private DropdownField mDataTargetDropdown;
        private TextField mOutputDataDirField;
        private TextField mOutputCodeDirField;
        private Toggle mUseAssemblyToggle;
        private TextField mAssemblyNameField;
        private Toggle mGenerateExternalTypeUtilToggle;
        private Label mStatusLabel;
        private Label mLoadModeLabel;
        private Label mGenerateStatusLabel;
        private VisualElement mTablesInfoContainer;
        private VisualElement mLogContainer;
        private VisualElement mDataPreviewContainer;
        private Button mGenerateBtn;

        #endregion

        #region EditorPrefs 键

        private const string PREF_EDITOR_DATA_PATH = "TableKit_EditorDataPath";
        private const string PREF_RUNTIME_PATH_PATTERN = "TableKit_RuntimePathPattern";
        private const string PREF_LUBAN_WORK_DIR = "TableKit_LubanWorkDir";
        private const string PREF_LUBAN_DLL_PATH = "TableKit_LubanDllPath";
        private const string PREF_TARGET = "TableKit_Target";
        private const string PREF_CODE_TARGET = "TableKit_CodeTarget";
        private const string PREF_DATA_TARGET = "TableKit_DataTarget";
        private const string PREF_OUTPUT_DATA_DIR = "TableKit_OutputDataDir";
        private const string PREF_OUTPUT_CODE_DIR = "TableKit_OutputCodeDir";
        private const string PREF_USE_ASSEMBLY = "TableKit_UseAssembly";
        private const string PREF_ASSEMBLY_NAME = "TableKit_AssemblyName";
        private const string PREF_GENERATE_EXTERNAL_TYPE_UTIL = "TableKit_GenerateExternalTypeUtil";

        #endregion

        #region 下拉选项

        private static readonly string[] TARGET_OPTIONS = { "client", "server", "all" };
        private static readonly string[] CODE_TARGET_OPTIONS = { "cs-bin", "cs-simple-json", "cs-newtonsoft-json", "cs-dotnet-json" };
        private static readonly string[] DATA_TARGET_OPTIONS = { "bin", "json" };

        #endregion

        /// <summary>
        /// 构建完整 UI
        /// </summary>
        public VisualElement BuildUI()
        {
            LoadPrefs();
            
            var root = new VisualElement();
            root.style.flexGrow = 1;
            
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            scrollView.style.paddingLeft = 16;
            scrollView.style.paddingRight = 16;
            scrollView.style.paddingTop = 16;
            root.Add(scrollView);
            
            // 标题
            var title = new Label("TableKit 配置表系统");
            title.style.fontSize = 16;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.marginBottom = 16;
            scrollView.Add(title);
            
            // Luban 生成配置卡片
            var lubanCard = CreateCard("Luban 生成配置");
            scrollView.Add(lubanCard);
            BuildLubanConfigSection(lubanCard);
            
            // 状态卡片
            var statusCard = CreateCard("运行状态");
            statusCard.style.marginTop = 16;
            scrollView.Add(statusCard);
            BuildStatusSection(statusCard);
            
            // 路径配置卡片
            var pathCard = CreateCard("TableKit 路径配置");
            pathCard.style.marginTop = 16;
            scrollView.Add(pathCard);
            BuildPathConfigSection(pathCard);
            
            // 操作按钮卡片
            var actionsCard = CreateCard("操作");
            actionsCard.style.marginTop = 16;
            scrollView.Add(actionsCard);
            BuildActionsSection(actionsCard);
            
            // 生成日志卡片
            var logCard = CreateCard("生成日志");
            logCard.style.marginTop = 16;
            scrollView.Add(logCard);
            BuildLogSection(logCard);
            
            // 数据预览卡片
            var previewCard = CreateCard("数据预览");
            previewCard.style.marginTop = 16;
            scrollView.Add(previewCard);
            BuildDataPreviewSection(previewCard);
            
            // 配置表信息卡片
            var tablesCard = CreateCard("配置表信息");
            tablesCard.style.marginTop = 16;
            scrollView.Add(tablesCard);
            BuildTablesInfoSection(tablesCard);
            
            // 初始刷新
            RefreshStatus();
            
            return root;
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


        private void BuildLubanConfigSection(VisualElement container)
        {
            var content = new VisualElement();
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingBottom = 12;
            container.Add(content);
            
            // Luban 配置说明
            var configHint = new Label("注意: Luban 工具不应放置在 Assets 内部，推荐放置在与 Assets 同级目录或其他位置");
            configHint.style.fontSize = 11;
            configHint.style.color = new StyleColor(new Color(0.9f, 0.7f, 0.4f));
            configHint.style.marginBottom = 8;
            content.Add(configHint);
            
            // Luban 工作目录
            content.Add(CreatePathRowAbsolute("Luban 工作目录:", ref mLubanWorkDirField, mLubanWorkDir, path =>
            {
                mLubanWorkDir = path;
                mLubanWorkDirField.value = path;
                SavePrefs();
            }, "选择包含 luban.conf 的目录"));
            
            // Luban 工作目录说明
            var workDirHint = new Label("需包含: luban.conf、Defines/、Datas/ 目录 (相对于项目根目录)");
            workDirHint.style.fontSize = 10;
            workDirHint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            workDirHint.style.marginLeft = 150;
            workDirHint.style.marginTop = 2;
            content.Add(workDirHint);
            
            // Luban.dll 路径
            content.Add(CreateFileRow("Luban.dll 路径:", ref mLubanDllPathField, mLubanDllPath, path =>
            {
                mLubanDllPath = path;
                mLubanDllPathField.value = path;
                SavePrefs();
            }, "dll", "选择 Luban.dll"));
            
            // Luban.dll 路径说明
            var dllHint = new Label("Luban 命令行工具 DLL (相对于项目根目录)");
            dllHint.style.fontSize = 10;
            dllHint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            dllHint.style.marginLeft = 150;
            dllHint.style.marginTop = 2;
            content.Add(dllHint);
            
            // Target 下拉
            var targetRow = CreateDropdownRow("Target (-t):", ref mTargetDropdown, TARGET_OPTIONS, mTarget, value =>
            {
                mTarget = value;
                SavePrefs();
            });
            content.Add(targetRow);
            
            // Code Target 下拉
            var codeTargetRow = CreateDropdownRow("Code Target (-c):", ref mCodeTargetDropdown, CODE_TARGET_OPTIONS, mCodeTarget, value =>
            {
                mCodeTarget = value;
                SavePrefs();
            });
            content.Add(codeTargetRow);
            
            // Data Target 下拉
            var dataTargetRow = CreateDropdownRow("Data Target (-d):", ref mDataTargetDropdown, DATA_TARGET_OPTIONS, mDataTarget, value =>
            {
                mDataTarget = value;
                SavePrefs();
            });
            content.Add(dataTargetRow);
            
            // 数据输出目录
            content.Add(CreatePathRow("数据输出目录:", ref mOutputDataDirField, mOutputDataDir, path =>
            {
                mOutputDataDir = path;
                mOutputDataDirField.value = path;
                SavePrefs();
            }));
            
            // 代码输出目录
            content.Add(CreatePathRow("代码输出目录:", ref mOutputCodeDirField, mOutputCodeDir, path =>
            {
                mOutputCodeDir = path;
                mOutputCodeDirField.value = path;
                SavePrefs();
            }));
            
            // 使用独立程序集开关
            var assemblyRow = new VisualElement();
            assemblyRow.style.flexDirection = FlexDirection.Row;
            assemblyRow.style.alignItems = Align.Center;
            assemblyRow.style.marginTop = 8;
            content.Add(assemblyRow);
            
            var assemblyLabel = new Label("使用独立程序集:");
            assemblyLabel.style.width = 150;
            assemblyRow.Add(assemblyLabel);
            
            mUseAssemblyToggle = new Toggle();
            mUseAssemblyToggle.value = mUseAssemblyDefinition;
            mUseAssemblyToggle.RegisterValueChangedCallback(evt =>
            {
                mUseAssemblyDefinition = evt.newValue;
                SavePrefs();
            });
            assemblyRow.Add(mUseAssemblyToggle);
            
            var assemblyHint = new Label("(开启后代码会放入独立程序集)");
            assemblyHint.style.fontSize = 10;
            assemblyHint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            assemblyHint.style.marginLeft = 8;
            assemblyRow.Add(assemblyHint);
            
            // 程序集名称输入
            var assemblyNameRow = new VisualElement();
            assemblyNameRow.style.flexDirection = FlexDirection.Row;
            assemblyNameRow.style.alignItems = Align.Center;
            assemblyNameRow.style.marginTop = 8;
            assemblyNameRow.style.marginLeft = 150;
            content.Add(assemblyNameRow);
            
            var assemblyNameLabel = new Label("程序集名称:");
            assemblyNameLabel.style.width = 80;
            assemblyNameRow.Add(assemblyNameLabel);
            
            mAssemblyNameField = new TextField();
            mAssemblyNameField.style.flexGrow = 1;
            mAssemblyNameField.value = mAssemblyName;
            mAssemblyNameField.SetEnabled(mUseAssemblyDefinition);
            mAssemblyNameField.RegisterValueChangedCallback(evt =>
            {
                mAssemblyName = evt.newValue;
                SavePrefs();
            });
            assemblyNameRow.Add(mAssemblyNameField);
            
            mUseAssemblyToggle.RegisterValueChangedCallback(evt =>
            {
                mAssemblyNameField.SetEnabled(evt.newValue);
            });
            
            // 生成 ExternalTypeUtil 开关
            var externalTypeRow = new VisualElement();
            externalTypeRow.style.flexDirection = FlexDirection.Row;
            externalTypeRow.style.alignItems = Align.Center;
            externalTypeRow.style.marginTop = 8;
            content.Add(externalTypeRow);
            
            var externalTypeLabel = new Label("生成 ExternalTypeUtil:");
            externalTypeLabel.style.width = 150;
            externalTypeRow.Add(externalTypeLabel);
            
            mGenerateExternalTypeUtilToggle = new Toggle();
            mGenerateExternalTypeUtilToggle.value = mGenerateExternalTypeUtil;
            mGenerateExternalTypeUtilToggle.RegisterValueChangedCallback(evt =>
            {
                mGenerateExternalTypeUtil = evt.newValue;
                SavePrefs();
            });
            externalTypeRow.Add(mGenerateExternalTypeUtilToggle);
            
            var externalTypeHint = new Label("(Luban vector 类型转 Unity Vector)");
            externalTypeHint.style.fontSize = 10;
            externalTypeHint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            externalTypeHint.style.marginLeft = 8;
            externalTypeRow.Add(externalTypeHint);
            
            // 生成状态
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginTop = 12;
            content.Add(statusRow);
            
            mGenerateStatusLabel = new Label("就绪");
            mGenerateStatusLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 0.6f));
            statusRow.Add(mGenerateStatusLabel);
            
            // 生成按钮
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 12;
            content.Add(buttonRow);
            
            mGenerateBtn = new Button(GenerateLuban) { text = "生成配置表" };
            mGenerateBtn.style.flexGrow = 1;
            mGenerateBtn.style.height = 32;
            mGenerateBtn.style.backgroundColor = new StyleColor(new Color(0.2f, 0.5f, 0.3f));
            buttonRow.Add(mGenerateBtn);
            
            var validateBtn = new Button(ValidateLuban) { text = "仅验证" };
            validateBtn.style.width = 80;
            validateBtn.style.height = 32;
            validateBtn.style.marginLeft = 8;
            buttonRow.Add(validateBtn);
            
            var openFolderBtn = new Button(OpenLubanFolder) { text = "打开目录" };
            openFolderBtn.style.width = 80;
            openFolderBtn.style.height = 32;
            openFolderBtn.style.marginLeft = 8;
            buttonRow.Add(openFolderBtn);
        }


        private void BuildStatusSection(VisualElement container)
        {
            var content = new VisualElement();
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingBottom = 12;
            container.Add(content);
            
            // 运行时初始化状态
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            statusRow.style.marginTop = 8;
            content.Add(statusRow);
            
            var statusLabel = new Label("运行时状态:");
            statusLabel.style.width = 100;
            statusRow.Add(statusLabel);
            
            mStatusLabel = new Label("未初始化");
            mStatusLabel.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
            statusRow.Add(mStatusLabel);
            
            var statusHint = new Label("(TableKit 在运行时是否已加载配置表数据)");
            statusHint.style.fontSize = 10;
            statusHint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            statusHint.style.marginLeft = 8;
            statusRow.Add(statusHint);
            
            // 加载模式
            var loadModeRow = new VisualElement();
            loadModeRow.style.flexDirection = FlexDirection.Row;
            loadModeRow.style.alignItems = Align.Center;
            loadModeRow.style.marginTop = 4;
            content.Add(loadModeRow);
            
            var loadModeLabel = new Label("加载模式:");
            loadModeLabel.style.width = 100;
            loadModeRow.Add(loadModeLabel);
            
            mLoadModeLabel = new Label("Binary");
            mLoadModeLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 0.6f));
            loadModeRow.Add(mLoadModeLabel);
        }

        private void BuildPathConfigSection(VisualElement container)
        {
            var content = new VisualElement();
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingBottom = 12;
            container.Add(content);
            
            // 编辑器数据路径
            content.Add(CreatePathRow("编辑器数据路径:", ref mEditorDataPathField, mEditorDataPath, path =>
            {
                mEditorDataPath = path;
                mEditorDataPathField.value = path;
                SavePrefs();
            }));
            
            // 运行时路径模式
            content.Add(CreateTextRow("运行时路径模式:", ref mRuntimePathPatternField, mRuntimePathPattern, value =>
            {
                mRuntimePathPattern = value;
                SavePrefs();
            }));
            
            // 提示
            var hint = new Label("提示: {0} 为数据文件名占位符 (如 item、skill 等)\n• 可寻址模式 (YooAsset/Addressables/Resources): 填 {0}\n• 完整路径模式: 填 Assets/Art/Table/{0} → 加载 item 时得到 Assets/Art/Table/item");
            hint.style.fontSize = 10;
            hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            hint.style.marginTop = 8;
            hint.style.whiteSpace = WhiteSpace.Normal;
            content.Add(hint);
            
            // 自定义加载器提示
            var customLoaderHint = new Label(
                "自定义加载器: 可通过 TableKit.SetBinaryLoader / SetJsonLoader 注册自定义加载方式\n" +
                "YooAsset 示例:\n" +
                "TableKit.SetBinaryLoader(name => {\n" +
                "    var handle = package.LoadAssetSync<TextAsset>(name);\n" +
                "    return handle.AssetObject != null ? ((TextAsset)handle.AssetObject).bytes : null;\n" +
                "});");
            customLoaderHint.style.fontSize = 10;
            customLoaderHint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            customLoaderHint.style.marginTop = 8;
            customLoaderHint.style.whiteSpace = WhiteSpace.Normal;
            customLoaderHint.style.unityFontStyleAndWeight = FontStyle.Italic;
            content.Add(customLoaderHint);
        }

        private void BuildActionsSection(VisualElement container)
        {
            var content = new VisualElement();
            content.style.paddingLeft = 12;
            content.style.paddingRight = 12;
            content.style.paddingBottom = 12;
            container.Add(content);
            
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.marginTop = 8;
            content.Add(buttonRow);
            
            // 刷新编辑器缓存按钮
            var refreshBtn = new Button(RefreshEditorCache) { text = "刷新编辑器缓存" };
            refreshBtn.style.flexGrow = 1;
            refreshBtn.style.height = 30;
            buttonRow.Add(refreshBtn);
            
            // 清理按钮
            var clearBtn = new Button(ClearAll) { text = "清理所有" };
            clearBtn.style.flexGrow = 1;
            clearBtn.style.height = 30;
            clearBtn.style.marginLeft = 8;
            clearBtn.style.backgroundColor = new StyleColor(new Color(0.6f, 0.3f, 0.3f));
            buttonRow.Add(clearBtn);
            
            // 提示说明
            var hint = new Label("刷新编辑器缓存: 重新加载编辑器环境下的配置表数据\n清理所有: 清除编辑器环境下缓存的配置表数据 (不影响运行时)");
            hint.style.fontSize = 10;
            hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            hint.style.marginTop = 4;
            hint.style.whiteSpace = WhiteSpace.Normal;
            content.Add(hint);
        }

        private void BuildLogSection(VisualElement container)
        {
            mLogContainer = new VisualElement();
            mLogContainer.style.paddingLeft = 12;
            mLogContainer.style.paddingRight = 12;
            mLogContainer.style.paddingBottom = 12;
            mLogContainer.style.maxHeight = 200;
            container.Add(mLogContainer);
            
            var logScroll = new ScrollView();
            logScroll.style.flexGrow = 1;
            logScroll.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
            logScroll.style.borderTopLeftRadius = 4;
            logScroll.style.borderTopRightRadius = 4;
            logScroll.style.borderBottomLeftRadius = 4;
            logScroll.style.borderBottomRightRadius = 4;
            logScroll.style.marginTop = 8;
            logScroll.style.minHeight = 100;
            mLogContainer.Add(logScroll);
            
            var logLabel = new Label("等待生成...");
            logLabel.name = "log-content";
            logLabel.style.fontSize = 11;
            logLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            logLabel.style.paddingLeft = 8;
            logLabel.style.paddingTop = 4;
            logLabel.style.whiteSpace = WhiteSpace.Normal;
            logScroll.Add(logLabel);
        }

        private void BuildDataPreviewSection(VisualElement container)
        {
            mDataPreviewContainer = new VisualElement();
            mDataPreviewContainer.style.paddingLeft = 12;
            mDataPreviewContainer.style.paddingRight = 12;
            mDataPreviewContainer.style.paddingBottom = 12;
            container.Add(mDataPreviewContainer);
            
            var hint = new Label("点击「仅验证」后显示数据预览");
            hint.name = "preview-hint";
            hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            hint.style.marginTop = 8;
            mDataPreviewContainer.Add(hint);
        }

        private void BuildTablesInfoSection(VisualElement container)
        {
            mTablesInfoContainer = new VisualElement();
            mTablesInfoContainer.style.paddingLeft = 12;
            mTablesInfoContainer.style.paddingRight = 12;
            mTablesInfoContainer.style.paddingBottom = 12;
            container.Add(mTablesInfoContainer);
            
            var hint = new Label("点击「刷新编辑器缓存」加载配置表信息");
            hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            hint.style.marginTop = 8;
            mTablesInfoContainer.Add(hint);
        }


        #region UI 辅助方法

        private VisualElement CreateTextRow(string labelText, ref TextField textField, string initialValue, Action<string> onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;
            
            var label = new Label(labelText);
            label.style.width = 150;
            row.Add(label);
            
            textField = new TextField();
            textField.style.flexGrow = 1;
            textField.value = initialValue;
            textField.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            row.Add(textField);
            
            return row;
        }

        private VisualElement CreatePathRow(string labelText, ref TextField textField, string initialValue, Action<string> onPathChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;
            
            var label = new Label(labelText);
            label.style.width = 150;
            row.Add(label);
            
            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.flexGrow = 1;
            
            textField = new TextField();
            textField.style.flexGrow = 1;
            textField.value = initialValue;
            textField.RegisterValueChangedCallback(evt => onPathChanged?.Invoke(evt.newValue));
            pathContainer.Add(textField);
            
            var browseBtn = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel(labelText, initialValue, "");
                if (!string.IsNullOrEmpty(path))
                {
                    var assetsIndex = path.IndexOf("Assets", StringComparison.Ordinal);
                    var newPath = assetsIndex >= 0 ? path.Substring(assetsIndex) : path;
                    if (!newPath.EndsWith("/")) newPath += "/";
                    onPathChanged?.Invoke(newPath);
                }
            }) { text = "..." };
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = 4;
            pathContainer.Add(browseBtn);
            
            row.Add(pathContainer);
            return row;
        }

        private VisualElement CreatePathRowAbsolute(string labelText, ref TextField textField, string initialValue, Action<string> onPathChanged, string dialogTitle)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;
            
            var label = new Label(labelText);
            label.style.width = 150;
            row.Add(label);
            
            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.flexGrow = 1;
            
            textField = new TextField();
            textField.style.flexGrow = 1;
            textField.value = initialValue;
            textField.RegisterValueChangedCallback(evt => onPathChanged?.Invoke(evt.newValue));
            pathContainer.Add(textField);
            
            var browseBtn = new Button(() =>
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var startPath = string.IsNullOrEmpty(initialValue) 
                    ? projectRoot 
                    : (Path.IsPathRooted(initialValue) ? initialValue : Path.Combine(projectRoot, initialValue));
                var path = EditorUtility.OpenFolderPanel(dialogTitle, startPath, "");
                if (!string.IsNullOrEmpty(path))
                {
                    var relativePath = GetRelativePath(projectRoot, path);
                    onPathChanged?.Invoke(relativePath);
                }
            }) { text = "..." };
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = 4;
            pathContainer.Add(browseBtn);
            
            row.Add(pathContainer);
            return row;
        }

        private VisualElement CreateFileRow(string labelText, ref TextField textField, string initialValue, Action<string> onPathChanged, string extension, string dialogTitle)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;
            
            var label = new Label(labelText);
            label.style.width = 150;
            row.Add(label);
            
            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;
            pathContainer.style.flexGrow = 1;
            
            textField = new TextField();
            textField.style.flexGrow = 1;
            textField.value = initialValue;
            textField.RegisterValueChangedCallback(evt => onPathChanged?.Invoke(evt.newValue));
            pathContainer.Add(textField);
            
            var browseBtn = new Button(() =>
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                var startPath = string.IsNullOrEmpty(initialValue) 
                    ? projectRoot 
                    : Path.GetDirectoryName(Path.IsPathRooted(initialValue) ? initialValue : Path.Combine(projectRoot, initialValue));
                var path = EditorUtility.OpenFilePanel(dialogTitle, startPath, extension);
                if (!string.IsNullOrEmpty(path))
                {
                    var relativePath = GetRelativePath(projectRoot, path);
                    onPathChanged?.Invoke(relativePath);
                }
            }) { text = "..." };
            browseBtn.style.width = 30;
            browseBtn.style.marginLeft = 4;
            pathContainer.Add(browseBtn);
            
            row.Add(pathContainer);
            return row;
        }

        private string GetRelativePath(string projectRoot, string absolutePath)
        {
            projectRoot = projectRoot.Replace('\\', '/').TrimEnd('/');
            absolutePath = absolutePath.Replace('\\', '/');
            
            if (absolutePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
            {
                var relative = absolutePath.Substring(projectRoot.Length).TrimStart('/');
                return string.IsNullOrEmpty(relative) ? "." : relative;
            }
            
            return absolutePath;
        }

        private VisualElement CreateDropdownRow(string labelText, ref DropdownField dropdown, string[] options, string initialValue, Action<string> onChanged)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.marginTop = 8;
            
            var label = new Label(labelText);
            label.style.width = 150;
            row.Add(label);
            
            dropdown = new DropdownField(new List<string>(options), 0);
            dropdown.style.flexGrow = 1;
            dropdown.value = string.IsNullOrEmpty(initialValue) ? options[0] : initialValue;
            dropdown.RegisterValueChangedCallback(evt => onChanged?.Invoke(evt.newValue));
            row.Add(dropdown);
            
            return row;
        }

        #endregion


        #region Luban 生成

        private void GenerateLuban() => ExecuteLuban(false);

        private void ValidateLuban() => ExecuteLuban(true);

        private void ExecuteLuban(bool validateOnly)
        {
            if (!ValidateLubanConfig()) return;
            
            mGenerateBtn.SetEnabled(false);
            mGenerateStatusLabel.text = validateOnly ? "验证中..." : "生成中...";
            mGenerateStatusLabel.style.color = new StyleColor(new Color(0.8f, 0.8f, 0.4f));
            
            var logLabel = mLogContainer.Q<Label>("log-content");
            var logBuilder = new StringBuilder();
            logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] 开始{(validateOnly ? "验证" : "生成")}...");
            
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var workDir = Path.IsPathRooted(mLubanWorkDir) 
                ? mLubanWorkDir 
                : Path.Combine(projectRoot, mLubanWorkDir);
            var dllPath = Path.IsPathRooted(mLubanDllPath) 
                ? mLubanDllPath 
                : Path.Combine(projectRoot, mLubanDllPath);
            
            try
            {
                var args = BuildLubanArgs(validateOnly);
                logBuilder.AppendLine($"命令: dotnet {dllPath}");
                logBuilder.AppendLine($"参数: {args}");
                logBuilder.AppendLine($"工作目录: {workDir}");
                logBuilder.AppendLine("---");
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = $"\"{dllPath}\" {args}",
                    WorkingDirectory = workDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8
                };
                
                using var process = new Process { StartInfo = startInfo };
                var outputBuilder = new StringBuilder();
                var errorBuilder = new StringBuilder();
                
                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) outputBuilder.AppendLine(e.Data);
                };
                
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data)) errorBuilder.AppendLine(e.Data);
                };
                
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();
                
                var exitCode = process.ExitCode;
                logBuilder.AppendLine(outputBuilder.ToString());
                
                if (!string.IsNullOrEmpty(errorBuilder.ToString()))
                {
                    logBuilder.AppendLine("[错误输出]");
                    logBuilder.AppendLine(errorBuilder.ToString());
                }
                
                logBuilder.AppendLine("---");
                logBuilder.AppendLine($"[{DateTime.Now:HH:mm:ss}] 退出码: {exitCode}");
                
                if (exitCode == 0)
                {
                    mGenerateStatusLabel.text = validateOnly ? "验证通过" : "生成成功";
                    mGenerateStatusLabel.style.color = new StyleColor(new Color(0.4f, 0.8f, 0.4f));
                    
                    if (validateOnly)
                    {
                        var tempDataDir = Path.Combine(projectRoot, "Temp/LubanValidate");
                        LoadDataPreview(tempDataDir, logBuilder);
                    }
                    else
                    {
                        EnsureRequiredFiles(logBuilder);
                        AssetDatabase.Refresh();
                        logBuilder.AppendLine("已刷新 Unity 资源数据库");
                    }
                }
                else
                {
                    mGenerateStatusLabel.text = validateOnly ? "验证失败" : "生成失败";
                    mGenerateStatusLabel.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
                }
            }
            catch (Exception ex)
            {
                logBuilder.AppendLine($"[异常] {ex.Message}");
                logBuilder.AppendLine(ex.StackTrace);
                mGenerateStatusLabel.text = "执行异常";
                mGenerateStatusLabel.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
                Debug.LogException(ex);
            }
            finally
            {
                mGenerateBtn.SetEnabled(true);
                logLabel.text = logBuilder.ToString();
            }
        }

        private string BuildLubanArgs(bool validateOnly)
        {
            var sb = new StringBuilder();
            sb.Append($"-t {mTarget} ");
            sb.Append($"-d {(validateOnly ? "json" : mDataTarget)} ");
            sb.Append("--conf luban.conf ");
            
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            
            if (validateOnly)
            {
                var tempDataDir = Path.Combine(projectRoot, "Temp/LubanValidate");
                sb.Append($"-x outputDataDir=\"{tempDataDir}\" ");
            }
            else
            {
                sb.Append($"-c {mCodeTarget} ");
                
                var dataDir = mOutputDataDir.StartsWith("Assets/") 
                    ? Path.Combine(projectRoot, mOutputDataDir.TrimEnd('/')) 
                    : mOutputDataDir;
                var codeDir = mOutputCodeDir.StartsWith("Assets/") 
                    ? Path.Combine(projectRoot, mOutputCodeDir.TrimEnd('/')) 
                    : mOutputCodeDir;
                
                var lubanCodeDir = Path.Combine(codeDir, "Luban");
                
                sb.Append($"-x outputDataDir=\"{dataDir}\" ");
                sb.Append($"-x outputCodeDir=\"{lubanCodeDir}\" ");
            }
            
            return sb.ToString();
        }

        private bool ValidateLubanConfig()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            
            var workDir = Path.IsPathRooted(mLubanWorkDir) 
                ? mLubanWorkDir 
                : Path.Combine(projectRoot, mLubanWorkDir);
            
            if (string.IsNullOrEmpty(mLubanWorkDir) || !Directory.Exists(workDir))
            {
                EditorUtility.DisplayDialog("配置错误", $"Luban 工作目录不存在\n路径: {workDir}", "确定");
                return false;
            }
            
            var confPath = Path.Combine(workDir, "luban.conf");
            if (!File.Exists(confPath))
            {
                EditorUtility.DisplayDialog("配置错误", $"找不到 luban.conf 文件\n路径: {confPath}", "确定");
                return false;
            }
            
            var dllPath = Path.IsPathRooted(mLubanDllPath) 
                ? mLubanDllPath 
                : Path.Combine(projectRoot, mLubanDllPath);
            
            if (string.IsNullOrEmpty(mLubanDllPath) || !File.Exists(dllPath))
            {
                EditorUtility.DisplayDialog("配置错误", $"Luban.dll 路径无效\n路径: {dllPath}", "确定");
                return false;
            }
            
            return true;
        }

        private void OpenLubanFolder()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var workDir = Path.IsPathRooted(mLubanWorkDir) 
                ? mLubanWorkDir 
                : Path.Combine(projectRoot, mLubanWorkDir);
            
            if (!string.IsNullOrEmpty(workDir) && Directory.Exists(workDir))
            {
                EditorUtility.RevealInFinder(workDir);
            }
            else
            {
                EditorUtility.DisplayDialog("提示", $"Luban 工作目录未配置或不存在\n路径: {workDir}", "确定");
            }
        }

        private void EnsureRequiredFiles(StringBuilder logBuilder)
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var codeDir = mOutputCodeDir.StartsWith("Assets/") 
                ? Path.Combine(projectRoot, mOutputCodeDir.TrimEnd('/')) 
                : mOutputCodeDir;
            
            var lubanCodeDir = Path.Combine(codeDir, "Luban");
            if (!Directory.Exists(lubanCodeDir))
            {
                Directory.CreateDirectory(lubanCodeDir);
            }
            
            logBuilder.AppendLine("正在生成 TableKit 运行时代码...");
            TableKitCodeGenerator.Generate(codeDir, mUseAssemblyDefinition, mGenerateExternalTypeUtil, mAssemblyName, "cfg");
            logBuilder.AppendLine("TableKit 运行时代码生成完成");
            
            if (mGenerateExternalTypeUtil)
            {
                logBuilder.AppendLine("已生成 ExternalTypeUtil.cs");
            }
        }

        #endregion


        #region 数据预览

        private void LoadDataPreview(string dataDir, StringBuilder logBuilder)
        {
            mDataPreviewContainer.Clear();
            
            if (!Directory.Exists(dataDir))
            {
                var hint = new Label("验证数据目录不存在");
                hint.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
                hint.style.marginTop = 8;
                mDataPreviewContainer.Add(hint);
                return;
            }
            
            var jsonFiles = Directory.GetFiles(dataDir, "*.json");
            if (jsonFiles.Length == 0)
            {
                var hint = new Label("没有找到 JSON 数据文件");
                hint.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
                hint.style.marginTop = 8;
                mDataPreviewContainer.Add(hint);
                return;
            }
            
            logBuilder.AppendLine($"找到 {jsonFiles.Length} 个数据文件");
            
            var fileNames = new List<string>();
            foreach (var file in jsonFiles)
            {
                fileNames.Add(Path.GetFileNameWithoutExtension(file));
            }
            
            var selectRow = new VisualElement();
            selectRow.style.flexDirection = FlexDirection.Row;
            selectRow.style.alignItems = Align.Center;
            selectRow.style.marginTop = 8;
            mDataPreviewContainer.Add(selectRow);
            
            var selectLabel = new Label("选择配置表:");
            selectLabel.style.width = 80;
            selectRow.Add(selectLabel);
            
            var dropdown = new DropdownField(fileNames, 0);
            dropdown.style.flexGrow = 1;
            selectRow.Add(dropdown);
            
            // 搜索栏
            var searchRow = new VisualElement();
            searchRow.style.flexDirection = FlexDirection.Row;
            searchRow.style.alignItems = Align.Center;
            searchRow.style.marginTop = 8;
            mDataPreviewContainer.Add(searchRow);
            
            var searchLabel = new Label("搜索:");
            searchLabel.style.width = 80;
            searchRow.Add(searchLabel);
            
            var searchField = new TextField();
            searchField.style.flexGrow = 1;
            searchField.RegisterValueChangedCallback(evt =>
            {
                var treeContainer = mDataPreviewContainer.Q<ScrollView>("tree-container");
                if (treeContainer != null) FilterTreeBySearch(treeContainer, evt.newValue);
            });
            searchRow.Add(searchField);
            
            var clearSearchBtn = new Button(() =>
            {
                searchField.value = "";
                var treeContainer = mDataPreviewContainer.Q<ScrollView>("tree-container");
                if (treeContainer != null) FilterTreeBySearch(treeContainer, "");
            }) { text = "清除" };
            clearSearchBtn.style.width = 50;
            clearSearchBtn.style.marginLeft = 4;
            searchRow.Add(clearSearchBtn);
            
            var searchHint = new Label("支持搜索键名或值，匹配项会高亮显示");
            searchHint.style.fontSize = 10;
            searchHint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            searchHint.style.marginLeft = 80;
            searchHint.style.marginTop = 2;
            mDataPreviewContainer.Add(searchHint);
            
            var treeContainer = new ScrollView();
            treeContainer.name = "tree-container";
            treeContainer.style.marginTop = 8;
            treeContainer.style.maxHeight = 400;
            treeContainer.style.backgroundColor = new StyleColor(new Color(0.12f, 0.12f, 0.12f));
            treeContainer.style.borderTopLeftRadius = 4;
            treeContainer.style.borderTopRightRadius = 4;
            treeContainer.style.borderBottomLeftRadius = 4;
            treeContainer.style.borderBottomRightRadius = 4;
            mDataPreviewContainer.Add(treeContainer);
            
            LoadJsonToTree(jsonFiles[0], treeContainer);
            
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = fileNames.IndexOf(evt.newValue);
                if (index >= 0 && index < jsonFiles.Length)
                {
                    searchField.value = "";
                    LoadJsonToTree(jsonFiles[index], treeContainer);
                }
            });
        }

        private void FilterTreeBySearch(VisualElement container, string searchText)
        {
            var isSearching = !string.IsNullOrEmpty(searchText);
            var lowerSearch = searchText?.ToLowerInvariant() ?? "";
            FilterElementRecursive(container, lowerSearch, isSearching);
        }

        private bool FilterElementRecursive(VisualElement element, string searchText, bool isSearching)
        {
            var hasMatch = false;
            
            if (element is Foldout foldout)
            {
                var titleMatch = isSearching && foldout.text.ToLowerInvariant().Contains(searchText);
                
                foreach (var child in foldout.Children())
                {
                    if (FilterElementRecursive(child, searchText, isSearching)) hasMatch = true;
                }
                
                if (titleMatch) hasMatch = true;
                
                foldout.style.display = (!isSearching || hasMatch) ? DisplayStyle.Flex : DisplayStyle.None;
                
                if (isSearching && hasMatch) foldout.value = true;
                
                foldout.style.backgroundColor = titleMatch 
                    ? new StyleColor(new Color(0.3f, 0.4f, 0.3f)) 
                    : StyleKeyword.Null;
            }
            else if (element.childCount > 0)
            {
                var labels = element.Query<Label>().ToList();
                foreach (var label in labels)
                {
                    if (isSearching && label.text.ToLowerInvariant().Contains(searchText))
                    {
                        hasMatch = true;
                        label.style.backgroundColor = new StyleColor(new Color(0.4f, 0.5f, 0.3f));
                    }
                    else
                    {
                        label.style.backgroundColor = StyleKeyword.Null;
                    }
                }
                
                foreach (var child in element.Children())
                {
                    if (FilterElementRecursive(child, searchText, isSearching)) hasMatch = true;
                }
                
                if (element.parent is Foldout)
                {
                    element.style.display = (!isSearching || hasMatch) ? DisplayStyle.Flex : DisplayStyle.None;
                }
            }
            
            return hasMatch;
        }

        private void LoadJsonToTree(string jsonPath, ScrollView container)
        {
            container.Clear();
            
            try
            {
                var jsonText = File.ReadAllText(jsonPath);
                var json = JSON.Parse(jsonText);
                
                if (json == null)
                {
                    var errorLabel = new Label("JSON 解析失败");
                    errorLabel.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
                    errorLabel.style.paddingLeft = 8;
                    errorLabel.style.paddingTop = 4;
                    container.Add(errorLabel);
                    return;
                }
                
                BuildJsonTree(json, container, 0, Path.GetFileNameWithoutExtension(jsonPath));
            }
            catch (Exception ex)
            {
                var errorLabel = new Label($"加载失败: {ex.Message}");
                errorLabel.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
                errorLabel.style.paddingLeft = 8;
                errorLabel.style.paddingTop = 4;
                container.Add(errorLabel);
            }
        }

        private void BuildJsonTree(JSONNode node, VisualElement parent, int depth, string key = null)
        {
            var indent = depth * 16;
            
            if (node.IsArray)
            {
                var foldout = new Foldout();
                foldout.text = string.IsNullOrEmpty(key) ? $"Array [{node.Count}]" : $"{key} [{node.Count}]";
                foldout.value = depth < 1;
                foldout.style.marginLeft = indent;
                parent.Add(foldout);
                
                var index = 0;
                foreach (var item in node.Children)
                {
                    BuildJsonTree(item, foldout, depth + 1, $"[{index}]");
                    index++;
                    
                    if (index >= 100)
                    {
                        var moreLabel = new Label($"... 还有 {node.Count - 100} 项");
                        moreLabel.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
                        moreLabel.style.marginLeft = (depth + 1) * 16;
                        foldout.Add(moreLabel);
                        break;
                    }
                }
            }
            else if (node.IsObject)
            {
                var foldout = new Foldout();
                foldout.text = string.IsNullOrEmpty(key) ? "Object" : key;
                foldout.value = depth < 2;
                foldout.style.marginLeft = indent;
                parent.Add(foldout);
                
                foreach (var kvp in node.AsObject)
                {
                    BuildJsonTree(kvp.Value, foldout, depth + 1, kvp.Key);
                }
            }
            else
            {
                var row = new VisualElement();
                row.style.flexDirection = FlexDirection.Row;
                row.style.marginLeft = indent;
                row.style.paddingTop = 2;
                row.style.paddingBottom = 2;
                parent.Add(row);
                
                if (!string.IsNullOrEmpty(key))
                {
                    var keyLabel = new Label($"{key}: ");
                    keyLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 1f));
                    row.Add(keyLabel);
                }
                
                var valueLabel = new Label(node.Value);
                valueLabel.style.color = GetValueColor(node);
                row.Add(valueLabel);
            }
        }

        private StyleColor GetValueColor(JSONNode node)
        {
            if (node.IsNumber) return new StyleColor(new Color(0.7f, 0.9f, 0.7f));
            if (node.IsBoolean) return new StyleColor(new Color(0.9f, 0.7f, 0.7f));
            if (node.IsNull) return new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            return new StyleColor(new Color(0.9f, 0.8f, 0.6f));
        }

        #endregion


        #region TableKit 操作

        private void RefreshEditorCache()
        {
            var tablesType = FindTablesType();
            if (tablesType == null)
            {
                EditorUtility.DisplayDialog("TableKit", "cfg.Tables 类型不存在，请先生成配置表代码", "确定");
                RefreshStatus();
                return;
            }
            
            try
            {
                var tableKitType = FindTableKitType();
                if (tableKitType == null)
                {
                    EditorUtility.DisplayDialog("TableKit", "TableKit 类型不存在，请先生成配置表代码", "确定");
                    return;
                }
                
                var setPathMethod = tableKitType.GetMethod("SetEditorDataPath");
                setPathMethod?.Invoke(null, new object[] { mEditorDataPath });
                
                var refreshMethod = tableKitType.GetMethod("RefreshEditor");
                refreshMethod?.Invoke(null, null);
                
                var tablesEditorProp = tableKitType.GetProperty("TablesEditor");
                var tables = tablesEditorProp?.GetValue(null);
                
                RefreshTablesInfo(tables);
                EditorUtility.DisplayDialog("TableKit", "编辑器缓存已刷新", "确定");
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("TableKit", $"加载配置表失败:\n{ex.Message}", "确定");
            }
            
            RefreshStatus();
        }

        private void ClearAll()
        {
            try
            {
                var tableKitType = FindTableKitType();
                if (tableKitType != null)
                {
                    var clearMethod = tableKitType.GetMethod("Clear");
                    clearMethod?.Invoke(null, null);
                }
            }
            catch
            {
                // 忽略清理错误
            }
            
            EditorUtility.DisplayDialog("TableKit", "已清理所有资源", "确定");
            RefreshStatus();
            
            mTablesInfoContainer.Clear();
            var hint = new Label("点击「刷新编辑器缓存」加载配置表信息");
            hint.style.color = new StyleColor(new Color(0.5f, 0.5f, 0.5f));
            hint.style.marginTop = 8;
            mTablesInfoContainer.Add(hint);
        }

        public void RefreshStatus()
        {
            var tableKitType = FindTableKitType();
            
            if (tableKitType != null)
            {
                var initializedProp = tableKitType.GetProperty("Initialized");
                var initialized = initializedProp != null && (bool)initializedProp.GetValue(null);
                
                if (initialized)
                {
                    mStatusLabel.text = "已初始化";
                    mStatusLabel.style.color = new StyleColor(new Color(0.4f, 0.8f, 0.4f));
                }
                else
                {
                    mStatusLabel.text = "未初始化";
                    mStatusLabel.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
                }
            }
            else
            {
                mStatusLabel.text = "未生成代码";
                mStatusLabel.style.color = new StyleColor(new Color(0.8f, 0.6f, 0.4f));
            }
            
            var tablesType = FindTablesType();
            if (tablesType == null)
            {
                mLoadModeLabel.text = "未生成代码";
                mLoadModeLabel.style.color = new StyleColor(new Color(0.8f, 0.6f, 0.4f));
                return;
            }
            
            try
            {
                var ctor = tablesType.GetConstructors()[0];
                var loaderParam = ctor.GetParameters()[0];
                var loaderReturnType = loaderParam.ParameterType.GetGenericArguments()[1];
                
                mLoadModeLabel.text = loaderReturnType.Name.Contains("ByteBuf") 
                    ? "Binary (二进制)" 
                    : "JSON";
                mLoadModeLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 0.6f));
            }
            catch
            {
                mLoadModeLabel.text = "未知";
            }
        }
        
        private Type FindTablesType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("cfg.Tables");
                if (type != null) return type;
            }
            return null;
        }
        
        private Type FindTableKitType()
        {
            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = assembly.GetType("TableKit");
                if (type != null) return type;
            }
            return null;
        }

        private void RefreshTablesInfo(object tables)
        {
            mTablesInfoContainer.Clear();
            
            if (tables == null)
            {
                var hint = new Label("配置表未加载");
                hint.style.color = new StyleColor(new Color(0.8f, 0.4f, 0.4f));
                hint.style.marginTop = 8;
                mTablesInfoContainer.Add(hint);
                return;
            }
            
            var tableType = tables.GetType();
            var properties = tableType.GetProperties();
            
            foreach (var prop in properties)
            {
                if (prop.PropertyType.Namespace == "cfg")
                {
                    var row = new VisualElement();
                    row.style.flexDirection = FlexDirection.Row;
                    row.style.alignItems = Align.Center;
                    row.style.marginTop = 4;
                    
                    var nameLabel = new Label($"• {prop.Name}");
                    nameLabel.style.width = 150;
                    row.Add(nameLabel);
                    
                    var typeLabel = new Label(prop.PropertyType.Name);
                    typeLabel.style.color = new StyleColor(new Color(0.6f, 0.8f, 0.6f));
                    row.Add(typeLabel);
                    
                    mTablesInfoContainer.Add(row);
                }
            }
        }

        #endregion

        #region 配置持久化

        private void LoadPrefs()
        {
            mEditorDataPath = EditorPrefs.GetString(PREF_EDITOR_DATA_PATH, "Assets/Art/Table/");
            mRuntimePathPattern = EditorPrefs.GetString(PREF_RUNTIME_PATH_PATTERN, "{0}");
            mLubanWorkDir = EditorPrefs.GetString(PREF_LUBAN_WORK_DIR, "Luban/MiniTemplate");
            mLubanDllPath = EditorPrefs.GetString(PREF_LUBAN_DLL_PATH, "Luban/Tools/Luban/Luban.dll");
            mTarget = EditorPrefs.GetString(PREF_TARGET, "client");
            mCodeTarget = EditorPrefs.GetString(PREF_CODE_TARGET, "cs-bin");
            mDataTarget = EditorPrefs.GetString(PREF_DATA_TARGET, "bin");
            mOutputDataDir = EditorPrefs.GetString(PREF_OUTPUT_DATA_DIR, "Assets/Art/Table/");
            mOutputCodeDir = EditorPrefs.GetString(PREF_OUTPUT_CODE_DIR, "Assets/Scripts/TabCode/");
            mUseAssemblyDefinition = EditorPrefs.GetBool(PREF_USE_ASSEMBLY, false);
            mAssemblyName = EditorPrefs.GetString(PREF_ASSEMBLY_NAME, "YokiFrame.TableKit");
            mGenerateExternalTypeUtil = EditorPrefs.GetBool(PREF_GENERATE_EXTERNAL_TYPE_UTIL, false);
        }

        public void SavePrefs()
        {
            EditorPrefs.SetString(PREF_EDITOR_DATA_PATH, mEditorDataPath);
            EditorPrefs.SetString(PREF_RUNTIME_PATH_PATTERN, mRuntimePathPattern);
            EditorPrefs.SetString(PREF_LUBAN_WORK_DIR, mLubanWorkDir);
            EditorPrefs.SetString(PREF_LUBAN_DLL_PATH, mLubanDllPath);
            EditorPrefs.SetString(PREF_TARGET, mTarget);
            EditorPrefs.SetString(PREF_CODE_TARGET, mCodeTarget);
            EditorPrefs.SetString(PREF_DATA_TARGET, mDataTarget);
            EditorPrefs.SetString(PREF_OUTPUT_DATA_DIR, mOutputDataDir);
            EditorPrefs.SetString(PREF_OUTPUT_CODE_DIR, mOutputCodeDir);
            EditorPrefs.SetBool(PREF_USE_ASSEMBLY, mUseAssemblyDefinition);
            EditorPrefs.SetString(PREF_ASSEMBLY_NAME, mAssemblyName);
            EditorPrefs.SetBool(PREF_GENERATE_EXTERNAL_TYPE_UTIL, mGenerateExternalTypeUtil);
        }

        #endregion
    }
}
#endif
