#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - UI 区块构建
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region A. 命令中心

        private VisualElement BuildCommandCenter()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.paddingLeft = 12;
            container.style.paddingRight = 12;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;
            container.style.marginBottom = 12;

            // 标题行
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 12;
            container.Add(titleRow);

            var title = new Label("TableKit 配置表生成");
            title.style.fontSize = Design.FontSizeTitle;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            titleRow.Add(title);

            // 主内容行
            var mainRow = new VisualElement();
            mainRow.style.flexDirection = FlexDirection.Row;
            mainRow.style.alignItems = Align.Center;
            mainRow.style.justifyContent = Justify.SpaceBetween;
            container.Add(mainRow);

            // 左侧下拉
            mainRow.Add(BuildCommandDropdowns());
            // 右侧按钮
            mainRow.Add(BuildCommandButtons());

            return container;
        }

        private VisualElement BuildCommandDropdowns()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // Target
            var targetLabel = new Label("Target:");
            targetLabel.style.color = new StyleColor(Design.TextSecondary);
            targetLabel.style.marginRight = 4;
            container.Add(targetLabel);

            mTargetDropdown = new DropdownField(new List<string>(TARGET_OPTIONS), 0);
            mTargetDropdown.style.width = 80;
            mTargetDropdown.value = string.IsNullOrEmpty(mTarget) ? TARGET_OPTIONS[0] : mTarget;
            mTargetDropdown.RegisterValueChangedCallback(evt => { mTarget = evt.newValue; SavePrefs(); });
            container.Add(mTargetDropdown);

            var spacer = new VisualElement { style = { width = 16 } };
            container.Add(spacer);

            // Code Target
            var codeLabel = new Label("Code:");
            codeLabel.style.color = new StyleColor(Design.TextSecondary);
            codeLabel.style.marginRight = 4;
            container.Add(codeLabel);

            mCodeTargetDropdown = new DropdownField(new List<string>(CODE_TARGET_OPTIONS), 0);
            mCodeTargetDropdown.style.width = 140;
            mCodeTargetDropdown.value = string.IsNullOrEmpty(mCodeTarget) ? CODE_TARGET_OPTIONS[0] : mCodeTarget;
            mCodeTargetDropdown.RegisterValueChangedCallback(evt => { mCodeTarget = evt.newValue; SavePrefs(); });
            container.Add(mCodeTargetDropdown);

            return container;
        }

        private VisualElement BuildCommandButtons()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;

            // 还原默认设置按钮
            var resetBtn = new Button(ResetToDefaults) { text = "还原默认" };
            ApplySecondaryButtonStyle(resetBtn);
            resetBtn.tooltip = "还原所有配置为默认值";
            container.Add(resetBtn);

            // 验证按钮
            var validateBtn = new Button(ValidateLuban) { text = "验证" };
            validateBtn.style.marginLeft = 4;
            ApplySecondaryButtonStyle(validateBtn);
            container.Add(validateBtn);

            // 打开目录
            var openBtn = new Button(OpenLubanFolder) { text = "..." };
            openBtn.style.width = 28;
            openBtn.style.height = 28;
            openBtn.style.marginLeft = 4;
            ApplySecondaryButtonStyle(openBtn);
            openBtn.tooltip = "打开 Luban 工作目录";
            container.Add(openBtn);

            // 生成按钮
            mGenerateBtn = new Button(GenerateLuban) { text = "生成配置表" };
            mGenerateBtn.style.height = 28;
            mGenerateBtn.style.paddingLeft = 16;
            mGenerateBtn.style.paddingRight = 16;
            mGenerateBtn.style.marginLeft = 8;
            mGenerateBtn.style.backgroundColor = new StyleColor(Design.BrandPrimary);
            mGenerateBtn.style.color = new StyleColor(Color.white);
            mGenerateBtn.style.borderTopLeftRadius = mGenerateBtn.style.borderTopRightRadius = 4;
            mGenerateBtn.style.borderBottomLeftRadius = mGenerateBtn.style.borderBottomRightRadius = 4;
            container.Add(mGenerateBtn);

            return container;
        }

        #endregion

        #region B. 可折叠配置区

        private VisualElement BuildConfigFoldout()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.marginBottom = 12;

            // 折叠头部
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.cursor = StyleKeyword.Initial;
            container.Add(header);

            var arrow = new Label("▶") { name = "foldout-arrow" };
            arrow.style.fontSize = Design.FontSizeSmall;
            arrow.style.color = new StyleColor(Design.TextTertiary);
            arrow.style.marginRight = 6;
            header.Add(arrow);

            var title = new Label("环境与路径配置");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            title.style.flexGrow = 1;
            header.Add(title);

            // 状态点
            mConfigStatusDot = new VisualElement();
            mConfigStatusDot.style.width = 8;
            mConfigStatusDot.style.height = 8;
            mConfigStatusDot.style.borderTopLeftRadius = mConfigStatusDot.style.borderTopRightRadius = 4;
            mConfigStatusDot.style.borderBottomLeftRadius = mConfigStatusDot.style.borderBottomRightRadius = 4;
            mConfigStatusDot.style.backgroundColor = new StyleColor(Design.BrandSuccess);
            header.Add(mConfigStatusDot);

            // 折叠内容
            bool isExpanded = EditorPrefs.GetBool(PREF_CONFIG_EXPANDED, false);
            mConfigFoldout = new VisualElement();
            mConfigFoldout.style.paddingLeft = 12;
            mConfigFoldout.style.paddingRight = 12;
            mConfigFoldout.style.paddingBottom = 12;
            mConfigFoldout.style.borderTopWidth = 1;
            mConfigFoldout.style.borderTopColor = new StyleColor(Design.BorderDefault);
            mConfigFoldout.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(mConfigFoldout);

            arrow.text = isExpanded ? "▼" : "▶";

            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool expanded = mConfigFoldout.style.display == DisplayStyle.Flex;
                mConfigFoldout.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                arrow.text = expanded ? "▶" : "▼";
                EditorPrefs.SetBool(PREF_CONFIG_EXPANDED, !expanded);
            });

            BuildConfigContent(mConfigFoldout);
            return container;
        }

        private void BuildConfigContent(VisualElement container)
        {
            // 警告 Callout
            var warning = CreateCallout("Luban 工具不应放置在 Assets 内部，推荐放置在与 Assets 同级目录", Design.BrandWarning);
            warning.style.marginTop = 12;
            container.Add(warning);

            // Luban 环境
            var lubanSection = CreateSubSection("Luban 环境");
            container.Add(lubanSection);

            lubanSection.Add(CreateValidatedPathRow("工作目录:", ref mLubanWorkDirField, mLubanWorkDir, path =>
            {
                mLubanWorkDir = path;
                mLubanWorkDirField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, true, "选择包含 luban.conf 的目录"));

            // 工作目录说明
            var workDirHint = new Label("包含 Datas、Defines、luban.conf 的文件夹");
            workDirHint.style.fontSize = Design.FontSizeSmall;
            workDirHint.style.color = new StyleColor(Design.TextTertiary);
            workDirHint.style.marginTop = 2;
            workDirHint.style.marginLeft = 100;
            lubanSection.Add(workDirHint);

            lubanSection.Add(CreateValidatedFileRow("Luban.dll:", ref mLubanDllPathField, mLubanDllPath, path =>
            {
                mLubanDllPath = path;
                mLubanDllPathField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, "dll", "选择 Luban.dll"));

            // Luban.dll 说明
            var dllHint = new Label("Luban 代码生成工具的 DLL 路径");
            dllHint.style.fontSize = Design.FontSizeSmall;
            dllHint.style.color = new StyleColor(Design.TextTertiary);
            dllHint.style.marginTop = 2;
            dllHint.style.marginLeft = 100;
            lubanSection.Add(dllHint);

            // 输出路径
            var outputSection = CreateSubSection("输出路径");
            container.Add(outputSection);

            // Data Target
            var dataRow = new VisualElement();
            dataRow.style.flexDirection = FlexDirection.Row;
            dataRow.style.alignItems = Align.Center;
            dataRow.style.marginTop = 8;
            outputSection.Add(dataRow);

            var dataLabel = new Label("数据格式:");
            dataLabel.style.width = 100;
            dataLabel.style.color = new StyleColor(Design.TextSecondary);
            dataRow.Add(dataLabel);

            mDataTargetDropdown = new DropdownField(new List<string>(DATA_TARGET_OPTIONS), 0);
            mDataTargetDropdown.style.flexGrow = 1;
            mDataTargetDropdown.value = string.IsNullOrEmpty(mDataTarget) ? DATA_TARGET_OPTIONS[0] : mDataTarget;
            mDataTargetDropdown.RegisterValueChangedCallback(evt => { mDataTarget = evt.newValue; SavePrefs(); });
            dataRow.Add(mDataTargetDropdown);

            outputSection.Add(CreateValidatedPathRow("数据输出:", ref mOutputDataDirField, mOutputDataDir, path =>
            {
                mOutputDataDir = path;
                mOutputDataDirField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, false, "选择数据输出目录"));

            // 数据输出说明
            var dataOutputHint = new Label("生成的配置数据文件存放路径，默认 Assets/Resources/Art/Table/");
            dataOutputHint.style.fontSize = Design.FontSizeSmall;
            dataOutputHint.style.color = new StyleColor(Design.TextTertiary);
            dataOutputHint.style.marginTop = 2;
            dataOutputHint.style.marginLeft = 100;
            outputSection.Add(dataOutputHint);

            outputSection.Add(CreateValidatedPathRow("代码输出:", ref mOutputCodeDirField, mOutputCodeDir, path =>
            {
                mOutputCodeDir = path;
                mOutputCodeDirField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, false, "选择代码输出目录"));

            // 代码输出说明
            var codeOutputHint = new Label("生成的 C# 配置表代码存放路径");
            codeOutputHint.style.fontSize = Design.FontSizeSmall;
            codeOutputHint.style.color = new StyleColor(Design.TextTertiary);
            codeOutputHint.style.marginTop = 2;
            codeOutputHint.style.marginLeft = 100;
            outputSection.Add(codeOutputHint);

            // TableKit 路径
            var tkSection = CreateSubSection("TableKit 路径");
            container.Add(tkSection);

            tkSection.Add(CreateValidatedPathRow("编辑器数据:", ref mEditorDataPathField, mEditorDataPath, path =>
            {
                mEditorDataPath = path;
                mEditorDataPathField.value = path;
                SavePrefs();
            }, false, "选择编辑器数据路径"));

            // 编辑器数据说明
            var editorDataHint = new Label("TableKit.TablesEditor 编辑器访问用的表数据路径");
            editorDataHint.style.fontSize = Design.FontSizeSmall;
            editorDataHint.style.color = new StyleColor(Design.TextTertiary);
            editorDataHint.style.marginTop = 2;
            editorDataHint.style.marginLeft = 100;
            tkSection.Add(editorDataHint);

            var runtimeRow = new VisualElement();
            runtimeRow.style.flexDirection = FlexDirection.Row;
            runtimeRow.style.alignItems = Align.Center;
            runtimeRow.style.marginTop = 8;
            tkSection.Add(runtimeRow);

            var runtimeLabel = new Label("运行时模式:");
            runtimeLabel.style.width = 100;
            runtimeLabel.style.color = new StyleColor(Design.TextSecondary);
            runtimeRow.Add(runtimeLabel);

            mRuntimePathPatternField = new TextField();
            mRuntimePathPatternField.style.flexGrow = 1;
            mRuntimePathPatternField.value = mRuntimePathPattern;
            mRuntimePathPatternField.RegisterValueChangedCallback(evt => { mRuntimePathPattern = evt.newValue; SavePrefs(); });
            runtimeRow.Add(mRuntimePathPatternField);

            var hint = new Label("{0} 为文件名占位符 • 可寻址模式填 {0} • 完整路径填 Assets/Art/Table/{0}");
            hint.style.fontSize = Design.FontSizeSmall;
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 4;
            hint.style.marginLeft = 100;
            tkSection.Add(hint);
        }

        #endregion

        #region C. 构建选项区

        private VisualElement BuildBuildOptions()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.paddingLeft = 12;
            container.style.paddingRight = 12;
            container.style.paddingTop = 12;
            container.style.paddingBottom = 12;
            container.style.marginBottom = 12;

            var title = new Label("构建选项");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            title.style.marginBottom = 12;
            container.Add(title);

            // Toggle 组
            var toggleGroup = new VisualElement();
            container.Add(toggleGroup);

            // 第一行：使用独立程序集开关 + 程序集名称
            var asmRow = new VisualElement();
            asmRow.style.flexDirection = FlexDirection.Row;
            asmRow.style.alignItems = Align.Center;
            asmRow.style.marginBottom = 4;
            toggleGroup.Add(asmRow);

            mUseAssemblyToggle = CreateCapsuleToggle("使用独立程序集", mUseAssemblyDefinition, v =>
            {
                mUseAssemblyDefinition = v;
                mAssemblyNameField?.SetEnabled(v);
                SavePrefs();
            });
            asmRow.Add(mUseAssemblyToggle);

            var asmLabel = new Label("程序集名称:");
            asmLabel.style.marginLeft = 16;
            asmLabel.style.color = new StyleColor(Design.TextSecondary);
            asmRow.Add(asmLabel);

            mAssemblyNameField = new TextField();
            mAssemblyNameField.style.width = 150;
            mAssemblyNameField.style.marginLeft = 4;
            mAssemblyNameField.value = mAssemblyName;
            mAssemblyNameField.SetEnabled(mUseAssemblyDefinition);
            mAssemblyNameField.RegisterValueChangedCallback(evt => { mAssemblyName = evt.newValue; SavePrefs(); });
            asmRow.Add(mAssemblyNameField);

            // 独立程序集说明
            var asmHint = new Label("打开后生成的代码会放入独立程序集 (asmdef)");
            asmHint.style.fontSize = Design.FontSizeSmall;
            asmHint.style.color = new StyleColor(Design.TextTertiary);
            asmHint.style.marginBottom = 8;
            toggleGroup.Add(asmHint);

            // 第二行：生成 ExternalTypeUtil 开关
            var extRow = new VisualElement();
            extRow.style.flexDirection = FlexDirection.Row;
            extRow.style.alignItems = Align.Center;
            extRow.style.marginBottom = 4;
            toggleGroup.Add(extRow);

            mGenerateExternalTypeUtilToggle = CreateCapsuleToggle("生成 ExternalTypeUtil", mGenerateExternalTypeUtil, v =>
            {
                mGenerateExternalTypeUtil = v;
                SavePrefs();
            });
            extRow.Add(mGenerateExternalTypeUtilToggle);

            // ExternalTypeUtil 说明
            var extHint = new Label("Luban vector 转 Unity Vector，如有需要可自行添加代码，不会重复生成覆盖");
            extHint.style.fontSize = Design.FontSizeSmall;
            extHint.style.color = new StyleColor(Design.TextTertiary);
            extHint.style.marginBottom = 4;
            toggleGroup.Add(extHint);

            var extHint2 = new Label("注意：TableKit.cs 会被重复生成覆盖，请勿在其中添加自定义代码");
            extHint2.style.fontSize = Design.FontSizeSmall;
            extHint2.style.color = new StyleColor(Design.BrandWarning);
            toggleGroup.Add(extHint2);

            return container;
        }

        #endregion

        #region D. 控制台

        private VisualElement BuildConsole()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.marginBottom = 12;

            // 标题栏
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.Add(header);

            var title = new Label("控制台");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            header.Add(title);

            var clearBtn = new Button(ClearLog) { text = "清除" };
            ApplySmallButtonStyle(clearBtn);
            header.Add(clearBtn);

            // 状态横幅
            mStatusBanner = new VisualElement();
            mStatusBanner.style.flexDirection = FlexDirection.Row;
            mStatusBanner.style.alignItems = Align.Center;
            mStatusBanner.style.paddingLeft = 12;
            mStatusBanner.style.paddingRight = 12;
            mStatusBanner.style.paddingTop = 6;
            mStatusBanner.style.paddingBottom = 6;
            mStatusBanner.style.backgroundColor = new StyleColor(Design.LayerElevated);
            container.Add(mStatusBanner);

            var statusIcon = new Label("●") { name = "status-icon" };
            statusIcon.style.marginRight = 6;
            statusIcon.style.color = new StyleColor(Design.BrandSuccess);
            mStatusBanner.Add(statusIcon);

            mStatusBannerLabel = new Label("就绪");
            mStatusBannerLabel.style.color = new StyleColor(Design.TextPrimary);
            mStatusBannerLabel.style.fontSize = Design.FontSizeBody;
            mStatusBanner.Add(mStatusBannerLabel);

            UpdateStatusBanner(BuildStatus.Ready);

            // 日志区
            mLogContainer = new ScrollView();
            mLogContainer.style.flexGrow = 1;
            mLogContainer.style.minHeight = 120;
            mLogContainer.style.maxHeight = 200;
            mLogContainer.style.backgroundColor = new StyleColor(Design.LayerConsole);
            mLogContainer.style.paddingLeft = 12;
            mLogContainer.style.paddingRight = 12;
            mLogContainer.style.paddingTop = 8;
            mLogContainer.style.paddingBottom = 8;
            container.Add(mLogContainer);

            mLogContent = new Label("等待操作...");
            mLogContent.style.fontSize = Design.FontSizeSmall;
            mLogContent.style.color = new StyleColor(Design.TextSecondary);
            mLogContent.style.whiteSpace = WhiteSpace.Normal;
            mLogContainer.Add(mLogContent);

            return container;
        }

        private void UpdateStatusBanner(BuildStatus status)
        {
            mCurrentStatus = status;
            var icon = mStatusBanner?.Q<Label>("status-icon");

            switch (status)
            {
                case BuildStatus.Ready:
                    mStatusBannerLabel.text = "就绪";
                    mStatusBanner.style.backgroundColor = new StyleColor(Design.LayerElevated);
                    if (icon != null) icon.style.color = new StyleColor(Design.BrandSuccess);
                    break;
                case BuildStatus.Building:
                    mStatusBannerLabel.text = "生成中...";
                    mStatusBanner.style.backgroundColor = new StyleColor(new Color(0.2f, 0.25f, 0.3f));
                    if (icon != null) icon.style.color = new StyleColor(Design.BrandPrimary);
                    break;
                case BuildStatus.Success:
                    mStatusBannerLabel.text = "生成成功";
                    mStatusBanner.style.backgroundColor = new StyleColor(new Color(0.15f, 0.25f, 0.15f));
                    if (icon != null) icon.style.color = new StyleColor(Design.BrandSuccess);
                    break;
                case BuildStatus.Failed:
                    mStatusBannerLabel.text = "生成失败";
                    mStatusBanner.style.backgroundColor = new StyleColor(new Color(0.3f, 0.15f, 0.15f));
                    if (icon != null) icon.style.color = new StyleColor(Design.BrandDanger);
                    break;
            }
        }

        private void ClearLog()
        {
            mLogContent.text = "日志已清除";
            UpdateStatusBanner(BuildStatus.Ready);
        }

        #endregion

        #region E. 数据预览区

        private TextField mDataPreviewSearchField;
        private string mDataPreviewSearchText = "";
        private ScrollView mDataPreviewTreeContainer;
        private string mCurrentPreviewJsonPath;
        private Label mDataPreviewMatchLabel;

        private VisualElement BuildDataPreview()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.marginBottom = 12;

            // 标题栏
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.Add(header);

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            header.Add(titleRow);

            var title = new Label("数据预览");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            titleRow.Add(title);

            // 匹配数量标签
            mDataPreviewMatchLabel = new Label();
            mDataPreviewMatchLabel.style.marginLeft = 8;
            mDataPreviewMatchLabel.style.fontSize = Design.FontSizeSmall;
            mDataPreviewMatchLabel.style.color = new StyleColor(Design.TextTertiary);
            mDataPreviewMatchLabel.style.display = DisplayStyle.None;
            titleRow.Add(mDataPreviewMatchLabel);

            // 搜索框
            var searchContainer = new VisualElement();
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.alignItems = Align.Center;
            header.Add(searchContainer);

            var searchIcon = new Label("[搜索]");
            searchIcon.style.marginRight = 4;
            searchIcon.style.fontSize = Design.FontSizeSmall;
            searchIcon.style.color = new StyleColor(Design.TextTertiary);
            searchContainer.Add(searchIcon);

            mDataPreviewSearchField = new TextField();
            mDataPreviewSearchField.style.width = 150;
            mDataPreviewSearchField.style.height = 22;
            var placeholder = "搜索键/值...";
            mDataPreviewSearchField.value = placeholder;
            mDataPreviewSearchField.style.color = new StyleColor(Design.TextTertiary);

            mDataPreviewSearchField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (mDataPreviewSearchField.value == placeholder)
                {
                    mDataPreviewSearchField.value = "";
                    mDataPreviewSearchField.style.color = new StyleColor(Design.TextPrimary);
                }
            });

            mDataPreviewSearchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrEmpty(mDataPreviewSearchField.value))
                {
                    mDataPreviewSearchField.value = placeholder;
                    mDataPreviewSearchField.style.color = new StyleColor(Design.TextTertiary);
                }
            });

            mDataPreviewSearchField.RegisterValueChangedCallback(evt =>
            {
                var newValue = evt.newValue;
                if (newValue == placeholder) newValue = "";

                mDataPreviewSearchText = newValue;
                RefreshDataPreviewWithSearch();
            });
            searchContainer.Add(mDataPreviewSearchField);

            mDataPreviewContainer = new VisualElement();
            mDataPreviewContainer.style.paddingLeft = 12;
            mDataPreviewContainer.style.paddingRight = 12;
            mDataPreviewContainer.style.paddingBottom = 12;
            container.Add(mDataPreviewContainer);

            var hint = new Label("点击「验证」后显示数据预览");
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 8;
            mDataPreviewContainer.Add(hint);

            return container;
        }

        #endregion

        #region F. 配置表信息区

        private TextField mTablesSearchField;
        private string mTablesSearchText = "";
        private object mCachedTables;

        private VisualElement BuildTablesInfo()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.marginBottom = 16;

            // 标题栏
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.Add(header);

            var title = new Label("配置表信息");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            header.Add(title);

            // 右侧操作区：刷新缓存按钮 + 搜索框
            var rightContainer = new VisualElement();
            rightContainer.style.flexDirection = FlexDirection.Row;
            rightContainer.style.alignItems = Align.Center;
            header.Add(rightContainer);

            // 刷新缓存按钮
            var refreshBtn = new Button(RefreshEditorCache) { text = "刷新缓存" };
            ApplySmallButtonStyle(refreshBtn);
            refreshBtn.style.marginRight = 8;
            rightContainer.Add(refreshBtn);

            // 搜索框
            var searchContainer = new VisualElement();
            searchContainer.style.flexDirection = FlexDirection.Row;
            searchContainer.style.alignItems = Align.Center;
            rightContainer.Add(searchContainer);

            var searchIcon = new Label("[搜索]");
            searchIcon.style.marginRight = 4;
            searchIcon.style.fontSize = Design.FontSizeSmall;
            searchIcon.style.color = new StyleColor(Design.TextTertiary);
            searchContainer.Add(searchIcon);

            mTablesSearchField = new TextField();
            mTablesSearchField.style.width = 150;
            mTablesSearchField.style.height = 22;
            mTablesSearchField.value = "";
            // 设置占位符样式
            var placeholder = "搜索表名...";
            mTablesSearchField.value = placeholder;
            mTablesSearchField.style.color = new StyleColor(Design.TextTertiary);
            
            mTablesSearchField.RegisterCallback<FocusInEvent>(_ =>
            {
                if (mTablesSearchField.value == placeholder)
                {
                    mTablesSearchField.value = "";
                    mTablesSearchField.style.color = new StyleColor(Design.TextPrimary);
                }
            });
            
            mTablesSearchField.RegisterCallback<FocusOutEvent>(_ =>
            {
                if (string.IsNullOrEmpty(mTablesSearchField.value))
                {
                    mTablesSearchField.value = placeholder;
                    mTablesSearchField.style.color = new StyleColor(Design.TextTertiary);
                }
            });
            
            mTablesSearchField.RegisterValueChangedCallback(evt =>
            {
                var newValue = evt.newValue;
                if (newValue == placeholder) newValue = "";
                
                mTablesSearchText = newValue;
                FilterTablesInfo();
            });
            searchContainer.Add(mTablesSearchField);

            mTablesInfoContainer = new VisualElement();
            mTablesInfoContainer.style.paddingLeft = 12;
            mTablesInfoContainer.style.paddingRight = 12;
            mTablesInfoContainer.style.paddingBottom = 12;
            mTablesInfoContainer.style.maxHeight = 300;
            container.Add(mTablesInfoContainer);

            // 使用 ScrollView 包裹内容
            var scrollView = new ScrollView();
            scrollView.style.flexGrow = 1;
            mTablesInfoContainer.Add(scrollView);

            var hint = new Label("点击「刷新缓存」加载配置表信息");
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 8;
            scrollView.Add(hint);

            return container;
        }

        /// <summary>
        /// 根据搜索文本过滤配置表信息
        /// </summary>
        private void FilterTablesInfo()
        {
            if (mCachedTables == null) return;
            RefreshTablesInfoInternal(mCachedTables, mTablesSearchText);
        }

        #endregion

        #region G. 使用指南区（可折叠）

        private VisualElement mGuideFoldout;

        /// <summary>
        /// 构建使用指南区块（可折叠）
        /// </summary>
        private VisualElement BuildUsageGuide()
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerCard);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 8;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 8;
            container.style.borderLeftWidth = container.style.borderRightWidth = 1;
            container.style.borderTopWidth = container.style.borderBottomWidth = 1;
            container.style.borderLeftColor = container.style.borderRightColor = new StyleColor(Design.BorderDefault);
            container.style.borderTopColor = container.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.style.marginBottom = 16;

            // 折叠头部
            var header = new VisualElement();
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.paddingLeft = 12;
            header.style.paddingRight = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.cursor = StyleKeyword.Initial;
            container.Add(header);

            var arrow = new Label("▶") { name = "guide-foldout-arrow" };
            arrow.style.fontSize = Design.FontSizeSmall;
            arrow.style.color = new StyleColor(Design.TextTertiary);
            arrow.style.marginRight = 6;
            header.Add(arrow);

            var title = new Label("使用指南");
            title.style.fontSize = Design.FontSizeSection;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            title.style.flexGrow = 1;
            header.Add(title);

            // 折叠内容
            bool isExpanded = EditorPrefs.GetBool(PREF_GUIDE_EXPANDED, false);
            mGuideFoldout = new VisualElement();
            mGuideFoldout.style.paddingLeft = 12;
            mGuideFoldout.style.paddingRight = 12;
            mGuideFoldout.style.paddingBottom = 12;
            mGuideFoldout.style.borderTopWidth = 1;
            mGuideFoldout.style.borderTopColor = new StyleColor(Design.BorderDefault);
            mGuideFoldout.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(mGuideFoldout);

            arrow.text = isExpanded ? "▼" : "▶";

            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool expanded = mGuideFoldout.style.display == DisplayStyle.Flex;
                mGuideFoldout.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                arrow.text = expanded ? "▶" : "▼";
                EditorPrefs.SetBool(PREF_GUIDE_EXPANDED, !expanded);
            });

            BuildGuideContent(mGuideFoldout);
            return container;
        }

        /// <summary>
        /// 构建使用指南内容
        /// </summary>
        private void BuildGuideContent(VisualElement container)
        {
            // 基础用法
            var basicSection = CreateGuideSection("基础用法 (Resources 加载)");
            basicSection.style.marginTop = 12;
            container.Add(basicSection);

            var basicDesc = new Label("TableKit 默认使用 Resources.Load 加载配置数据，无需额外配置：");
            basicDesc.style.color = new StyleColor(Design.TextSecondary);
            basicDesc.style.fontSize = Design.FontSizeBody;
            basicDesc.style.marginBottom = 8;
            basicDesc.style.whiteSpace = WhiteSpace.Normal;
            basicSection.Add(basicDesc);

            var basicCode = CreateCodeBlock(
@"// 运行时访问配置表
var tables = TableKit.Tables;
var itemConfig = tables.TbItem.Get(1001);

// 编辑器访问配置表
#if UNITY_EDITOR
var editorTables = TableKit.TablesEditor;
#endif");
            basicSection.Add(basicCode);

            // 自定义加载器
            var customSection = CreateGuideSection("自定义加载器");
            container.Add(customSection);

            var customDesc = new Label("如需使用 Addressables 或 YooAsset 等资源管理方案，可实现自定义加载器：");
            customDesc.style.color = new StyleColor(Design.TextSecondary);
            customDesc.style.fontSize = Design.FontSizeBody;
            customDesc.style.marginBottom = 8;
            customDesc.style.whiteSpace = WhiteSpace.Normal;
            customSection.Add(customDesc);

            var customCode = CreateCodeBlock(
@"// 实现 ITableLoader 接口
public class MyTableLoader : ITableLoader
{
    public byte[] Load(string tableName)
    {
        // 自定义加载逻辑
        return yourLoadMethod(tableName);
    }
}

// 初始化时设置加载器
TableKit.SetLoader(new MyTableLoader());");
            customSection.Add(customCode);

            // YooAsset 示例
            var yooSection = CreateGuideSection("YooAsset 加载器示例");
            container.Add(yooSection);

            var yooDesc = new Label("使用 YooAsset 加载配置表的完整实现：");
            yooDesc.style.color = new StyleColor(Design.TextSecondary);
            yooDesc.style.fontSize = Design.FontSizeBody;
            yooDesc.style.marginBottom = 8;
            yooDesc.style.whiteSpace = WhiteSpace.Normal;
            yooSection.Add(yooDesc);

            var yooCode = CreateCodeBlock(
@"using YooAsset;

public class YooAssetTableLoader : ITableLoader
{
    private readonly ResourcePackage mPackage;
    private readonly string mPathPattern;

    public YooAssetTableLoader(ResourcePackage package, string pathPattern = ""{0}"")
    {
        mPackage = package;
        mPathPattern = pathPattern;
    }

    public byte[] Load(string tableName)
    {
        var path = string.Format(mPathPattern, tableName);
        var handle = mPackage.LoadRawFileSync(path);
        return handle.GetRawFileData();
    }
}

// 使用示例
var package = YooAssets.GetPackage(""DefaultPackage"");
TableKit.SetLoader(new YooAssetTableLoader(package, ""Art/Table/{0}""));");
            yooSection.Add(yooCode);

            // 注意事项
            var noteSection = CreateGuideSection("注意事项");
            container.Add(noteSection);

            var notes = new[]
            {
                "• 运行时模式路径需与数据输出路径对应",
                "• 使用 Resources 时，数据需放在 Resources 文件夹下",
                "• 使用 YooAsset 时，确保资源已正确打包",
                "• 编辑器数据路径用于 TableKit.TablesEditor 访问"
            };

            foreach (var note in notes)
            {
                var noteLabel = new Label(note);
                noteLabel.style.color = new StyleColor(Design.TextSecondary);
                noteLabel.style.fontSize = Design.FontSizeBody;
                noteLabel.style.marginTop = 4;
                noteSection.Add(noteLabel);
            }
        }

        /// <summary>
        /// 创建指南子区块
        /// </summary>
        private VisualElement CreateGuideSection(string title)
        {
            var section = new VisualElement();
            section.style.marginBottom = 12;

            var titleLabel = new Label(title);
            titleLabel.style.color = new StyleColor(Design.BrandPrimary);
            titleLabel.style.fontSize = Design.FontSizeBody;
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = 6;
            section.Add(titleLabel);

            return section;
        }

        /// <summary>
        /// 创建代码块（带语法高亮）
        /// </summary>
        private VisualElement CreateCodeBlock(string code)
        {
            var container = new VisualElement();
            container.style.backgroundColor = new StyleColor(Design.LayerConsole);
            container.style.borderTopLeftRadius = container.style.borderTopRightRadius = 4;
            container.style.borderBottomLeftRadius = container.style.borderBottomRightRadius = 4;
            container.style.paddingLeft = 8;
            container.style.paddingRight = 8;
            container.style.paddingTop = 6;
            container.style.paddingBottom = 6;
            container.style.overflow = Overflow.Hidden;

            var scrollView = new ScrollView(ScrollViewMode.Horizontal);
            scrollView.style.flexGrow = 1;
            container.Add(scrollView);

            // 应用语法高亮
            var highlightedCode = ApplySyntaxHighlighting(code);

            var codeLabel = new Label(highlightedCode);
            codeLabel.style.fontSize = Design.FontSizeCode;
            codeLabel.style.whiteSpace = WhiteSpace.PreWrap;
            codeLabel.enableRichText = true;
            scrollView.Add(codeLabel);

            return container;
        }

        /// <summary>
        /// C# 语法高亮颜色定义
        /// </summary>
        private static class SyntaxColors
        {
            public const string KEYWORD = "#569CD6";      // 蓝色 - 关键字
            public const string TYPE = "#4EC9B0";         // 青色 - 类型名
            public const string STRING = "#CE9178";       // 橙色 - 字符串
            public const string COMMENT = "#6A9955";      // 绿色 - 注释
            public const string NUMBER = "#B5CEA8";       // 浅绿 - 数字
            public const string METHOD = "#DCDCAA";       // 黄色 - 方法名
            public const string DEFAULT = "#D4D4D4";      // 灰白 - 默认文本
        }

        /// <summary>
        /// 应用 C# 语法高亮
        /// </summary>
        private string ApplySyntaxHighlighting(string code)
        {
            // C# 关键字
            var keywords = new HashSet<string>
            {
                "using", "namespace", "class", "struct", "interface", "enum",
                "public", "private", "protected", "internal", "static", "readonly",
                "const", "new", "return", "if", "else", "for", "foreach", "while",
                "var", "void", "string", "int", "bool", "byte", "float", "double",
                "null", "true", "false", "this", "base", "get", "set", "value"
            };

            // 常见类型名
            var types = new HashSet<string>
            {
                "ITableLoader", "ResourcePackage", "TableKit", "YooAssets",
                "Tables", "TablesEditor", "String", "Int32", "Boolean"
            };

            var result = new StringBuilder();
            var lines = code.Split('\n');

            foreach (var line in lines)
            {
                var processedLine = ProcessLine(line, keywords, types);
                result.AppendLine(processedLine);
            }

            // 移除最后的换行
            if (result.Length > 0 && result[result.Length - 1] == '\n')
                result.Length--;
            if (result.Length > 0 && result[result.Length - 1] == '\r')
                result.Length--;

            return result.ToString();
        }

        /// <summary>
        /// 处理单行代码的语法高亮
        /// </summary>
        private string ProcessLine(string line, HashSet<string> keywords, HashSet<string> types)
        {
            // 处理注释行
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith("//"))
            {
                var leadingSpaces = line.Substring(0, line.Length - trimmed.Length);
                return $"{leadingSpaces}<color={SyntaxColors.COMMENT}>{EscapeRichText(trimmed)}</color>";
            }

            var result = new StringBuilder();
            var i = 0;
            var lineChars = line.ToCharArray();

            while (i < lineChars.Length)
            {
                // 处理字符串
                if (lineChars[i] == '"')
                {
                    var stringEnd = FindStringEnd(line, i);
                    var str = line.Substring(i, stringEnd - i + 1);
                    result.Append($"<color={SyntaxColors.STRING}>{EscapeRichText(str)}</color>");
                    i = stringEnd + 1;
                    continue;
                }

                // 处理标识符和关键字
                if (char.IsLetter(lineChars[i]) || lineChars[i] == '_')
                {
                    var wordEnd = i;
                    while (wordEnd < lineChars.Length && (char.IsLetterOrDigit(lineChars[wordEnd]) || lineChars[wordEnd] == '_'))
                        wordEnd++;

                    var word = line.Substring(i, wordEnd - i);

                    if (keywords.Contains(word))
                        result.Append($"<color={SyntaxColors.KEYWORD}>{word}</color>");
                    else if (types.Contains(word))
                        result.Append($"<color={SyntaxColors.TYPE}>{word}</color>");
                    else if (wordEnd < lineChars.Length && lineChars[wordEnd] == '(')
                        result.Append($"<color={SyntaxColors.METHOD}>{word}</color>");
                    else
                        result.Append($"<color={SyntaxColors.DEFAULT}>{word}</color>");

                    i = wordEnd;
                    continue;
                }

                // 处理数字
                if (char.IsDigit(lineChars[i]))
                {
                    var numEnd = i;
                    while (numEnd < lineChars.Length && (char.IsDigit(lineChars[numEnd]) || lineChars[numEnd] == '.'))
                        numEnd++;

                    var num = line.Substring(i, numEnd - i);
                    result.Append($"<color={SyntaxColors.NUMBER}>{num}</color>");
                    i = numEnd;
                    continue;
                }

                // 其他字符
                result.Append($"<color={SyntaxColors.DEFAULT}>{EscapeRichText(lineChars[i].ToString())}</color>");
                i++;
            }

            return result.ToString();
        }

        /// <summary>
        /// 查找字符串结束位置
        /// </summary>
        private int FindStringEnd(string line, int start)
        {
            for (int i = start + 1; i < line.Length; i++)
            {
                if (line[i] == '"' && (i == 0 || line[i - 1] != '\\'))
                    return i;
            }
            return line.Length - 1;
        }

        /// <summary>
        /// 转义 Rich Text 特殊字符
        /// </summary>
        private string EscapeRichText(string text)
        {
            return text.Replace("<", "<<").Replace(">", ">>");
        }

        #endregion
    }
}
#endif
