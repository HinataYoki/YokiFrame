#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// UIDynamicElement 自定义 Inspector 编辑器
    /// </summary>
    [CustomEditor(typeof(UIDynamicElement))]
    [CanEditMultipleObjects]
    public class UIDynamicElementEditor : Editor
    {
        #region SerializedProperties

        private SerializedProperty mEnableRaycastProp;
        private SerializedProperty mAutoInitializeProp;

        #endregion

        private void OnEnable()
        {
            mEnableRaycastProp = serializedObject.FindProperty("mEnableRaycast");
            mAutoInitializeProp = serializedObject.FindProperty("mAutoInitialize");
        }

        public override VisualElement CreateInspectorGUI()
        {
            var root = new VisualElement();
            root.AddToClassList("dynamic-element-inspector");
            
            // 加载样式
            var styleSheet = YokiFrameEditorUtility.LoadStyleSheetByName("UIDynamicElementStyles");
            if (styleSheet != null)
            {
                root.styleSheets.Add(styleSheet);
            }
            
            // 主区块
            var section = CreateMainSection();
            root.Add(section);
            
            // 状态信息区块
            var statusSection = CreateStatusSection();
            root.Add(statusSection);
            
            return root;
        }

        /// <summary>
        /// 创建主配置区块
        /// </summary>
        private VisualElement CreateMainSection()
        {
            var section = new VisualElement();
            section.AddToClassList("dynamic-section");
            
            // 标题
            var header = new Label("动态元素配置");
            header.AddToClassList("dynamic-section-header");
            section.Add(header);
            
            // 内容
            var content = new VisualElement();
            content.AddToClassList("dynamic-section-content");
            
            // 帮助提示
            var helpBox = CreateHelpBox(
                "将此组件添加到频繁更新的 UI 元素上，自动创建嵌套 Canvas 隔离重建。\n" +
                "适用于：血条、计时器、进度条等实时更新的元素。"
            );
            content.Add(helpBox);
            
            // 射线检测开关
            var raycastRow = CreateFieldRow(
                "启用射线检测",
                "是否需要接收点击事件（有按钮等交互元素时启用）",
                mEnableRaycastProp
            );
            content.Add(raycastRow);
            
            // 自动初始化开关
            var autoInitRow = CreateFieldRow(
                "自动初始化",
                "是否在 Awake 时自动创建嵌套 Canvas（禁用后需手动调用 Initialize）",
                mAutoInitializeProp
            );
            content.Add(autoInitRow);
            
            section.Add(content);
            return section;
        }

        /// <summary>
        /// 创建状态信息区块
        /// </summary>
        private VisualElement CreateStatusSection()
        {
            var section = new VisualElement();
            section.AddToClassList("dynamic-section");
            section.AddToClassList("dynamic-section-status");
            
            // 标题
            var header = new Label("运行时状态");
            header.AddToClassList("dynamic-section-header");
            section.Add(header);
            
            // 内容
            var content = new VisualElement();
            content.AddToClassList("dynamic-section-content");
            
            var dynamicElement = target as UIDynamicElement;
            
            // 初始化状态
            bool isInitialized = Application.isPlaying && dynamicElement != null && dynamicElement.IsInitialized;
            
            var statusRow = new VisualElement();
            statusRow.style.flexDirection = FlexDirection.Row;
            statusRow.style.alignItems = Align.Center;
            
            var statusIcon = new Image 
            { 
                image = KitIcons.GetTexture(isInitialized ? KitIcons.SUCCESS : KitIcons.CLOCK) 
            };
            statusIcon.style.width = 14;
            statusIcon.style.height = 14;
            statusIcon.style.marginRight = 4;
            statusRow.Add(statusIcon);
            
            var statusLabel = new Label(isInitialized ? "已初始化" : "未初始化（运行时自动初始化）");
            statusLabel.AddToClassList("dynamic-status-label");
            statusLabel.AddToClassList(isInitialized ? "status-initialized" : "status-pending");
            statusRow.Add(statusLabel);
            
            content.Add(statusRow);
            
            // Canvas 信息
            if (Application.isPlaying && dynamicElement != null && dynamicElement.Canvas != null)
            {
                var canvasInfo = new Label($"嵌套 Canvas: {dynamicElement.Canvas.name}");
                canvasInfo.AddToClassList("dynamic-canvas-info");
                content.Add(canvasInfo);
            }
            
            section.Add(content);
            return section;
        }

        #region UI 辅助方法

        /// <summary>
        /// 创建帮助提示框
        /// </summary>
        private VisualElement CreateHelpBox(string message)
        {
            var helpBox = new VisualElement();
            helpBox.AddToClassList("dynamic-helpbox");
            
            var icon = new Image { image = KitIcons.GetTexture(KitIcons.INFO) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.AddToClassList("dynamic-helpbox-icon");
            helpBox.Add(icon);
            
            var text = new Label(message);
            text.AddToClassList("dynamic-helpbox-text");
            helpBox.Add(text);
            
            return helpBox;
        }

        /// <summary>
        /// 创建字段行
        /// </summary>
        private VisualElement CreateFieldRow(string label, string tooltip, SerializedProperty property)
        {
            var row = new VisualElement();
            row.AddToClassList("dynamic-field-row");
            
            var labelElement = new Label(label);
            labelElement.AddToClassList("dynamic-field-label");
            labelElement.tooltip = tooltip;
            row.Add(labelElement);
            
            var field = new PropertyField(property, "");
            field.AddToClassList("dynamic-field");
            field.BindProperty(property);
            row.Add(field);
            
            return row;
        }

        #endregion
    }
}
#endif
