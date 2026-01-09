#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AbstractBind 自定义 Inspector 编辑器
    /// 使用 UI Toolkit 重绘，提供更友好的中文界面
    /// </summary>
    /// <remarks>
    /// 文件结构：
    /// - AbstractBindInspector.cs（主文件）：字段定义和入口方法
    /// - AbstractBindInspector.UI.cs：UI 创建方法
    /// - AbstractBindInspector.Events.cs：事件处理
    /// - AbstractBindInspector.Validation.cs：验证和更新逻辑
    /// </remarks>
    [CustomEditor(typeof(AbstractBind), true)]
    [CanEditMultipleObjects]
    public partial class AbstractBindInspector : Editor
    {
        #region SerializedProperties
        
        private SerializedProperty mBindProp;
        private SerializedProperty mNameProp;
        private SerializedProperty mAutoTypeProp;
        private SerializedProperty mCustomTypeProp;
        private SerializedProperty mTypeProp;
        private SerializedProperty mCommentProp;
        
        #endregion
        
        #region 缓存
        
        // 缓存组件列表（避免 LINQ 分配）
        private readonly List<string> mComponentNames = new(16);
        private int mComponentNameIndex;
        
        #endregion
        
        #region UI 元素缓存
        
        private VisualElement mRoot;
        private VisualElement mNameRow;
        private VisualElement mTypeRow;
        private VisualElement mCommentRow;
        private Label mValidationLabel;
        private EnumField mBindTypeField;
        private TextField mNameField;
        private TextField mCustomTypeField;
        private PopupField<string> mComponentPopup;
        private TextField mCommentField;
        
        // 新增 UI 元素
        private VisualElement mTypeConvertRow;
        private Label mBindPathLabel;
        private Foldout mCodePreviewFoldout;
        private Label mCodePreviewLabel;
        private VisualElement mSuggestionRow;
        private Label mSuggestionLabel;
        private Button mApplySuggestionBtn;
        private Button mJumpToCodeBtn;
        
        #endregion

        private void OnEnable()
        {
            mBindProp = serializedObject.FindProperty("bind");
            mNameProp = serializedObject.FindProperty("mName");
            mAutoTypeProp = serializedObject.FindProperty("autoType");
            mCustomTypeProp = serializedObject.FindProperty("customType");
            mTypeProp = serializedObject.FindProperty("type");
            mCommentProp = serializedObject.FindProperty("comment");
            
            CacheComponentNames();
        }
        
        private void CacheComponentNames()
        {
            mComponentNames.Clear();
            
            var targetBind = target as AbstractBind;
            if (targetBind == null) return;
            
            var components = targetBind.GetComponents<UnityEngine.Component>();
            for (int i = 0; i < components.Length; i++)
            {
                var comp = components[i];
                if (comp != null && comp is not AbstractBind)
                {
                    mComponentNames.Add(comp.GetType().FullName);
                }
            }
            
            // 查找当前选中的索引
            mComponentNameIndex = 0;
            var currentType = mAutoTypeProp?.stringValue;
            if (!string.IsNullOrEmpty(currentType))
            {
                for (int i = 0; i < mComponentNames.Count; i++)
                {
                    if (mComponentNames[i].Contains(currentType))
                    {
                        mComponentNameIndex = i;
                        break;
                    }
                }
            }
            
            // 默认选择最后一个（通常是最具体的组件）
            if (mComponentNameIndex == 0 && mComponentNames.Count > 0)
            {
                mComponentNameIndex = mComponentNames.Count - 1;
            }
        }

        public override VisualElement CreateInspectorGUI()
        {
            mRoot = new VisualElement();
            mRoot.AddToClassList("bind-inspector");
            
            // 加载样式
            var styleSheet = YokiFrameEditorUtility.LoadStyleSheetByName("BindInspectorStyles");
            if (styleSheet != null)
            {
                mRoot.styleSheets.Add(styleSheet);
            }
            
            // 主容器
            var container = new VisualElement();
            container.AddToClassList("bind-container");
            mRoot.Add(container);
            
            // 绑定类型
            CreateBindTypeRow(container);
            
            // 字段名称
            mNameRow = CreateNameRow(container);
            
            // 类型选择
            mTypeRow = CreateTypeRow(container);
            
            // 注释
            mCommentRow = CreateCommentRow(container);
            
            // 验证提示
            mValidationLabel = new Label();
            mValidationLabel.AddToClassList("validation-label");
            container.Add(mValidationLabel);
            
            // 命名建议行
            CreateSuggestionRow(container);
            
            // 绑定路径显示
            CreateBindPathRow(container);
            
            // 代码预览区域
            CreateCodePreviewSection(container);
            
            // 跳转到代码按钮
            CreateJumpToCodeButton(container);
            
            // 初始化可见性
            UpdateRowVisibility((BindType)mBindProp.enumValueIndex);
            ValidateFields();
            UpdateSuggestion();
            UpdateBindPath();
            UpdateCodePreview();
            UpdateJumpToCodeButton();
            
            return mRoot;
        }
        
        /// <summary>
        /// 格式化组件名称（只显示类名）
        /// </summary>
        private string FormatComponentName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;
            
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }
    }
}
#endif
