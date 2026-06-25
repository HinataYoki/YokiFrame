#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    public sealed partial class YooInitConfigDrawer
    {
        private static void RefreshBuildContent(VisualElement container, SerializedProperty property)
        {
            container.Clear();

            var packages = GetBuildPackageNames(property);
            var selectedPackage = packages.Count > 0 ? packages[0] : YooInitConfig.DEFAULT_PACKAGE_NAME;
            var selectedPipeline = GetBuildPipeline(selectedPackage);
            if (!sBuildPipelineChoices.Contains(selectedPipeline))
                selectedPipeline = sBuildPipelineChoices[0];

            AddBuildRows(container, property, packages, selectedPackage, selectedPipeline);
        }

        private static void AddBuildRows(VisualElement container, SerializedProperty property, List<string> packages, string selectedPackage, string selectedPipeline)
        {
            container.Add(CreateBuildPackageDropdown(container, property, packages, selectedPackage));
            container.Add(CreateBuildPipelineDropdown(container, property, packages, selectedPackage, selectedPipeline));
            container.Add(CreateBuildCompressDropdown(selectedPackage, selectedPipeline));
            container.Add(CreateBuildCopyOptionDropdown(selectedPackage, selectedPipeline));
            container.Add(CreateBuildCopyParamsRow(selectedPackage, selectedPipeline));
            container.Add(CreateBuildAdvancedOptions(selectedPackage, selectedPipeline));
            container.Add(CreateBuildSeparator());
            container.Add(CreateBuildButtons(selectedPackage, selectedPipeline));
        }

        private static VisualElement CreateBuildPackageDropdown(VisualElement container, SerializedProperty property, List<string> packages, string selectedPackage)
        {
            var row = CreateRow("资源包");
            var index = packages.IndexOf(selectedPackage);
            if (index < 0)
                index = 0;

            var dropdown = new DropdownField(packages, index);
            dropdown.value = packages[index];
            dropdown.name = "yoo-build-package-field";
            dropdown.AddToClassList("yoki-field-row__field");
            dropdown.style.flexGrow = 1f;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrEmpty(evt.newValue))
                    return;

                container.Clear();
                var pipeline = GetBuildPipeline(evt.newValue);
                if (!sBuildPipelineChoices.Contains(pipeline))
                    pipeline = sBuildPipelineChoices[0];
                AddBuildRows(container, property, packages, evt.newValue, pipeline);
            });
            row.Add(dropdown);
            return row;
        }

        private static VisualElement CreateBuildPipelineDropdown(VisualElement container, SerializedProperty property, List<string> packages, string selectedPackage, string selectedPipeline)
        {
            var row = CreateRow("构建管线");
            var index = sBuildPipelineChoices.IndexOf(selectedPipeline);
            if (index < 0)
                index = 0;

            var dropdown = new DropdownField(sBuildPipelineChoices, index);
            dropdown.value = sBuildPipelineChoices[index];
            dropdown.name = "yoo-build-pipeline-field";
            dropdown.AddToClassList("yoki-field-row__field");
            dropdown.style.flexGrow = 1f;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                if (string.IsNullOrEmpty(evt.newValue))
                    return;

                SetBuildPipeline(selectedPackage, evt.newValue);
                container.Clear();
                AddBuildRows(container, property, packages, selectedPackage, evt.newValue);
            });
            row.Add(dropdown);
            return row;
        }

        private static VisualElement CreateBuildCompressDropdown(string packageName, string pipelineName)
        {
            var row = CreateRow("压缩方式");
            var current = (int)GetCompressOption(packageName, pipelineName);
            current = ClampIndex(current, sCompressChoices.Count);
            var dropdown = new DropdownField(sCompressChoices, current);
            dropdown.value = sCompressChoices[current];
            dropdown.name = "yoo-build-compress-field";
            dropdown.AddToClassList("yoki-field-row__field");
            dropdown.style.flexGrow = 1f;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = sCompressChoices.IndexOf(evt.newValue);
                if (index >= 0)
                    SetCompressOption(packageName, pipelineName, (ECompressOption)index);
            });
            row.Add(dropdown);
            return row;
        }

        private static VisualElement CreateBuildCopyOptionDropdown(string packageName, string pipelineName)
        {
            var row = CreateRow("首包拷贝");
            var current = ClampIndex(GetCopyOptionIndex(packageName, pipelineName), sCopyOptionDisplayNames.Count);
            var dropdown = new DropdownField(sCopyOptionDisplayNames, current);
            dropdown.value = sCopyOptionDisplayNames[current];
            dropdown.name = "yoo-build-copy-option-field";
            dropdown.AddToClassList("yoki-field-row__field");
            dropdown.style.flexGrow = 1f;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                var index = sCopyOptionDisplayNames.IndexOf(evt.newValue);
                if (index >= 0)
                    SetCopyOption(packageName, pipelineName, index);
            });
            row.Add(dropdown);
            return row;
        }

        private static VisualElement CreateBuildCopyParamsRow(string packageName, string pipelineName)
        {
            var row = CreateRow("拷贝标签");
            var field = new TextField { value = GetCopyParams(packageName, pipelineName) };
            field.AddToClassList("yoki-field-row__field");
            field.style.flexGrow = 1f;
            field.RegisterValueChangedCallback(evt =>
            {
                SetCopyParams(packageName, pipelineName, evt.newValue);
            });
            row.Add(field);
            return row;
        }

        private static VisualElement CreateBuildAdvancedOptions(string packageName, string pipelineName)
        {
            var isExpanded = EditorPrefs.GetBool(FOLDOUT_PREFS_PREFIX + "yoo-build-advanced-options", false);
            var container = new VisualElement();
            container.name = "yoo-build-advanced-options";
            container.AddToClassList("yoki-section");
            container.style.marginTop = 10f;
            container.style.backgroundColor = sNestedBackground;
            container.style.borderTopLeftRadius = 5f;
            container.style.borderTopRightRadius = 5f;
            container.style.borderBottomLeftRadius = 5f;
            container.style.borderBottomRightRadius = 5f;
            container.style.paddingLeft = 12f;
            container.style.paddingRight = 12f;
            container.style.paddingTop = 9f;
            container.style.paddingBottom = 9f;

            var header = new VisualElement();
            header.name = "yoo-build-advanced-options-header";
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.minHeight = 24f;
            container.Add(header);

            var arrow = new Label(isExpanded ? "▼" : "▶");
            arrow.name = "yoo-build-advanced-options-arrow";
            arrow.style.width = 14f;
            arrow.style.color = sTextSecondary;
            arrow.style.fontSize = 10f;
            arrow.style.unityTextAlign = TextAnchor.MiddleCenter;
            header.Add(arrow);

            var title = new Label("高级选项");
            title.style.color = sTextPrimary;
            title.style.fontSize = 11f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.flexGrow = 1f;
            header.Add(title);

            var body = new VisualElement();
            body.name = "yoo-build-advanced-options-body";
            body.style.marginTop = 8f;
            body.style.display = isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
            container.Add(body);

            header.RegisterCallback<MouseEnterEvent>(_ =>
            {
                header.style.backgroundColor = YokiFrameUIComponents.Colors.LayerHover;
            });
            header.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                header.style.backgroundColor = StyleKeyword.Null;
            });
            header.RegisterCallback<ClickEvent>(_ =>
            {
                var next = body.style.display == DisplayStyle.None;
                body.style.display = next ? DisplayStyle.Flex : DisplayStyle.None;
                arrow.text = next ? "▼" : "▶";
                EditorPrefs.SetBool(FOLDOUT_PREFS_PREFIX + "yoo-build-advanced-options", next);
            });

            body.Add(CreateBuildBoolRow("清空构建缓存", GetClearBuildCache(packageName, pipelineName), value => SetClearBuildCache(packageName, pipelineName, value)));
            body.Add(CreateBuildBoolRow("使用依赖缓存", GetUseAssetDependencyDB(packageName, pipelineName), value => SetUseAssetDependencyDB(packageName, pipelineName, value)));
            body.Add(CreateBuildEncryptionDropdown(packageName, pipelineName));
            return container;
        }

        private static VisualElement CreateBuildBoolRow(string label, bool value, Action<bool> onChanged)
        {
            var toggle = YokiFrameUIComponents.CreateModernToggle(label, value, onChanged);
            toggle.style.marginTop = 6f;
            toggle.style.marginBottom = 6f;
            return toggle;
        }

        private static VisualElement CreateBuildEncryptionDropdown(string packageName, string pipelineName)
        {
            var row = CreateRow("加密服务");
            var choices = GetEncryptionServiceClassNames();
            var current = GetEncryptionServiceClassName(packageName, pipelineName);
            var index = choices.IndexOf(current);
            if (index < 0)
                index = 0;

            var dropdown = new DropdownField(choices, index);
            dropdown.value = choices[index];
            dropdown.AddToClassList("yoki-field-row__field");
            dropdown.style.flexGrow = 1f;
            dropdown.RegisterValueChangedCallback(evt =>
            {
                SetEncryptionServiceClassName(packageName, pipelineName, evt.newValue);
            });
            row.Add(dropdown);
            return row;
        }

        private static VisualElement CreateBuildSeparator()
        {
            var separator = new VisualElement();
            separator.style.height = 1f;
            separator.style.backgroundColor = sBorderColor;
            separator.style.marginTop = 12f;
            separator.style.marginBottom = 8f;
            return separator;
        }

        private static VisualElement CreateBuildButtons(string packageName, string pipelineName)
        {
            var container = new VisualElement();

            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            container.Add(row);

            var collector = CreateActionButton("打开收集器", sTextSecondary, YooAssetEditorMenuBridge.OpenCollector);
            collector.style.flexGrow = 1f;
            collector.style.marginRight = 6f;
            row.Add(collector);

            var builder = CreateActionButton("打开原始构建器", sTextSecondary, YooAssetEditorMenuBridge.OpenBuilder);
            builder.style.flexGrow = 1f;
            row.Add(builder);

            var build = CreateActionButton("构建资源包", sBrandGreen, () =>
            {
                SetBuildPipeline(packageName, pipelineName);
                if (!YooAssetEditorMenuBridge.OpenBuilder())
                    EditorUtility.DisplayDialog("YooAsset", "未找到 YooAsset 构建器菜单，请确认 YooAsset 包已正确导入。", "OK");
            });
            build.style.height = 30f;
            build.style.marginTop = 8f;
            container.Add(build);

            return container;
        }

        private static VisualElement CreateInfoPanel(string title, string description, Color accent)
        {
            var panel = new VisualElement();
            panel.AddToClassList("yoki-helpbox");
            panel.style.backgroundColor = new Color(accent.r * 0.12f, accent.g * 0.12f, accent.b * 0.12f, 0.70f);
            panel.style.borderLeftWidth = 2f;
            panel.style.borderLeftColor = accent;
            panel.style.borderTopLeftRadius = 5f;
            panel.style.borderTopRightRadius = 5f;
            panel.style.borderBottomLeftRadius = 5f;
            panel.style.borderBottomRightRadius = 5f;
            panel.style.paddingLeft = 12f;
            panel.style.paddingRight = 12f;
            panel.style.paddingTop = 9f;
            panel.style.paddingBottom = 9f;

            if (!string.IsNullOrEmpty(title))
            {
                var titleLabel = new Label(title);
                titleLabel.style.color = accent;
                titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                titleLabel.style.fontSize = 11f;
                titleLabel.style.marginBottom = 4f;
                panel.Add(titleLabel);
            }

            var descriptionLabel = new Label(description);
            descriptionLabel.AddToClassList("yoki-helpbox__text");
            descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
            descriptionLabel.style.color = sTextSecondary;
            descriptionLabel.style.fontSize = 10f;
            panel.Add(descriptionLabel);
            return panel;
        }

        private static Button CreateResetButton(string text, Action action)
        {
            var button = CreateActionButton(text, sTextSecondary, action);
            button.style.marginTop = 8f;
            return button;
        }

        private static Button CreateActionButton(string text, Color textColor, Func<bool> action)
        {
            return CreateActionButton(text, textColor, () =>
            {
                if (!action())
                    EditorUtility.DisplayDialog("YooAsset", "未找到 YooAsset 菜单，请确认 YooAsset 包已正确导入。", "OK");
            });
        }

        private static Button CreateActionButton(string text, Color textColor, Action action)
        {
            var button = new Button(action) { text = text };
            button.AddToClassList("yoki-btn-sm");
            button.style.height = 26f;
            button.style.backgroundColor = sFieldBackground;
            button.style.borderTopLeftRadius = 4f;
            button.style.borderTopRightRadius = 4f;
            button.style.borderBottomLeftRadius = 4f;
            button.style.borderBottomRightRadius = 4f;
            button.style.borderLeftWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderTopWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftColor = sBorderColor;
            button.style.borderRightColor = sBorderColor;
            button.style.borderTopColor = sBorderColor;
            button.style.borderBottomColor = sBorderColor;
            button.style.color = textColor;
            return button;
        }

        private static Color Lighten(Color color, float multiplier)
        {
            return new Color(
                Mathf.Clamp01(color.r * multiplier),
                Mathf.Clamp01(color.g * multiplier),
                Mathf.Clamp01(color.b * multiplier),
                color.a);
        }

        private static int ClampIndex(int value, int count)
        {
            if (count <= 0)
                return 0;

            if (value < 0)
                return 0;

            if (value >= count)
                return count - 1;

            return value;
        }

        private static void AddEnumChoice(SerializedProperty property, List<string> choices, List<int> values, string enumName)
        {
            var names = property.enumNames;
            for (var i = 0; i < names.Length; i++)
            {
                if (names[i] != enumName)
                    continue;

                choices.Add(enumName);
                values.Add(i);
                return;
            }
        }

    }
}
#endif
#endif
