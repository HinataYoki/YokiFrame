#if UNITY_EDITOR && YOKIFRAME_LUBAN_SUPPORT
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.TableKit.Editor
{
    /// <summary>
    /// TableKitEditorUI - 构建选项区块
    /// </summary>
    public partial class TableKitEditorUI
    {
        #region 构建选项区

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

            var toggleGroup = new VisualElement();
            container.Add(toggleGroup);

            BuildAssemblyOption(toggleGroup);
            BuildExternalTypeOption(toggleGroup);

            return container;
        }

        private void BuildAssemblyOption(VisualElement toggleGroup)
        {
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

            var asmHint = new Label("打开后生成的代码会放入独立程序集 (asmdef)");
            asmHint.style.fontSize = Design.FontSizeSmall;
            asmHint.style.color = new StyleColor(Design.TextTertiary);
            asmHint.style.marginBottom = 8;
            toggleGroup.Add(asmHint);
        }

        private void BuildExternalTypeOption(VisualElement toggleGroup)
        {
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

            var extHint = new Label("Luban vector 转 Unity Vector，如有需要可自行添加代码，不会重复生成覆盖");
            extHint.style.fontSize = Design.FontSizeSmall;
            extHint.style.color = new StyleColor(Design.TextTertiary);
            extHint.style.marginBottom = 4;
            toggleGroup.Add(extHint);

            var extHint2 = new Label("注意：TableKit.cs 会被重复生成覆盖，请勿在其中添加自定义代码");
            extHint2.style.fontSize = Design.FontSizeSmall;
            extHint2.style.color = new StyleColor(Design.BrandWarning);
            toggleGroup.Add(extHint2);
        }

        #endregion
    }
}
#endif
