#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 配置内容区块
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 可折叠配置区

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
            container.style.overflow = Overflow.Hidden;

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

            var arrow = new Image { name = "foldout-arrow", image = TableKitIcons.GetIcon(TableKitIcons.CHEVRON_RIGHT) };
            arrow.style.width = 12;
            arrow.style.height = 12;
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

            arrow.image = isExpanded ? TableKitIcons.GetIcon(TableKitIcons.CHEVRON_DOWN) : TableKitIcons.GetIcon(TableKitIcons.CHEVRON_RIGHT);

            header.RegisterCallback<ClickEvent>(_ =>
            {
                bool expanded = mConfigFoldout.style.display == DisplayStyle.Flex;
                mConfigFoldout.style.display = expanded ? DisplayStyle.None : DisplayStyle.Flex;
                arrow.image = expanded ? TableKitIcons.GetIcon(TableKitIcons.CHEVRON_RIGHT) : TableKitIcons.GetIcon(TableKitIcons.CHEVRON_DOWN);
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

            BuildLubanSection(container);
            BuildOutputSection(container);
            BuildTableKitSection(container);
        }

        private void BuildLubanSection(VisualElement container)
        {
            var lubanSection = CreateSubSection("Luban 环境");
            container.Add(lubanSection);

            lubanSection.Add(CreateValidatedPathRow("工作目录:", ref mLubanWorkDirField, mLubanWorkDir, path =>
            {
                mLubanWorkDir = path;
                mLubanWorkDirField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, true, "选择包含 luban.conf 的目录"));

            var workDirHint = new Label("包含 Datas、Defines、luban.conf 的文件夹");
            workDirHint.style.fontSize = Design.FontSizeSmall;
            workDirHint.style.color = new StyleColor(Design.TextTertiary);
            workDirHint.style.marginTop = 2;
            workDirHint.style.marginLeft = 80;
            lubanSection.Add(workDirHint);

            lubanSection.Add(CreateValidatedFileRow("Luban.dll:", ref mLubanDllPathField, mLubanDllPath, path =>
            {
                mLubanDllPath = path;
                mLubanDllPathField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, "dll", "选择 Luban.dll"));

            var dllHint = new Label("Luban 代码生成工具的 DLL 路径");
            dllHint.style.fontSize = Design.FontSizeSmall;
            dllHint.style.color = new StyleColor(Design.TextTertiary);
            dllHint.style.marginTop = 2;
            dllHint.style.marginLeft = 80;
            lubanSection.Add(dllHint);
        }

        private void BuildOutputSection(VisualElement container)
        {
            var outputSection = CreateSubSection("输出路径");
            container.Add(outputSection);

            // Data Target
            var dataRow = new VisualElement();
            dataRow.style.flexDirection = FlexDirection.Row;
            dataRow.style.alignItems = Align.Center;
            dataRow.style.marginTop = 8;
            outputSection.Add(dataRow);

            var dataLabel = new Label("数据格式:");
            dataLabel.style.width = 80;
            dataLabel.style.color = new StyleColor(Design.TextSecondary);
            dataRow.Add(dataLabel);

            mDataTargetDropdown = new DropdownField(new List<string>(DATA_TARGET_OPTIONS), 0);
            mDataTargetDropdown.style.width = 120;
            mDataTargetDropdown.value = string.IsNullOrEmpty(mDataTarget) ? DATA_TARGET_OPTIONS[0] : mDataTarget;
            mDataTargetDropdown.RegisterValueChangedCallback(evt =>
            {
                mDataTarget = evt.newValue;
                var newCodeTarget = evt.newValue == "bin" ? "cs-bin" : "cs-simple-json";
                if (mCodeTarget != newCodeTarget)
                {
                    mCodeTarget = newCodeTarget;
                    mCodeTargetDropdown?.SetValueWithoutNotify(newCodeTarget);
                }
                SavePrefs();
            });
            dataRow.Add(mDataTargetDropdown);

            outputSection.Add(CreateValidatedPathRow("数据输出:", ref mOutputDataDirField, mOutputDataDir, path =>
            {
                mOutputDataDir = path;
                mOutputDataDirField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, false, "选择数据输出目录"));

            var dataOutputHint = new Label("生成的配置数据文件存放路径，默认 Assets/Resources/Art/Table/");
            dataOutputHint.style.fontSize = Design.FontSizeSmall;
            dataOutputHint.style.color = new StyleColor(Design.TextTertiary);
            dataOutputHint.style.marginTop = 2;
            dataOutputHint.style.marginLeft = 80;
            outputSection.Add(dataOutputHint);

            outputSection.Add(CreateValidatedPathRow("代码输出:", ref mOutputCodeDirField, mOutputCodeDir, path =>
            {
                mOutputCodeDir = path;
                mOutputCodeDirField.value = path;
                SavePrefs();
                RefreshConfigStatus();
            }, false, "选择代码输出目录"));

            var codeOutputHint = new Label("生成的 C# 配置表代码存放路径");
            codeOutputHint.style.fontSize = Design.FontSizeSmall;
            codeOutputHint.style.color = new StyleColor(Design.TextTertiary);
            codeOutputHint.style.marginTop = 2;
            codeOutputHint.style.marginLeft = 80;
            outputSection.Add(codeOutputHint);

            // 多目标输出配置
            BuildExtraOutputSection(container);
        }

        private void BuildTableKitSection(VisualElement container)
        {
            var tkSection = CreateSubSection("TableKit 路径");
            container.Add(tkSection);

            tkSection.Add(CreateValidatedPathRow("编辑器数据:", ref mEditorDataPathField, mEditorDataPath, path =>
            {
                mEditorDataPath = path;
                mEditorDataPathField.value = path;
                SavePrefs();
            }, false, "选择编辑器数据路径"));

            var editorDataHint = new Label("TableKit.TablesEditor 编辑器访问用的表数据路径");
            editorDataHint.style.fontSize = Design.FontSizeSmall;
            editorDataHint.style.color = new StyleColor(Design.TextTertiary);
            editorDataHint.style.marginTop = 2;
            editorDataHint.style.marginLeft = 80;
            tkSection.Add(editorDataHint);

            var runtimeRow = new VisualElement();
            runtimeRow.style.flexDirection = FlexDirection.Row;
            runtimeRow.style.alignItems = Align.Center;
            runtimeRow.style.marginTop = 8;
            tkSection.Add(runtimeRow);

            var runtimeLabel = new Label("运行时模式:");
            runtimeLabel.style.width = 80;
            runtimeLabel.style.color = new StyleColor(Design.TextSecondary);
            runtimeRow.Add(runtimeLabel);

            var runtimeFieldContainer = new VisualElement();
            runtimeFieldContainer.style.flexDirection = FlexDirection.Row;
            runtimeFieldContainer.style.flexGrow = 1;
            runtimeRow.Add(runtimeFieldContainer);

            mRuntimePathPatternField = new TextField();
            mRuntimePathPatternField.style.flexGrow = 1;
            mRuntimePathPatternField.value = mRuntimePathPattern;
            mRuntimePathPatternField.RegisterValueChangedCallback(evt => { mRuntimePathPattern = evt.newValue; SavePrefs(); });
            runtimeFieldContainer.Add(mRuntimePathPatternField);

            var hint = new Label("{0} 为文件名占位符 • 可寻址模式填 {0} • 完整路径填 Assets/Art/Table/{0}");
            hint.style.fontSize = Design.FontSizeSmall;
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 4;
            hint.style.marginLeft = 80;
            tkSection.Add(hint);
        }

        #endregion
    }
}
#endif
