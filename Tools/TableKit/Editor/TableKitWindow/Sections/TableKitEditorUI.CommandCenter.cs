#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 命令中心区块
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 命令中心

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
            mCodeTargetDropdown.RegisterValueChangedCallback(evt =>
            {
                mCodeTarget = evt.newValue;
                // 自动同步数据格式：cs-bin 对应 bin，其他 JSON 代码生成器对应 json
                var newDataTarget = evt.newValue == "cs-bin" ? "bin" : "json";
                if (mDataTarget != newDataTarget)
                {
                    mDataTarget = newDataTarget;
                    mDataTargetDropdown?.SetValueWithoutNotify(newDataTarget);
                }
                SavePrefs();
            });
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
    }
}
#endif
