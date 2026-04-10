#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKitCreatePanelWindow 的 UI 构建与预览辅助逻辑。
    /// </summary>
    public partial class UIKitCreatePanelWindow
    {
        #region UI 构建方法

        /// <summary>
        /// 构建程序集配置区块。
        /// </summary>
        private void BuildAssemblySection(VisualElement parent)
        {
            var section = CreateSectionContainer("程序集配置");
            parent.Add(section);

            var (row, field) = CreateCompactTextField("程序集名称", AssemblyName, v => AssemblyName = v);
            mAssemblyField = field;
            section.Add(row);
        }

        /// <summary>
        /// 构建命名空间配置区块。
        /// </summary>
        private void BuildNamespaceSection(VisualElement parent)
        {
            var section = CreateSectionContainer("命名空间配置");
            parent.Add(section);

            var (row, field) = CreateCompactTextField("命名空间", ScriptNamespace, v => ScriptNamespace = v);
            mNamespaceField = field;
            section.Add(row);
        }

        /// <summary>
        /// 构建输出路径配置区块。
        /// </summary>
        private void BuildPathsSection(VisualElement parent)
        {
            var section = CreateSectionContainer("路径配置");
            parent.Add(section);

            var (scriptRow, scriptField) = CreatePathFieldRow("Scripts 目录", ScriptGeneratePath, _ =>
            {
                var folderPath = EditorUtility.OpenFolderPanel("选择 Scripts 目录", ScriptGeneratePath, string.Empty);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var assetsIndex = folderPath.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                    if (assetsIndex >= 0)
                    {
                        ScriptGeneratePath = folderPath[assetsIndex..];
                        mScriptPathField.value = ScriptGeneratePath;
                        UpdatePreview();
                    }
                }
            });
            mScriptPathField = scriptField;
            section.Add(scriptRow);

            var (prefabRow, prefabField) = CreatePathFieldRow("Prefab 目录", PrefabGeneratePath, _ =>
            {
                var folderPath = EditorUtility.OpenFolderPanel("选择 Prefab 目录", PrefabGeneratePath, string.Empty);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var assetsIndex = folderPath.IndexOf(ASSETS_PREFIX, StringComparison.Ordinal);
                    if (assetsIndex >= 0)
                    {
                        PrefabGeneratePath = folderPath[assetsIndex..];
                        mPrefabPathField.value = PrefabGeneratePath;
                        UpdatePreview();
                    }
                }
            });
            mPrefabPathField = prefabField;
            section.Add(prefabRow);
        }

        /// <summary>
        /// 构建面板生成参数区块。
        /// </summary>
        private void BuildPanelConfigSection(VisualElement parent)
        {
            var section = CreateSectionContainer("面板配置");
            parent.Add(section);

            var (nameRow, nameField) = CreateCompactTextField("Panel 名称", mPanelCreateName, v =>
            {
                mPanelCreateName = v;
                UpdatePreview();
                UpdateCreateButtonState();
            });
            mPanelNameField = nameField;
            section.Add(nameRow);

            {
                var levelNames = new System.Collections.Generic.List<string>();
                var predefined = UILevel.PredefinedLevels;
                int initialIndex = 0;
                for (int i = 0; i < predefined.Count; i++)
                {
                    levelNames.Add(predefined[i].ToString());
                    if (predefined[i] == mSelectedLevel) initialIndex = i;
                }

                mLevelField = new DropdownField(levelNames, initialIndex);
                mLevelField.RegisterValueChangedCallback(evt =>
                {
                    if (UILevel.TryParse(evt.newValue, out var parsed))
                        mSelectedLevel = parsed;
                });

                var levelRow = CreateCompactFormRow("UI 层级", mLevelField);
                section.Add(levelRow);
            }

            var (modalRow, modalToggle) = CreateToggleRow("模态面板", mIsModal, v => mIsModal = v);
            mModalToggle = modalToggle;
            section.Add(modalRow);

            var (lifecycleRow, lifecycleToggle) = CreateToggleRow("生命周期钩子", mGenerateLifecycleHooks, v => mGenerateLifecycleHooks = v);
            mLifecycleToggle = lifecycleToggle;
            section.Add(lifecycleRow);

            var (focusRow, focusToggle) = CreateToggleRow("焦点导航支持", mGenerateFocusSupport, v => mGenerateFocusSupport = v);
            mFocusToggle = focusToggle;
            section.Add(focusRow);
        }

        /// <summary>
        /// 构建动画配置区块。
        /// </summary>
        private void BuildAnimationSection(VisualElement parent)
        {
            mAnimationFoldout = new Foldout { text = "动画配置", value = false };
            mAnimationFoldout.style.marginTop = Spacing.SM;
            mAnimationFoldout.style.marginBottom = Spacing.SM;
            parent.Add(mAnimationFoldout);

            var content = new VisualElement();
            content.style.paddingLeft = Spacing.LG;
            content.style.paddingTop = Spacing.SM;
            content.style.paddingBottom = Spacing.SM;
            content.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            content.style.borderTopLeftRadius = Radius.MD;
            content.style.borderTopRightRadius = Radius.MD;
            content.style.borderBottomLeftRadius = Radius.MD;
            content.style.borderBottomRightRadius = Radius.MD;
            mAnimationFoldout.Add(content);

            var (showRow, showField) = CreateEnumFieldRow("显示动画", mShowAnimationType, v =>
            {
                mShowAnimationType = v;
                UpdateDurationVisibility();
            });
            mShowAnimField = showField;
            content.Add(showRow);

            var (hideRow, hideField) = CreateEnumFieldRow("隐藏动画", mHideAnimationType, v =>
            {
                mHideAnimationType = v;
                UpdateDurationVisibility();
            });
            mHideAnimField = hideField;
            content.Add(hideRow);

            mDurationRow = CreateCompactFormRow("动画时长", null);
            mDurationSlider = new Slider(0.1f, 2f) { value = mAnimationDuration };
            mDurationSlider.style.flexGrow = 1;
            mDurationSlider.RegisterValueChangedCallback(evt => mAnimationDuration = evt.newValue);
            mDurationRow.Add(mDurationSlider);

            var durationLabel = new Label($"{mAnimationDuration:F1}s");
            durationLabel.style.width = 40;
            durationLabel.style.unityTextAlign = TextAnchor.MiddleRight;
            mDurationSlider.RegisterValueChangedCallback(evt => durationLabel.text = $"{evt.newValue:F1}s");
            mDurationRow.Add(durationLabel);

            mDurationRow.style.display = DisplayStyle.None;
            content.Add(mDurationRow);
        }

        /// <summary>
        /// 构建输出文件预览区块。
        /// </summary>
        private void BuildPreviewSection(VisualElement parent)
        {
            mPreviewContainer = new VisualElement();
            mPreviewContainer.style.marginTop = Spacing.SM;
            mPreviewContainer.style.marginBottom = Spacing.SM;
            mPreviewContainer.style.paddingTop = Spacing.MD;
            mPreviewContainer.style.paddingBottom = Spacing.MD;
            mPreviewContainer.style.paddingLeft = Spacing.MD;
            mPreviewContainer.style.paddingRight = Spacing.MD;
            mPreviewContainer.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            mPreviewContainer.style.borderTopLeftRadius = Radius.MD;
            mPreviewContainer.style.borderTopRightRadius = Radius.MD;
            mPreviewContainer.style.borderBottomLeftRadius = Radius.MD;
            mPreviewContainer.style.borderBottomRightRadius = Radius.MD;
            mPreviewContainer.style.display = DisplayStyle.None;
            parent.Add(mPreviewContainer);
        }

        /// <summary>
        /// 构建创建按钮。
        /// </summary>
        private void BuildCreateButton(VisualElement parent)
        {
            mCreateButton = new Button(OnCreateUIPanelClick) { text = "创建 UI Panel" };
            mCreateButton.style.height = 36;
            mCreateButton.style.marginTop = Spacing.MD;
            mCreateButton.style.fontSize = 14;
            mCreateButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            mCreateButton.style.backgroundColor = new StyleColor(Colors.BrandPrimary);
            mCreateButton.style.color = new StyleColor(Color.white);
            mCreateButton.style.borderTopLeftRadius = Radius.MD;
            mCreateButton.style.borderTopRightRadius = Radius.MD;
            mCreateButton.style.borderBottomLeftRadius = Radius.MD;
            mCreateButton.style.borderBottomRightRadius = Radius.MD;
            mCreateButton.SetEnabled(false);
            parent.Add(mCreateButton);
        }

        #endregion

        #region UI 辅助方法

        /// <summary>
        /// 创建标准分区容器。
        /// </summary>
        private VisualElement CreateSectionContainer(string title)
        {
            var section = new VisualElement();
            section.style.marginBottom = Spacing.MD;
            section.style.paddingTop = Spacing.SM;
            section.style.paddingBottom = Spacing.SM;
            section.style.paddingLeft = Spacing.MD;
            section.style.paddingRight = Spacing.MD;
            section.style.backgroundColor = new StyleColor(Colors.LayerCard);
            section.style.borderTopLeftRadius = Radius.MD;
            section.style.borderTopRightRadius = Radius.MD;
            section.style.borderBottomLeftRadius = Radius.MD;
            section.style.borderBottomRightRadius = Radius.MD;

            var titleLabel = new Label(title);
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = Spacing.SM;
            titleLabel.style.color = new StyleColor(Colors.TextSecondary);
            section.Add(titleLabel);

            return section;
        }

        /// <summary>
        /// 根据动画配置切换时长输入行的显隐状态。
        /// </summary>
        private void UpdateDurationVisibility()
        {
            bool showDuration = mShowAnimationType != AnimationType.None || mHideAnimationType != AnimationType.None;
            mDurationRow.style.display = showDuration ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// 刷新当前输入对应的文件输出预览。
        /// </summary>
        private void UpdatePreview()
        {
            mPreviewContainer.Clear();

            if (string.IsNullOrEmpty(mPanelCreateName))
            {
                mPreviewContainer.style.display = DisplayStyle.None;
                return;
            }

            mPreviewContainer.style.display = DisplayStyle.Flex;

            var titleLabel = new Label("生成文件预览");
            titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleLabel.style.marginBottom = Spacing.SM;
            titleLabel.style.color = new StyleColor(Colors.TextSecondary);
            mPreviewContainer.Add(titleLabel);

            mPreviewContainer.Add(CreateFilePreviewRow(PrefabPath, System.IO.File.Exists(PrefabPath)));
            mPreviewContainer.Add(CreateFilePreviewRow(ScriptPath, System.IO.File.Exists(ScriptPath)));
            mPreviewContainer.Add(CreateFilePreviewRow(DesignerPath, System.IO.File.Exists(DesignerPath)));
        }

        /// <summary>
        /// 根据当前名称和目标文件状态刷新创建按钮可用性。
        /// </summary>
        private void UpdateCreateButtonState()
        {
            bool canCreate = !string.IsNullOrEmpty(mPanelCreateName) && !System.IO.File.Exists(PrefabPath);
            mCreateButton.SetEnabled(canCreate);
        }

        #endregion
    }
}
#endif
