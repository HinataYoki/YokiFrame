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
    /// UIPanelInspector - UI 辅助方法
    /// </summary>
    public partial class UIPanelInspector
    {
        /// <summary>
        /// 创建打开代码按钮
        /// </summary>
        private VisualElement CreateOpenCodeButton(UIPanel panel)
        {
            // 获取代码文件路径
            string panelName = panel.gameObject.name;
            string scriptPath = GetPanelScriptPath(panelName);
            
            // 检查文件是否存在
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
            btn.tooltip = $"在 IDE 中打开 {panelName}.cs";
            
            // 代码图标
            var icon = new Image { image = KitIcons.GetTexture(KitIcons.CODE) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.marginRight = 4;
            btn.Add(icon);
            
            var label = new Label("打开代码");
            label.style.fontSize = 11;
            btn.Add(label);
            
            return btn;
        }
        
        /// <summary>
        /// 获取 Panel 脚本文件路径
        /// </summary>
        private static string GetPanelScriptPath(string panelName)
        {
            var config = UIKitCreateConfig.Instance;
            if (config == default) return null;
            
            return $"{config.ScriptGeneratePath}/{panelName}/{panelName}{UICodeGenConstants.SCRIPT_SUFFIX}";
        }
        
        /// <summary>
        /// 在 IDE 中打开脚本文件
        /// </summary>
        private static void OpenScriptInIDE(string scriptPath)
        {
            var scriptAsset = AssetDatabase.LoadAssetAtPath<MonoScript>(scriptPath);
            if (scriptAsset != default)
            {
                // 使用 Unity 的默认方式打开脚本（会使用配置的外部编辑器）
                AssetDatabase.OpenAsset(scriptAsset);
            }
            else
            {
                Debug.LogWarning($"[UIKit] 无法加载脚本文件: {scriptPath}");
            }
        }

        /// <summary>
        /// 创建区块容器
        /// </summary>
        private VisualElement CreateSection(string title, string className)
        {
            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList(className);
            
            // 标题
            var header = new Label(title);
            header.AddToClassList("uipanel-section-header");
            section.Add(header);
            
            // 内容容器
            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            section.Add(content);
            
            return section;
        }

        /// <summary>
        /// 创建帮助提示框
        /// </summary>
        private VisualElement CreateHelpBox(string message)
        {
            var helpBox = new VisualElement();
            helpBox.AddToClassList("uipanel-helpbox");
            
            // 图标
            var icon = new Image { image = KitIcons.GetTexture(KitIcons.INFO) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.AddToClassList("uipanel-helpbox-icon");
            helpBox.Add(icon);
            
            // 文本
            var text = new Label(message);
            text.AddToClassList("uipanel-helpbox-text");
            helpBox.Add(text);
            
            return helpBox;
        }

        /// <summary>
        /// 创建字段行（带标签和提示）
        /// </summary>
        private VisualElement CreateFieldRow(string label, string tooltip, SerializedProperty property)
        {
            var row = new VisualElement();
            row.AddToClassList("uipanel-field-row");
            
            // 标签容器
            var labelContainer = new VisualElement();
            labelContainer.AddToClassList("uipanel-label-container");
            
            var labelElement = new Label(label);
            labelElement.AddToClassList("uipanel-field-label");
            labelElement.tooltip = tooltip;
            labelContainer.Add(labelElement);
            
            row.Add(labelContainer);
            
            // 字段
            var field = new PropertyField(property, "");
            field.AddToClassList("uipanel-field");
            field.BindProperty(property);
            row.Add(field);
            
            return row;
        }
    }
}
#endif
