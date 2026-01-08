#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System;
using System.Collections.Generic;
using System.IO;
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

            var title = new Label("📊 TableKit 配置表生成");
            title.style.fontSize = 14;
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

            // 验证按钮
            var validateBtn = new Button(ValidateLuban) { text = "✓ 验证" };
            ApplySecondaryButtonStyle(validateBtn);
            container.Add(validateBtn);

            // 打开目录
            var openBtn = new Button(OpenLubanFolder) { text = "📁" };
            openBtn.style.width = 28;
            openBtn.style.height = 28;
            openBtn.style.marginLeft = 4;
            ApplySecondaryButtonStyle(openBtn);
            container.Add(openBtn);

            // 生成按钮
            mGenerateBtn = new Button(GenerateLuban) { text = "⚡ 生成配置表" };
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
            arrow.style.fontSize = 10;
            arrow.style.color = new StyleColor(Design.TextTertiary);
            arrow.style.marginRight = 6;
            header.Add(arrow);

            var title = new Label("⚙️ 环境与路径配置");
            title.style.fontSize = 13;
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
            var warning = CreateCallout("⚠️ Luban 工具不应放置在 Assets 内部，推荐放置在与 Assets 同级目录", Design.BrandWarning);
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

            lubanSection.Add(CreateValidatedFileRow("Luban.dll:", ref mLubanDllPathField, mLubanDllPath, path =>
            {
                mLubanDllPath = path;
                mLubanDllPathField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, "dll", "选择 Luban.dll"));

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

            outputSection.Add(CreateValidatedPathRow("代码输出:", ref mOutputCodeDirField, mOutputCodeDir, path =>
            {
                mOutputCodeDir = path;
                mOutputCodeDirField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, false, "选择代码输出目录"));

            // TableKit 路径
            var tkSection = CreateSubSection("TableKit 路径");
            container.Add(tkSection);

            tkSection.Add(CreateValidatedPathRow("编辑器数据:", ref mEditorDataPathField, mEditorDataPath, path =>
            {
                mEditorDataPath = path;
                mEditorDataPathField.value = path;
                SavePrefs();
            }, false, "选择编辑器数据路径"));

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
            hint.style.fontSize = 10;
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

            var title = new Label("🔧 构建选项");
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            title.style.marginBottom = 12;
            container.Add(title);

            // Toggle 组
            var toggleGroup = new VisualElement();
            toggleGroup.style.flexDirection = FlexDirection.Row;
            toggleGroup.style.flexWrap = Wrap.Wrap;
            container.Add(toggleGroup);

            var asmContainer = new VisualElement { style = { marginRight = 24, marginBottom = 8 } };
            mUseAssemblyToggle = CreateCapsuleToggle("使用独立程序集", mUseAssemblyDefinition, v =>
            {
                mUseAssemblyDefinition = v;
                mAssemblyNameField?.SetEnabled(v);
                SavePrefs();
            });
            asmContainer.Add(mUseAssemblyToggle);
            toggleGroup.Add(asmContainer);

            var extContainer = new VisualElement { style = { marginBottom = 8 } };
            mGenerateExternalTypeUtilToggle = CreateCapsuleToggle("生成 ExternalTypeUtil", mGenerateExternalTypeUtil, v =>
            {
                mGenerateExternalTypeUtil = v;
                SavePrefs();
            });
            extContainer.Add(mGenerateExternalTypeUtilToggle);
            toggleGroup.Add(extContainer);

            // 程序集名称
            var asmRow = new VisualElement();
            asmRow.style.flexDirection = FlexDirection.Row;
            asmRow.style.alignItems = Align.Center;
            asmRow.style.marginTop = 8;
            container.Add(asmRow);

            var asmLabel = new Label("程序集名称:");
            asmLabel.style.width = 100;
            asmLabel.style.color = new StyleColor(Design.TextSecondary);
            asmRow.Add(asmLabel);

            mAssemblyNameField = new TextField();
            mAssemblyNameField.style.flexGrow = 1;
            mAssemblyNameField.value = mAssemblyName;
            mAssemblyNameField.SetEnabled(mUseAssemblyDefinition);
            mAssemblyNameField.RegisterValueChangedCallback(evt => { mAssemblyName = evt.newValue; SavePrefs(); });
            asmRow.Add(mAssemblyNameField);

            var hint = new Label("独立程序集: 代码放入独立 asmdef • ExternalTypeUtil: Luban vector 转 Unity Vector");
            hint.style.fontSize = 10;
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 8;
            container.Add(hint);

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

            var title = new Label("📝 控制台");
            title.style.fontSize = 13;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.color = new StyleColor(Design.TextPrimary);
            header.Add(title);

            var btns = new VisualElement();
            btns.style.flexDirection = FlexDirection.Row;
            btns.style.alignItems = Align.Center;
            header.Add(btns);

            var refreshBtn = new Button(RefreshEditorCache) { text = "🔄 刷新缓存" };
            ApplySmallButtonStyle(refreshBtn);
            btns.Add(refreshBtn);

            var clearBtn = new Button(ClearLog) { text = "🗑️ 清除" };
            clearBtn.style.marginLeft = 4;
            ApplySmallButtonStyle(clearBtn);
            btns.Add(clearBtn);

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
            mStatusBannerLabel.style.fontSize = 12;
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
            mLogContent.style.fontSize = 11;
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

            var header = new Label("👁️ 数据预览");
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new StyleColor(Design.TextPrimary);
            header.style.paddingLeft = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.Add(header);

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

            var header = new Label("📋 配置表信息");
            header.style.fontSize = 13;
            header.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.style.color = new StyleColor(Design.TextPrimary);
            header.style.paddingLeft = 12;
            header.style.paddingTop = 10;
            header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(Design.BorderDefault);
            container.Add(header);

            mTablesInfoContainer = new VisualElement();
            mTablesInfoContainer.style.paddingLeft = 12;
            mTablesInfoContainer.style.paddingRight = 12;
            mTablesInfoContainer.style.paddingBottom = 12;
            container.Add(mTablesInfoContainer);

            var hint = new Label("点击「刷新缓存」加载配置表信息");
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 8;
            mTablesInfoContainer.Add(hint);

            return container;
        }

        #endregion
    }
}
#endif
