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
        #region SerializedProperties
        
        private SerializedProperty mShowAnimationConfigProp;
        private SerializedProperty mHideAnimationConfigProp;
        private SerializedProperty mDefaultSelectableProp;
        
        #endregion

        #region UI 元素缓存
        
        private VisualElement mRoot;
        private VisualElement mLastSection;
        
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
            
            // 标记最后一个区块
            if (mLastSection != null)
            {
                mLastSection.AddToClassList("last-section");
            }
            
            return mRoot;
        }
    }
}
#endif
