#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// UIPanel 自定义 Inspector 编辑器
    /// 使用 UI Toolkit 重绘，提供更友好的中文界面
    /// </summary>
    /// <remarks>
    /// 文件结构：
    /// - UIPanelInspector.cs（主文件）：字段定义和入口方法
    /// - UIPanelInspector.PanelConfig.cs：面板配置区块（动画、焦点）
    /// - UIPanelInspector.BindTree.cs：绑定树区块
    /// - UIPanelInspector.TreeNode.cs：节点渲染
    /// - UIPanelInspector.Validation.cs：验证逻辑
    /// - UIPanelInspector.Helpers.cs：UI 辅助方法
    /// - UIPanelInspector.Legacy.cs：旧版兼容方法
    /// </remarks>
    [CustomEditor(typeof(UIPanel), true)]
    [CanEditMultipleObjects]
    public partial class UIPanelInspector : Editor
    {
        #region 折叠状态 Key 常量
        
        private const string KEY_PANEL_CONFIG_FOLDOUT = "YokiFrame.UIPanelInspector.PanelConfigFoldout";
        private const string KEY_BIND_TREE_FOLDOUT = "YokiFrame.UIPanelInspector.BindTreeFoldout";
        private const string KEY_CUSTOM_PROPS_FOLDOUT = "YokiFrame.UIPanelInspector.CustomPropsFoldout";
        
        #endregion
        
        #region SerializedProperties
        
        private SerializedProperty mShowAnimationConfigProp;
        private SerializedProperty mHideAnimationConfigProp;
        private SerializedProperty mDefaultSelectableProp;
        
        #endregion

        #region UI 元素缓存
        
        private VisualElement mRoot;
        private VisualElement mLastSection;
        
        // Foldout 缓存（用于绑定状态变更事件）
        private Foldout mPanelConfigFoldout;
        private Foldout mCustomPropsFoldout;
        
        // 绑定树相关
        private Foldout mBindTreeFoldout;
        private VisualElement mBindTreeContainer;
        private Label mBindStatsLabel;
        private Label mValidationSummaryLabel;
        
        // 节点折叠状态（key: 节点路径）
        private HashSet<string> mCollapsedNodes = new(16);
        
        // 缓存的验证结果（避免重复验证）
        private List<BindValidationResult> mCachedValidationResults;
        
        #endregion

        private void OnEnable()
        {
            mShowAnimationConfigProp = serializedObject.FindProperty("mShowAnimationConfig");
            mHideAnimationConfigProp = serializedObject.FindProperty("mHideAnimationConfig");
            mDefaultSelectableProp = serializedObject.FindProperty("mDefaultSelectable");
        }

        public override VisualElement CreateInspectorGUI()
        {
            mRoot = new VisualElement();
            mRoot.AddToClassList("uipanel-inspector");
            
            // 加载样式
            var styleSheet = YokiFrameEditorUtility.LoadStyleSheetByName("UIPanelInspectorStyles");
            if (styleSheet != null)
            {
                mRoot.styleSheets.Add(styleSheet);
            }
            
            // 面板配置区块（包含动画和焦点配置）
            bool hasAnimConfig = mShowAnimationConfigProp != null || mHideAnimationConfigProp != null;
            bool hasFocusConfig = mDefaultSelectableProp != null;
            if (hasAnimConfig || hasFocusConfig)
            {
                CreatePanelConfigSection(hasAnimConfig, hasFocusConfig);
            }
            
            // 绑定关系区块
            CreateBindTreeSection();
            
            // 子类自定义属性区块
            CreateCustomPropertiesSection();
            
            // 标记最后一个区块
            if (mLastSection != null)
            {
                mLastSection.AddToClassList("last-section");
            }
            
            return mRoot;
        }
        
        /// <summary>
        /// 创建子类自定义属性区块
        /// 显示子类中标记了 [SerializeField] 且不在基类中的字段
        /// </summary>
        private void CreateCustomPropertiesSection()
        {
            // 获取基类定义的属性名（排除这些）
            var basePropertyNames = new HashSet<string>
            {
                "m_Script",
                "mShowAnimationConfig",
                "mHideAnimationConfig",
                "mDefaultSelectable"
            };
            
            // 收集子类自定义属性
            var customProperties = new List<SerializedProperty>();
            var iterator = serializedObject.GetIterator();
            
            if (iterator.NextVisible(true))
            {
                do
                {
                    // 跳过基类属性和 Designer 生成的属性
                    if (basePropertyNames.Contains(iterator.name))
                        continue;
                    
                    // 跳过 m_Script
                    if (iterator.name == "m_Script")
                        continue;
                    
                    // 检查是否是 Designer 生成的属性（通常是 UIElement 类型）
                    if (IsDesignerGeneratedProperty(iterator))
                        continue;
                    
                    customProperties.Add(iterator.Copy());
                }
                while (iterator.NextVisible(false));
            }
            
            // 如果没有自定义属性，不创建区块
            if (customProperties.Count == 0)
                return;
            
            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList("uipanel-section-custom");
            
            // 可折叠标题（从 SessionState 恢复折叠状态）
            bool savedFoldoutState = SessionState.GetBool(KEY_CUSTOM_PROPS_FOLDOUT, true);
            mCustomPropsFoldout = new Foldout { text = "自定义属性", value = savedFoldoutState };
            mCustomPropsFoldout.AddToClassList("uipanel-custom-foldout");
            
            // 注册折叠状态变更回调，保存到 SessionState
            mCustomPropsFoldout.RegisterValueChangedCallback(evt =>
            {
                SessionState.SetBool(KEY_CUSTOM_PROPS_FOLDOUT, evt.newValue);
            });
            
            section.Add(mCustomPropsFoldout);
            
            // 内容容器
            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            mCustomPropsFoldout.Add(content);
            
            // 添加所有自定义属性
            for (int i = 0; i < customProperties.Count; i++)
            {
                var prop = customProperties[i];
                var field = new UnityEditor.UIElements.PropertyField(prop);
                field.AddToClassList("uipanel-custom-field");
                content.Add(field);
            }
            
            mRoot.Add(section);
            mLastSection = section;
        }
        
        /// <summary>
        /// 判断属性是否是 Designer 生成的（UIElement/UIComponent 类型）
        /// </summary>
        private bool IsDesignerGeneratedProperty(SerializedProperty property)
        {
            // 检查类型名是否包含 UIElement 或 UIComponent
            var typeName = property.type;
            if (string.IsNullOrEmpty(typeName))
                return false;
            
            // Designer 生成的属性通常是 UIElement 子类或 mData
            if (typeName.Contains("UIElement") || typeName.Contains("UIComponent"))
                return true;
            
            // mData 字段
            if (property.name == "mData")
                return true;
            
            return false;
        }
    }
}
#endif
