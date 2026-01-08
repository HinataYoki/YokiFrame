#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 多目标输出 UI 构建
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 多目标输出 UI 字段

        private VisualElement mExtraOutputContainer;

        #endregion

        #region 多目标输出 UI 构建

        /// <summary>
        /// 构建多目标输出区块
        /// </summary>
        private VisualElement BuildExtraOutputSection(VisualElement container)
        {
            var section = CreateSubSection("额外输出目标");
            container.Add(section);

            var hint = new Label("可添加多个输出目标，每个目标可独立选择导出字段分组");
            hint.style.fontSize = Design.FontSizeSmall;
            hint.style.color = new StyleColor(Design.TextTertiary);
            hint.style.marginTop = 4;
            section.Add(hint);

            var groupHint = new Label("提示: 不同导出目标会分批运行 Luban，确保字段正确导出");
            groupHint.style.fontSize = Design.FontSizeSmall;
            groupHint.style.color = new StyleColor(Design.BrandWarning);
            groupHint.style.marginTop = 2;
            section.Add(groupHint);

            mExtraOutputContainer = new VisualElement();
            mExtraOutputContainer.style.marginTop = 8;
            section.Add(mExtraOutputContainer);

            var addBtn = new Button(AddExtraOutputTarget) { text = "+ 添加输出目标" };
            addBtn.style.marginTop = 8;
            addBtn.style.alignSelf = Align.FlexStart;
            ApplySmallButtonStyle(addBtn);
            section.Add(addBtn);

            RefreshExtraOutputList();

            return section;
        }

        /// <summary>
        /// 刷新额外输出目标列表
        /// </summary>
        private void RefreshExtraOutputList()
        {
            mExtraOutputContainer.Clear();

            for (int i = 0; i < mExtraOutputTargets.Count; i++)
            {
                var target = mExtraOutputTargets[i];
                var index = i;
                mExtraOutputContainer.Add(BuildExtraOutputTargetItem(target, index));
            }
        }

        /// <summary>
        /// 构建单个输出目标项
        /// </summary>
        private VisualElement BuildExtraOutputTargetItem(ExtraOutputTarget target, int index)
        {
            var item = new VisualElement();
            item.style.backgroundColor = new StyleColor(Design.LayerElevated);
            item.style.borderTopLeftRadius = item.style.borderTopRightRadius = 6;
            item.style.borderBottomLeftRadius = item.style.borderBottomRightRadius = 6;
            item.style.paddingLeft = 12;
            item.style.paddingRight = 12;
            item.style.paddingTop = 10;
            item.style.paddingBottom = 10;
            item.style.marginLeft = 8;
            item.style.marginRight = 8;
            item.style.marginBottom = 8;

            BuildTargetHeader(item, target, index);
            BuildTargetDataRow(item, target);
            BuildTargetCodeRow(item, target);

            return item;
        }

        private void BuildTargetHeader(VisualElement item, ExtraOutputTarget target, int index)
        {
            var headerRow = new VisualElement();
            headerRow.style.flexDirection = FlexDirection.Row;
            headerRow.style.alignItems = Align.Center;
            headerRow.style.justifyContent = Justify.SpaceBetween;
            item.Add(headerRow);

            var leftHeader = new VisualElement();
            leftHeader.style.flexDirection = FlexDirection.Row;
            leftHeader.style.alignItems = Align.Center;
            headerRow.Add(leftHeader);

            // 名称输入
            var nameField = new TextField();
            nameField.style.width = 80;
            nameField.value = target.name;
            nameField.RegisterValueChangedCallback(evt =>
            {
                target.name = evt.newValue;
                SaveExtraOutputTargets();
            });
            leftHeader.Add(nameField);

            // 导出目标下拉
            var targetDropdown = new DropdownField(new List<string>(TARGET_OPTIONS), 0);
            targetDropdown.style.width = 70;
            targetDropdown.style.marginLeft = 8;
            targetDropdown.value = target.target;
            targetDropdown.tooltip = "决定导出哪些字段分组（client=客户端字段, server=服务端字段, all=全部）";
            targetDropdown.RegisterValueChangedCallback(evt =>
            {
                target.target = evt.newValue;
                SaveExtraOutputTargets();
            });
            leftHeader.Add(targetDropdown);

            // 启用开关
            var enableToggle = CreateCapsuleToggle("启用", target.enabled, v =>
            {
                target.enabled = v;
                SaveExtraOutputTargets();
            });
            enableToggle.style.marginLeft = 8;
            leftHeader.Add(enableToggle);

            // 删除按钮
            var deleteBtn = new Button(() =>
            {
                mExtraOutputTargets.RemoveAt(index);
                SaveExtraOutputTargets();
                RefreshExtraOutputList();
            });
            deleteBtn.style.width = 24;
            deleteBtn.style.height = 24;
            deleteBtn.style.backgroundColor = new StyleColor(Color.clear);
            deleteBtn.style.paddingLeft = 4;
            deleteBtn.style.paddingRight = 4;
            var deleteIcon = new Image { image = TableKitIcons.GetIcon(TableKitIcons.DELETE) };
            deleteIcon.style.width = 14;
            deleteIcon.style.height = 14;
            deleteBtn.Add(deleteIcon);
            headerRow.Add(deleteBtn);

            // 单独生成按钮
            var generateBtn = new Button(() => GenerateSingleTarget(index)) { text = "生成" };
            generateBtn.style.marginLeft = 4;
            generateBtn.style.height = 22;
            generateBtn.style.paddingLeft = 8;
            generateBtn.style.paddingRight = 8;
            generateBtn.style.backgroundColor = new StyleColor(Design.BrandPrimary);
            generateBtn.style.color = new StyleColor(Color.white);
            generateBtn.style.borderTopLeftRadius = generateBtn.style.borderTopRightRadius = 3;
            generateBtn.style.borderBottomLeftRadius = generateBtn.style.borderBottomRightRadius = 3;
            generateBtn.tooltip = "仅生成此目标的数据和代码";
            headerRow.Add(generateBtn);
        }

        private void BuildTargetDataRow(VisualElement item, ExtraOutputTarget target)
        {
            var dataRow = new VisualElement();
            dataRow.style.flexDirection = FlexDirection.Row;
            dataRow.style.alignItems = Align.Center;
            dataRow.style.marginTop = 8;
            item.Add(dataRow);

            var dataLabel = new Label("数据:");
            dataLabel.style.width = 40;
            dataLabel.style.color = new StyleColor(Design.TextSecondary);
            dataRow.Add(dataLabel);

            var dataTargetDropdown = new DropdownField(new List<string>(DATA_TARGET_OPTIONS), 0);
            dataTargetDropdown.style.width = 70;
            dataTargetDropdown.value = target.dataTarget;
            dataRow.Add(dataTargetDropdown);

            var dataFieldContainer = new VisualElement();
            dataFieldContainer.style.flexDirection = FlexDirection.Row;
            dataFieldContainer.style.flexGrow = 1;
            dataFieldContainer.style.marginLeft = 8;
            dataRow.Add(dataFieldContainer);

            var dataDirField = new TextField();
            dataDirField.style.flexGrow = 1;
            dataDirField.value = target.dataDir;
            dataDirField.RegisterValueChangedCallback(evt =>
            {
                target.dataDir = evt.newValue;
                SaveExtraOutputTargets();
            });
            dataFieldContainer.Add(dataDirField);

            var dataBtnContainer = new VisualElement();
            dataBtnContainer.style.flexDirection = FlexDirection.Row;
            dataRow.Add(dataBtnContainer);

            var dataBrowseBtn = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择数据输出目录", target.dataDir, "");
                if (!string.IsNullOrEmpty(path))
                {
                    target.dataDir = path;
                    dataDirField.value = path;
                    SaveExtraOutputTargets();
                }
            }) { text = "..." };
            dataBrowseBtn.style.width = 24;
            dataBrowseBtn.style.marginLeft = 4;
            dataBtnContainer.Add(dataBrowseBtn);

            var dataOpenBtn = CreateOpenFolderButton(() => dataDirField.value);
            dataBtnContainer.Add(dataOpenBtn);

            // 存储引用以便代码行使用
            item.userData = (dataTargetDropdown, dataDirField);
        }

        private void BuildTargetCodeRow(VisualElement item, ExtraOutputTarget target)
        {
            var codeRow = new VisualElement();
            codeRow.style.flexDirection = FlexDirection.Row;
            codeRow.style.alignItems = Align.Center;
            codeRow.style.marginTop = 4;
            item.Add(codeRow);

            var codeLabel = new Label("代码:");
            codeLabel.style.width = 40;
            codeLabel.style.color = new StyleColor(Design.TextSecondary);
            codeRow.Add(codeLabel);

            var codeTargetDropdown = new DropdownField(new List<string>(ALL_CODE_TARGET_OPTIONS), 0);
            codeTargetDropdown.style.width = 130;
            codeTargetDropdown.value = target.codeTarget;
            codeRow.Add(codeTargetDropdown);

            // 获取数据行的引用
            var (dataTargetDropdown, _) = ((DropdownField, TextField))item.userData;

            // 数据格式改变时，自动同步代码类型
            dataTargetDropdown.RegisterValueChangedCallback(evt =>
            {
                target.dataTarget = evt.newValue;
                var newCodeTarget = GetMatchingCodeTarget(target.codeTarget, evt.newValue);
                if (newCodeTarget != target.codeTarget)
                {
                    target.codeTarget = newCodeTarget;
                    codeTargetDropdown.SetValueWithoutNotify(newCodeTarget);
                }
                SaveExtraOutputTargets();
            });

            // 代码类型改变时，自动同步数据格式
            codeTargetDropdown.RegisterValueChangedCallback(evt =>
            {
                target.codeTarget = evt.newValue;
                var newDataTarget = GetMatchingDataTarget(evt.newValue);
                if (newDataTarget != target.dataTarget)
                {
                    target.dataTarget = newDataTarget;
                    dataTargetDropdown.SetValueWithoutNotify(newDataTarget);
                }
                SaveExtraOutputTargets();
            });

            var codeFieldContainer = new VisualElement();
            codeFieldContainer.style.flexDirection = FlexDirection.Row;
            codeFieldContainer.style.flexGrow = 1;
            codeFieldContainer.style.marginLeft = 8;
            codeRow.Add(codeFieldContainer);

            var codeDirField = new TextField();
            codeDirField.style.flexGrow = 1;
            codeDirField.value = target.codeDir;
            codeDirField.RegisterValueChangedCallback(evt =>
            {
                target.codeDir = evt.newValue;
                SaveExtraOutputTargets();
            });
            codeFieldContainer.Add(codeDirField);

            var codeBtnContainer = new VisualElement();
            codeBtnContainer.style.flexDirection = FlexDirection.Row;
            codeRow.Add(codeBtnContainer);

            var codeBrowseBtn = new Button(() =>
            {
                var path = EditorUtility.OpenFolderPanel("选择代码输出目录", target.codeDir, "");
                if (!string.IsNullOrEmpty(path))
                {
                    target.codeDir = path;
                    codeDirField.value = path;
                    SaveExtraOutputTargets();
                }
            }) { text = "..." };
            codeBrowseBtn.style.width = 24;
            codeBrowseBtn.style.marginLeft = 4;
            codeBtnContainer.Add(codeBrowseBtn);

            var codeOpenBtn = CreateOpenFolderButton(() => codeDirField.value);
            codeBtnContainer.Add(codeOpenBtn);
        }

        /// <summary>
        /// 添加新的输出目标
        /// </summary>
        private void AddExtraOutputTarget()
        {
            mExtraOutputTargets.Add(new ExtraOutputTarget
            {
                name = $"目标{mExtraOutputTargets.Count + 1}",
                target = "server",
                dataTarget = "json",
                dataDir = "",
                codeTarget = "java-json",
                codeDir = "",
                enabled = true
            });
            SaveExtraOutputTargets();
            RefreshExtraOutputList();
        }

        #endregion
    }
}
#endif
