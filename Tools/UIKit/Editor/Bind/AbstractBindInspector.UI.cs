#if UNITY_EDITOR
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// AbstractBindInspector - UI 创建方法
    /// </summary>
    public partial class AbstractBindInspector
    {
        /// <summary>
        /// 创建命名建议行
        /// </summary>
        private void CreateSuggestionRow(VisualElement parent)
        {
            mSuggestionRow = new VisualElement();
            mSuggestionRow.AddToClassList("bind-row");
            mSuggestionRow.AddToClassList("suggestion-row");
            
            var labelContainer = new VisualElement();
            labelContainer.style.flexDirection = FlexDirection.Row;
            labelContainer.style.alignItems = Align.Center;
            labelContainer.AddToClassList("bind-label");
            
            var icon = new Image { image = KitIcons.GetTexture(KitIcons.TIP) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.marginRight = 4;
            labelContainer.Add(icon);
            
            var label = new Label("建议");
            labelContainer.Add(label);
            mSuggestionRow.Add(labelContainer);
            
            var contentContainer = new VisualElement();
            contentContainer.AddToClassList("bind-field");
            contentContainer.AddToClassList("suggestion-content");
            
            mSuggestionLabel = new Label();
            mSuggestionLabel.AddToClassList("suggestion-text");
            contentContainer.Add(mSuggestionLabel);
            
            mApplySuggestionBtn = new Button(OnApplySuggestion) { text = "应用" };
            mApplySuggestionBtn.AddToClassList("suggestion-apply-btn");
            contentContainer.Add(mApplySuggestionBtn);
            
            mSuggestionRow.Add(contentContainer);
            parent.Add(mSuggestionRow);
        }
        
        /// <summary>
        /// 创建绑定路径显示行
        /// </summary>
        private void CreateBindPathRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("bind-row");
            row.AddToClassList("bind-path-row");
            
            var labelContainer = new VisualElement();
            labelContainer.style.flexDirection = FlexDirection.Row;
            labelContainer.style.alignItems = Align.Center;
            labelContainer.AddToClassList("bind-label");
            
            var icon = new Image { image = KitIcons.GetTexture(KitIcons.LOCATION) };
            icon.style.width = 14;
            icon.style.height = 14;
            icon.style.marginRight = 4;
            labelContainer.Add(icon);
            
            var label = new Label("路径");
            labelContainer.Add(label);
            row.Add(labelContainer);

            mBindPathLabel = new Label();
            mBindPathLabel.AddToClassList("bind-field");
            mBindPathLabel.AddToClassList("bind-path-text");
            row.Add(mBindPathLabel);
            
            parent.Add(row);
        }
        
        /// <summary>
        /// 创建代码预览区域
        /// </summary>
        private void CreateCodePreviewSection(VisualElement parent)
        {
            mCodePreviewFoldout = new Foldout { text = "代码预览", value = false };
            mCodePreviewFoldout.AddToClassList("code-preview-foldout");
            
            mCodePreviewLabel = new Label();
            mCodePreviewLabel.AddToClassList("code-preview-text");
            mCodePreviewFoldout.Add(mCodePreviewLabel);
            
            parent.Add(mCodePreviewFoldout);
        }
        
        /// <summary>
        /// 创建跳转到代码按钮
        /// </summary>
        private void CreateJumpToCodeButton(VisualElement parent)
        {
            mJumpToCodeBtn = new Button(OnJumpToCode) { text = "跳转到代码" };
            mJumpToCodeBtn.AddToClassList("jump-to-code-btn");
            parent.Add(mJumpToCodeBtn);
        }

        private void CreateBindTypeRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("bind-row");
            
            var label = new Label("绑定类型");
            label.AddToClassList("bind-label");
            row.Add(label);
            
            mBindTypeField = new EnumField((BindType)mBindProp.enumValueIndex);
            mBindTypeField.AddToClassList("bind-field");
            mBindTypeField.RegisterValueChangedCallback(OnBindTypeChanged);
            row.Add(mBindTypeField);
            
            parent.Add(row);
            
            // 类型快速切换按钮行
            CreateTypeConvertRow(parent);
        }

        /// <summary>
        /// 创建类型快速切换按钮行
        /// </summary>
        private void CreateTypeConvertRow(VisualElement parent)
        {
            mTypeConvertRow = new VisualElement();
            mTypeConvertRow.AddToClassList("bind-row");
            mTypeConvertRow.AddToClassList("type-convert-row");
            
            var label = new Label("快速转换");
            label.AddToClassList("bind-label");
            mTypeConvertRow.Add(label);
            
            var btnContainer = new VisualElement();
            btnContainer.AddToClassList("bind-field");
            btnContainer.AddToClassList("type-convert-buttons");
            
            var btnMember = new Button(() => OnQuickConvert(BindType.Member)) { text = "→ Member" };
            btnMember.AddToClassList("type-convert-btn");
            btnMember.name = "btn-to-member";
            btnContainer.Add(btnMember);
            
            var btnElement = new Button(() => OnQuickConvert(BindType.Element)) { text = "→ Element" };
            btnElement.AddToClassList("type-convert-btn");
            btnElement.name = "btn-to-element";
            btnContainer.Add(btnElement);
            
            var btnComponent = new Button(() => OnQuickConvert(BindType.Component)) { text = "→ Component" };
            btnComponent.AddToClassList("type-convert-btn");
            btnComponent.name = "btn-to-component";
            btnContainer.Add(btnComponent);
            
            mTypeConvertRow.Add(btnContainer);
            parent.Add(mTypeConvertRow);
            
            UpdateTypeConvertButtons((BindType)mBindProp.enumValueIndex);
        }
        
        private VisualElement CreateNameRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("bind-row");
            
            var label = new Label("字段名称");
            label.AddToClassList("bind-label");
            row.Add(label);
            
            // 初始化名称
            var targetBind = target as AbstractBind;
            if (string.IsNullOrEmpty(mNameProp.stringValue) && targetBind != null)
            {
                mNameProp.stringValue = targetBind.name;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }

            mNameField = new TextField();
            mNameField.AddToClassList("bind-field");
            mNameField.value = mNameProp.stringValue;
            mNameField.RegisterValueChangedCallback(OnNameChanged);
            row.Add(mNameField);
            
            parent.Add(row);
            return row;
        }
        
        private VisualElement CreateTypeRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("bind-row");
            
            var label = new Label("类型");
            label.AddToClassList("bind-label");
            row.Add(label);
            
            var fieldContainer = new VisualElement();
            fieldContainer.AddToClassList("bind-field");
            fieldContainer.style.flexDirection = FlexDirection.Column;
            
            var targetBind = target as AbstractBind;
            var bindType = (BindType)mBindProp.enumValueIndex;
            var needsApply = false;
            
            // 自定义类型输入框（Element/Component 使用）
            if (string.IsNullOrEmpty(mCustomTypeProp.stringValue) && targetBind != null)
            {
                mCustomTypeProp.stringValue = targetBind.name;
                needsApply = true;
            }
            
            mCustomTypeField = new TextField();
            mCustomTypeField.AddToClassList("bind-type-field");
            mCustomTypeField.value = mCustomTypeProp.stringValue;
            mCustomTypeField.RegisterValueChangedCallback(OnCustomTypeChanged);
            fieldContainer.Add(mCustomTypeField);
            
            // 组件下拉列表（Member 使用）
            if (mComponentNames.Count > 0)
            {
                // 初始化 autoType 和 type（Member 类型）
                if (string.IsNullOrEmpty(mAutoTypeProp.stringValue))
                {
                    mAutoTypeProp.stringValue = mComponentNames[mComponentNameIndex];
                    needsApply = true;
                }

                mComponentPopup = new PopupField<string>(
                    mComponentNames, 
                    mComponentNameIndex,
                    FormatComponentName,
                    FormatComponentName
                );
                mComponentPopup.AddToClassList("bind-type-field");
                mComponentPopup.RegisterValueChangedCallback(OnComponentSelected);
                fieldContainer.Add(mComponentPopup);
            }
            else
            {
                var noComponentLabel = new Label("无可用组件");
                noComponentLabel.AddToClassList("bind-no-component");
                fieldContainer.Add(noComponentLabel);
            }
            
            // 同步 type 字段（根据当前绑定类型）
            if (string.IsNullOrEmpty(mTypeProp.stringValue))
            {
                if (bindType == BindType.Member && mComponentNames.Count > 0)
                {
                    mTypeProp.stringValue = mAutoTypeProp.stringValue;
                }
                else if (bindType is BindType.Element or BindType.Component)
                {
                    mTypeProp.stringValue = mCustomTypeProp.stringValue;
                }
                needsApply = true;
            }
            
            if (needsApply)
            {
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
            
            row.Add(fieldContainer);
            parent.Add(row);
            return row;
        }
        
        private VisualElement CreateCommentRow(VisualElement parent)
        {
            var row = new VisualElement();
            row.AddToClassList("bind-row");
            
            var label = new Label("注释");
            label.AddToClassList("bind-label");
            row.Add(label);
            
            mCommentField = new TextField();
            mCommentField.AddToClassList("bind-field");
            mCommentField.value = mCommentProp.stringValue;
            mCommentField.RegisterValueChangedCallback(OnCommentChanged);
            row.Add(mCommentField);
            
            parent.Add(row);
            return row;
        }
    }
}
#endif
