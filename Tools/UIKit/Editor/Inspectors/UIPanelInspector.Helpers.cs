#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="UIPanelInspector"/> 共用的辅助方法集合。
    /// </summary>
    public partial class UIPanelInspector
    {
        /// <summary>
        /// 创建“打开脚本”按钮，用于在外部编辑器中打开当前面板对应的生成脚本。
        /// </summary>
        private VisualElement CreateOpenCodeButton(UIPanel panel)
        {
            string panelName = panel.gameObject.name;
            string scriptPath = GetPanelScriptPath(panelName);

            if (string.IsNullOrEmpty(scriptPath) || !File.Exists(scriptPath))
            {
                return null;
            }

            var btn = new Button(() => OpenScriptInIDE(scriptPath));
            btn.style.flexDirection = FlexDirection.Row;
            btn.style.alignItems = Align.Center;
            btn.style.height = 22;
            btn.style.paddingLeft = 8;
            btn.style.paddingRight = 8;
            btn.style.marginRight = 4;
            btn.style.backgroundColor = new StyleColor(new Color(0.25f, 0.25f, 0.28f));
            btn.style.borderTopLeftRadius = 4;
            btn.style.borderTopRightRadius = 4;
            btn.style.borderBottomLeftRadius = 4;
            btn.style.borderBottomRightRadius = 4;
            btn.tooltip = $"在外部编辑器中打开 {panelName}.cs";

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.CODE) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.marginRight = 4;
            btn.Add(icon);

            var label = new Label("打开脚本");
            label.style.fontSize = 11;
            btn.Add(label);

            return btn;
        }

        /// <summary>
        /// 获取当前面板生成脚本的资源路径。
        /// </summary>
        private static string GetPanelScriptPath(string panelName)
        {
            var config = UIKitCreateConfig.Instance;
            if (config == default)
                return null;

            return $"{config.ScriptGeneratePath}/{panelName}/{panelName}{UICodeGenConstants.SCRIPT_SUFFIX}";
        }

        /// <summary>
        /// 通过 Unity 当前配置的外部编辑器打开指定脚本文件。
        /// </summary>
        private static void OpenScriptInIDE(string scriptPath)
        {
            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (scriptAsset != default)
            {
                AssetDatabase.OpenAsset(scriptAsset);
            }
            else
            {
                Debug.LogWarning($"[UIKit] 无法加载脚本资源：{scriptPath}");
            }
        }

        /// <summary>
        /// 创建带标题头的通用区块容器。
        /// </summary>
        private VisualElement CreateSection(string title, string className)
        {
            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList(className);

            var header = new Label(title);
            header.AddToClassList("uipanel-section-header");
            section.Add(header);

            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            section.Add(content);

            return section;
        }

        /// <summary>
        /// 创建带信息图标的轻量提示框。
        /// </summary>
        private VisualElement CreateHelpBox(string message)
        {
            var helpBox = new VisualElement();
            helpBox.AddToClassList("uipanel-helpbox");

            var icon = new Image { image = KitIcons.GetTexture(KitIcons.INFO) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.AddToClassList("uipanel-helpbox-icon");
            helpBox.Add(icon);

            var text = new Label(message);
            text.AddToClassList("uipanel-helpbox-text");
            helpBox.Add(text);

            return helpBox;
        }

        /// <summary>
        /// 创建带标签与提示信息的属性行。
        /// </summary>
        private VisualElement CreateFieldRow(string label, string tooltip, SerializedProperty property)
        {
            var row = new VisualElement();
            row.AddToClassList("uipanel-field-row");

            var labelContainer = new VisualElement();
            labelContainer.AddToClassList("uipanel-label-container");

            var labelElement = new Label(label);
            labelElement.AddToClassList("uipanel-field-label");
            labelElement.tooltip = tooltip;
            labelContainer.Add(labelElement);

            row.Add(labelContainer);

            var field = new PropertyField(property, string.Empty);
            field.AddToClassList("uipanel-field");
            field.BindProperty(property);
            row.Add(field);

            return row;
        }
    }
}
#endif
