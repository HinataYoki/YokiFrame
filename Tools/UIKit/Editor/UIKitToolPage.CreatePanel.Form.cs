#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKitToolPage - 创建面板功能 - 表单配置区
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 区域 1: 折叠式配置舱

        /// <summary>
        /// 构建折叠式配置舱
        /// </summary>
        private VisualElement BuildSettingsDeck()
        {
            var deck = new VisualElement();
            deck.style.marginBottom = Spacing.LG;
            deck.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            deck.style.borderTopLeftRadius = Radius.LG;
            deck.style.borderTopRightRadius = Radius.LG;
            deck.style.borderBottomLeftRadius = Radius.LG;
            deck.style.borderBottomRightRadius = Radius.LG;

            // 折叠面板
            mSettingsFoldout = new Foldout { text = "  项目设置", value = false };
            mSettingsFoldout.style.marginLeft = 0;
            mSettingsFoldout.style.marginRight = 0;

            // 折叠面板标题样式
            var toggle = mSettingsFoldout.Q<Toggle>();
            if (toggle != null)
            {
                toggle.style.marginLeft = Spacing.SM;
                toggle.style.marginTop = Spacing.SM;
                toggle.style.marginBottom = Spacing.XS;
            }

            // 配置内容容器
            var content = new VisualElement();
            content.style.paddingLeft = Spacing.MD;
            content.style.paddingRight = Spacing.MD;
            content.style.paddingBottom = Spacing.MD;

            // 使用公共组件创建两列布局
            var (gridContainer, leftColumn, rightColumn) = CreateTwoColumnLayout();
            leftColumn.style.paddingRight = Spacing.SM;
            rightColumn.style.paddingLeft = Spacing.SM;

            // 左列内容：程序集、模板
            leftColumn.Add(BuildCompactDropdownRow("程序集", CreateAssemblyDropdown(), "序列化时反射获取类型"));
            leftColumn.Add(BuildCompactDropdownRow("生成模板", CreateTemplateDropdown(), "代码生成样式"));

            // 右列内容：命名空间
            rightColumn.Add(BuildCompactInputRow("命名空间", ref mNamespaceField, ScriptNamespace, 
                evt =>
                {
                    ScriptNamespace = evt.newValue;
                    UIKitCreateConfig.Instance.SaveConfig();
                }));

            content.Add(gridContainer);

            // 路径设置（全宽）
            content.Add(BuildPathSettingsSection());

            mSettingsFoldout.Add(content);
            deck.Add(mSettingsFoldout);

            // 摘要行（折叠时显示）
            var summaryRow = BuildSettingsSummary();
            deck.Add(summaryRow);

            // 监听折叠状态变化
            mSettingsFoldout.RegisterValueChangedCallback(evt =>
            {
                summaryRow.style.display = evt.newValue ? DisplayStyle.None : DisplayStyle.Flex;
            });

            return deck;
        }

        /// <summary>
        /// 构建配置摘要行
        /// </summary>
        private VisualElement BuildSettingsSummary()
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.paddingLeft = Spacing.MD;
            row.style.paddingRight = Spacing.MD;
            row.style.paddingBottom = Spacing.SM;
            row.style.display = DisplayStyle.Flex;

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.INFO) };
            icon.style.width = SMALL_ICON_SIZE;
            icon.style.height = SMALL_ICON_SIZE;
            icon.style.marginRight = Spacing.XS;
            icon.style.opacity = 0.5f;
            row.Add(icon);

            var label = new Label($"命名空间: {ScriptNamespace}  |  路径: {ScriptGeneratePath}");
            label.style.fontSize = 11;
            label.style.color = new StyleColor(Colors.TextTertiary);
            row.Add(label);

            return row;
        }

        /// <summary>
        /// 构建紧凑下拉框行
        /// </summary>
        private VisualElement BuildCompactDropdownRow(string labelText, DropdownField dropdown, string tooltip)
        {
            var row = new VisualElement();
            row.style.marginBottom = Spacing.SM;

            var label = new Label(labelText);
            label.style.fontSize = 11;
            label.style.color = new StyleColor(Colors.TextTertiary);
            label.style.marginBottom = 2;
            label.tooltip = tooltip;
            row.Add(label);

            dropdown.style.height = INPUT_HEIGHT;
            row.Add(dropdown);

            return row;
        }

        /// <summary>
        /// 构建紧凑输入框行
        /// </summary>
        private VisualElement BuildCompactInputRow(string labelText, ref TextField textField, string initialValue,
            EventCallback<ChangeEvent<string>> onChanged)
        {
            var row = new VisualElement();
            row.style.marginBottom = Spacing.SM;

            var label = new Label(labelText);
            label.style.fontSize = 11;
            label.style.color = new StyleColor(Colors.TextTertiary);
            label.style.marginBottom = 2;
            row.Add(label);

            textField = new TextField { value = initialValue };
            textField.style.height = INPUT_HEIGHT;
            ApplyFlatInputStyle(textField);
            textField.RegisterValueChangedCallback(onChanged);
            row.Add(textField);

            return row;
        }

        /// <summary>
        /// 构建路径设置区域
        /// </summary>
        private VisualElement BuildPathSettingsSection()
        {
            var section = new VisualElement();
            section.style.marginTop = Spacing.SM;
            section.style.paddingTop = Spacing.SM;
            section.style.borderTopWidth = 1;
            section.style.borderTopColor = new StyleColor(Colors.BorderDefault);

            // Scripts 路径
            section.Add(BuildPathRow("Scripts 目录", ref mScriptPathField, ScriptGeneratePath, path =>
            {
                ScriptGeneratePath = path;
                mScriptPathField.value = path;
                UIKitCreateConfig.Instance.SaveConfig();
            }));

            // Prefab 路径
            section.Add(BuildPathRow("Prefab 目录", ref mPrefabPathField, PrefabGeneratePath, path =>
            {
                PrefabGeneratePath = path;
                mPrefabPathField.value = path;
                UIKitCreateConfig.Instance.SaveConfig();
            }));

            return section;
        }

        /// <summary>
        /// 构建路径选择行
        /// </summary>
        private VisualElement BuildPathRow(string labelText, ref TextField textField, string initialValue, Action<string> onPathChanged)
        {
            var row = new VisualElement();
            row.style.marginBottom = Spacing.SM;

            var label = new Label(labelText);
            label.style.fontSize = 11;
            label.style.color = new StyleColor(Colors.TextTertiary);
            label.style.marginBottom = 2;
            row.Add(label);

            var pathContainer = new VisualElement();
            pathContainer.style.flexDirection = FlexDirection.Row;

            textField = new TextField { value = initialValue };
            textField.style.flexGrow = 1;
            textField.style.height = INPUT_HEIGHT;
            textField.SetEnabled(false);
            ApplyFlatInputStyle(textField);
            pathContainer.Add(textField);

            // 文件夹按钮
            var browseBtn = new Button(() =>
            {
                var folderPath = EditorUtility.OpenFolderPanel(labelText, initialValue, string.Empty);
                if (!string.IsNullOrEmpty(folderPath))
                {
                    var idx = folderPath.IndexOf(ASSETS, StringComparison.Ordinal);
                    var newPath = idx >= 0 ? folderPath[idx..] : folderPath;
                    onPathChanged?.Invoke(newPath);
                }
            });
            browseBtn.style.width = 32;
            browseBtn.style.height = INPUT_HEIGHT;
            browseBtn.style.marginLeft = Spacing.XS;
            browseBtn.style.paddingLeft = 0;
            browseBtn.style.paddingRight = 0;
            browseBtn.style.paddingTop = 0;
            browseBtn.style.paddingBottom = 0;
            browseBtn.style.alignItems = Align.Center;
            browseBtn.style.justifyContent = Justify.Center;

            var folderIcon = new Image { image = KitIcons.GetTexture(KitIcons.FOLDER_DOCS) };
            folderIcon.style.width = ICON_SIZE;
            folderIcon.style.height = ICON_SIZE;
            browseBtn.Add(folderIcon);

            pathContainer.Add(browseBtn);
            row.Add(pathContainer);

            return row;
        }

        #endregion

        #region 辅助方法 - 下拉框创建

        /// <summary>
        /// 创建程序集下拉框
        /// </summary>
        private DropdownField CreateAssemblyDropdown()
        {
            mAssemblyNames = GetAvailableAssemblies();

            int currentIndex = mAssemblyNames.IndexOf(AssemblyName);
            if (currentIndex < 0)
            {
                mAssemblyNames.Insert(0, AssemblyName);
                currentIndex = 0;
            }

            mAssemblyDropdown = new DropdownField(mAssemblyNames, currentIndex);
            mAssemblyDropdown.RegisterValueChangedCallback(evt =>
            {
                AssemblyName = evt.newValue;
                UIKitCreateConfig.Instance.SaveConfig();
            });
            return mAssemblyDropdown;
        }

        /// <summary>
        /// 创建模板下拉框
        /// </summary>
        private DropdownField CreateTemplateDropdown()
        {
            mTemplateNames = GetAvailableTemplates();

            var currentTemplateName = UICodeGenTemplateRegistry.ActiveTemplateName;
            int currentIndex = mTemplateNames.IndexOf(currentTemplateName);
            if (currentIndex < 0) currentIndex = 0;

            mTemplateDropdown = new DropdownField(mTemplateNames, currentIndex);
            mTemplateDropdown.RegisterValueChangedCallback(evt =>
            {
                UICodeGenTemplateRegistry.SetActiveTemplate(evt.newValue);
                UIKitCreateConfig.Instance.SaveConfig();
            });
            return mTemplateDropdown;
        }

        /// <summary>
        /// 获取所有可用的程序集名称
        /// </summary>
        private List<string> GetAvailableAssemblies()
        {
            var assemblies = new List<string>();
            var unityAssemblies = CompilationPipeline.GetAssemblies(AssembliesType.Player);

            foreach (var assembly in unityAssemblies)
            {
                if (IsUserAssembly(assembly.name))
                {
                    assemblies.Add(assembly.name);
                }
            }

            assemblies.Sort((a, b) =>
            {
                if (a == "Assembly-CSharp") return -1;
                if (b == "Assembly-CSharp") return 1;
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            return assemblies;
        }

        /// <summary>
        /// 判断是否为用户程序集
        /// </summary>
        private bool IsUserAssembly(string assemblyName)
        {
            if (assemblyName.StartsWith("Unity.", StringComparison.Ordinal) ||
                assemblyName.StartsWith("UnityEngine", StringComparison.Ordinal) ||
                assemblyName.StartsWith("UnityEditor", StringComparison.Ordinal))
                return false;

            if (assemblyName.StartsWith("DOTween", StringComparison.Ordinal) ||
                assemblyName.StartsWith("UniTask", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Cysharp", StringComparison.Ordinal) ||
                assemblyName.StartsWith("FMOD", StringComparison.Ordinal) ||
                assemblyName.StartsWith("Yoo", StringComparison.Ordinal))
                return false;

            if (assemblyName.Contains(".Tests") || assemblyName.Contains(".Editor"))
                return false;

            return true;
        }

        /// <summary>
        /// 获取所有可用的代码生成模板
        /// </summary>
        private List<string> GetAvailableTemplates()
        {
            var templates = new List<string>();

            foreach (var name in UICodeGenTemplateRegistry.GetAllTemplateNames())
            {
                templates.Add(name);
            }

            templates.Sort((a, b) =>
            {
                if (a == UICodeGenTemplateRegistry.DEFAULT_TEMPLATE_NAME) return -1;
                if (b == UICodeGenTemplateRegistry.DEFAULT_TEMPLATE_NAME) return 1;
                return string.Compare(a, b, StringComparison.Ordinal);
            });

            return templates;
        }

        #endregion
    }
}
#endif
