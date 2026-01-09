#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
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
