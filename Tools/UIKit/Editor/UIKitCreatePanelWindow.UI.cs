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
    /// UIKitCreatePanelWindow - UI 构建方法
    /// </summary>
    public partial class UIKitCreatePanelWindow
    {
        #region UI 构建方法

        /// <summary>
        /// 构建程序集配置区块
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
        /// 构建命名空间配置区块
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
        /// 构建路径配置区块
        /// </summary>
        private void BuildPathsSection(VisualElement parent)
        {
            var section = CreateSectionContainer("路径配置");
            parent.Add(section);

            // Scripts 目录
            var (scriptRow, scriptField) = CreatePathFieldRow("Scripts目录", ScriptGeneratePath, _ =>
            {
                var folderPath = EditorUtility.OpenFolderPanel("选择Scripts目录", ScriptGeneratePath, string.Empty);
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

            // Prefab 目录
            var (prefabRow, prefabField) = CreatePathFieldRow("Prefab目录", PrefabGeneratePath, _ =>
            {
                var folderPath = EditorUtility.OpenFolderPanel("选择Prefab目录", PrefabGeneratePath, string.Empty);
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
        /// 构建面板配置区块
        /// </summary>
        private void BuildPanelConfigSection(VisualElement parent)
        {
            var section = CreateSectionContainer("面板配置");
            parent.Add(section);

            // 面板名称
            var (nameRow, nameField) = CreateCompactTextField("Panel名称", mPanelCreateName, v =>
            {
                mPanelCreateName = v;
                UpdatePreview();
                UpdateCreateButtonState();
            });
            mPanelNameField = nameField;
            section.Add(nameRow);

            // UI 层级
            var (levelRow, levelField) = CreateEnumFieldRow("UI层级", mSelectedLevel, v => mSelectedLevel = v);
            mLevelField = levelField;
            section.Add(levelRow);

            // 模态面板
            var (modalRow, modalToggle) = CreateToggleRow("模态面板", mIsModal, v => mIsModal = v);
            mModalToggle = modalToggle;
            section.Add(modalRow);

            // 生命周期钩子
            var (lifecycleRow, lifecycleToggle) = CreateToggleRow("生命周期钩子", mGenerateLifecycleHooks, v => mGenerateLifecycleHooks = v);
            mLifecycleToggle = lifecycleToggle;
            section.Add(lifecycleRow);

            // 焦点导航支持
            var (focusRow, focusToggle) = CreateToggleRow("焦点导航支持", mGenerateFocusSupport, v => mGenerateFocusSupport = v);
            mFocusToggle = focusToggle;
            section.Add(focusRow);
        }

        /// <summary>
        /// 构建动画配置区块
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

            // 显示动画
            var (showRow, showField) = CreateEnumFieldRow("显示动画", mShowAnimationType, v =>
            {
                mShowAnimationType = v;
                UpdateDurationVisibility();
            });
            mShowAnimField = showField;
            content.Add(showRow);

            // 隐藏动画
            var (hideRow, hideField) = CreateEnumFieldRow("隐藏动画", mHideAnimationType, v =>
            {
                mHideAnimationType = v;
                UpdateDurationVisibility();
            });
            mHideAnimField = hideField;
            content.Add(hideRow);

            // 动画时长
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
        /// 构建文件预览区块
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
        /// 构建创建按钮
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
        /// 创建区块容器
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
        /// 更新动画时长行的可见性
        /// </summary>
        private void UpdateDurationVisibility()
        {
            bool showDuration = mShowAnimationType != AnimationType.None || mHideAnimationType != AnimationType.None;
            mDurationRow.style.display = showDuration ? DisplayStyle.Flex : DisplayStyle.None;
        }

        /// <summary>
        /// 更新文件预览
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

            // 使用公共组件创建文件预览行
            mPreviewContainer.Add(CreateFilePreviewRow(PrefabPath, System.IO.File.Exists(PrefabPath)));
            mPreviewContainer.Add(CreateFilePreviewRow(ScriptPath, System.IO.File.Exists(ScriptPath)));
            mPreviewContainer.Add(CreateFilePreviewRow(DesignerPath, System.IO.File.Exists(DesignerPath)));
        }

        /// <summary>
        /// 更新创建按钮状态
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
