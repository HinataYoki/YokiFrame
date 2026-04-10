#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// <see cref="UIPanel"/> 的自定义检视器入口。
    /// 使用 UI Toolkit 组织面板配置、绑定树查看以及自定义序列化属性区域。
    /// </summary>
    /// <remarks>
    /// 文件拆分说明：
    /// - <c>UIPanelInspector.cs</c>：检视器入口、字段缓存、区块组装
    /// - <c>UIPanelInspector.PanelConfig.cs</c>：面板配置，例如动画与焦点
    /// - <c>UIPanelInspector.BindTree.cs</c>：绑定树区块
    /// - <c>UIPanelInspector.TreeNode.cs</c>：绑定树节点渲染
    /// - <c>UIPanelInspector.Validation.cs</c>：校验汇总逻辑
    /// - <c>UIPanelInspector.Helpers.cs</c>：共享辅助方法
    /// </remarks>
    [CustomEditor(typeof(UIPanel), true)]
    [CanEditMultipleObjects]
    public partial class UIPanelInspector : Editor
    {
        #region Foldout Keys

        private const string KEY_PANEL_CONFIG_FOLDOUT = "YokiFrame.UIPanelInspector.PanelConfigFoldout";
        private const string KEY_BIND_TREE_FOLDOUT = "YokiFrame.UIPanelInspector.BindTreeFoldout";
        private const string KEY_CUSTOM_PROPS_FOLDOUT = "YokiFrame.UIPanelInspector.CustomPropsFoldout";

        #endregion

        #region Serialized Properties

        private SerializedProperty mShowAnimationConfigProp;
        private SerializedProperty mHideAnimationConfigProp;
        private SerializedProperty mDefaultSelectableProp;

        #endregion

        #region UI Element Cache

        private VisualElement mRoot;
        private VisualElement mLastSection;

        private Foldout mPanelConfigFoldout;
        private Foldout mCustomPropsFoldout;

        private Foldout mBindTreeFoldout;
        private VisualElement mBindTreeContainer;
        private Label mBindStatsLabel;
        private Label mValidationSummaryLabel;

        private readonly HashSet<string> mCollapsedNodes = new(16);
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

            var styleSheet = YokiFrameEditorUtility.LoadStyleSheetByName("UIPanelInspectorStyles");
            if (styleSheet != null)
            {
                mRoot.styleSheets.Add(styleSheet);
            }

            bool hasAnimConfig = mShowAnimationConfigProp != null || mHideAnimationConfigProp != null;
            bool hasFocusConfig = mDefaultSelectableProp != null;
            if (hasAnimConfig || hasFocusConfig)
            {
                CreatePanelConfigSection(hasAnimConfig, hasFocusConfig);
            }

            CreateBindTreeSection();
            CreateCustomPropertiesSection();

            if (mLastSection != null)
            {
                mLastSection.AddToClassList("last-section");
            }

            return mRoot;
        }

        /// <summary>
        /// 创建自定义序列化属性区块。
        /// 会过滤公共基础字段与设计器自动生成字段，只保留面板自身需要暴露的属性。
        /// </summary>
        private void CreateCustomPropertiesSection()
        {
            var basePropertyNames = new HashSet<string>
            {
                "m_Script",
                "mShowAnimationConfig",
                "mHideAnimationConfig",
                "mDefaultSelectable"
            };

            var customProperties = new List<SerializedProperty>();
            var iterator = serializedObject.GetIterator();

            if (iterator.NextVisible(true))
            {
                do
                {
                    if (basePropertyNames.Contains(iterator.name))
                        continue;

                    if (iterator.name == "m_Script")
                        continue;

                    if (IsDesignerGeneratedProperty(iterator))
                        continue;

                    customProperties.Add(iterator.Copy());
                }
                while (iterator.NextVisible(false));
            }

            if (customProperties.Count == 0)
                return;

            var section = new VisualElement();
            section.AddToClassList("uipanel-section");
            section.AddToClassList("uipanel-section-custom");

            bool savedFoldoutState = SessionState.GetBool(KEY_CUSTOM_PROPS_FOLDOUT, true);
            mCustomPropsFoldout = new Foldout { text = "自定义属性", value = savedFoldoutState };
            mCustomPropsFoldout.AddToClassList("uipanel-custom-foldout");

            mCustomPropsFoldout.RegisterValueChangedCallback(evt =>
            {
                SessionState.SetBool(KEY_CUSTOM_PROPS_FOLDOUT, evt.newValue);
            });

            section.Add(mCustomPropsFoldout);

            var content = new VisualElement();
            content.AddToClassList("uipanel-section-content");
            mCustomPropsFoldout.Add(content);

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
        /// 判断当前属性是否属于 UIKit 设计器自动生成的字段。
        /// </summary>
        private bool IsDesignerGeneratedProperty(SerializedProperty property)
        {
            var typeName = property.type;
            if (string.IsNullOrEmpty(typeName))
                return false;

            if (typeName.Contains("UIElement") || typeName.Contains("UIComponent"))
                return true;

            if (property.name == "mData")
                return true;

            return false;
        }
    }
}
#endif
