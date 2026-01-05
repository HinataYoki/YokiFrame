#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    [CustomEditor(typeof(AbstractBind), true)]
    [CanEditMultipleObjects]
    public class AbstractBindInspector : Editor
    {
        private const string STYLE_PATH = "Assets/YokiFrame/Tools/UIKit/Editor/Bind/BindInspectorStyles.uss";
        private static readonly Regex IDENTIFIER_REGEX = new(@"^[a-zA-Z_][a-zA-Z0-9_]*$", RegexOptions.Compiled);
        
        // SerializedProperties
        private SerializedProperty mBindProp;
        private SerializedProperty mNameProp;
        private SerializedProperty mAutoTypeProp;
        private SerializedProperty mCustomTypeProp;
        private SerializedProperty mTypeProp;
        private SerializedProperty mCommentProp;
        
        // 缓存组件列表（避免 LINQ 分配）
        private readonly List<string> mComponentNames = new(16);
        private int mComponentNameIndex;
        
        // UI 元素缓存
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
            
            var components = targetBind.GetComponents<Component>();
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
            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(STYLE_PATH);
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
            
            // 初始化可见性
            UpdateRowVisibility((BindType)mBindProp.enumValueIndex);
            ValidateFields();
            
            return mRoot;
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
        
        private string FormatComponentName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return fullName;
            
            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 ? fullName.Substring(lastDot + 1) : fullName;
        }

        #region Event Handlers
        
        private void OnBindTypeChanged(ChangeEvent<Enum> evt)
        {
            var newType = (BindType)evt.newValue;
            
            serializedObject.Update();
            mBindProp.enumValueIndex = (int)newType;
            
            // 切换类型时同步 type 字段
            if (newType == BindType.Member && mComponentNames.Count > 0)
            {
                if (string.IsNullOrEmpty(mAutoTypeProp.stringValue))
                {
                    mAutoTypeProp.stringValue = mComponentNames[mComponentNameIndex];
                }
                mTypeProp.stringValue = mAutoTypeProp.stringValue;
            }
            else if (newType is BindType.Element or BindType.Component)
            {
                mTypeProp.stringValue = mCustomTypeProp.stringValue;
            }
            
            serializedObject.ApplyModifiedProperties();
            
            UpdateRowVisibility(newType);
            ValidateFields();
        }
        
        private void OnNameChanged(ChangeEvent<string> evt)
        {
            var newName = evt.newValue;
            
            // 空值时使用 GameObject 名称
            var targetBind = target as AbstractBind;
            if (string.IsNullOrEmpty(newName) && targetBind != null)
            {
                newName = targetBind.name;
                mNameField.SetValueWithoutNotify(newName);
            }
            
            serializedObject.Update();
            mNameProp.stringValue = newName;
            serializedObject.ApplyModifiedProperties();
            
            ValidateFields();
        }
        
        private void OnCustomTypeChanged(ChangeEvent<string> evt)
        {
            serializedObject.Update();
            mCustomTypeProp.stringValue = evt.newValue;
            mTypeProp.stringValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
            
            ValidateFields();
        }
        
        private void OnComponentSelected(ChangeEvent<string> evt)
        {
            serializedObject.Update();
            mAutoTypeProp.stringValue = evt.newValue;
            mTypeProp.stringValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
        }
        
        private void OnCommentChanged(ChangeEvent<string> evt)
        {
            serializedObject.Update();
            mCommentProp.stringValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
        }
        
        #endregion
        
        #region Visibility & Validation
        
        private void UpdateRowVisibility(BindType bindType)
        {
            var isLeaf = bindType == BindType.Leaf;
            var isMember = bindType == BindType.Member;
            var isElementOrComponent = bindType is BindType.Element or BindType.Component;
            
            // 字段名称：非 Leaf 显示
            mNameRow.style.display = isLeaf ? DisplayStyle.None : DisplayStyle.Flex;
            
            // 类型行：非 Leaf 显示
            mTypeRow.style.display = isLeaf ? DisplayStyle.None : DisplayStyle.Flex;
            
            // 自定义类型输入框：Element/Component 显示
            if (mCustomTypeField != null)
            {
                mCustomTypeField.style.display = isElementOrComponent ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // 组件下拉列表：Member 显示
            if (mComponentPopup != null)
            {
                mComponentPopup.style.display = isMember ? DisplayStyle.Flex : DisplayStyle.None;
            }
            
            // 注释：非 Leaf 显示
            mCommentRow.style.display = isLeaf ? DisplayStyle.None : DisplayStyle.Flex;
            
            // 更新类型行标签
            var typeLabel = mTypeRow.Q<Label>();
            if (typeLabel != null)
            {
                typeLabel.text = isMember ? "组件列表" : "类名称";
            }
        }
        
        private void ValidateFields()
        {
            var bindType = (BindType)mBindProp.enumValueIndex;
            if (bindType == BindType.Leaf)
            {
                mValidationLabel.style.display = DisplayStyle.None;
                return;
            }
            
            var errors = new List<string>(4);
            
            // 验证字段名称
            var fieldName = mNameProp.stringValue;
            if (!string.IsNullOrEmpty(fieldName) && !IsValidIdentifier(fieldName))
            {
                errors.Add($"字段名称 '{fieldName}' 不是有效的 C# 标识符");
            }
            
            // 验证类名称（Element/Component）
            if (bindType is BindType.Element or BindType.Component)
            {
                var typeName = mCustomTypeProp.stringValue;
                if (!string.IsNullOrEmpty(typeName) && !IsValidIdentifier(typeName))
                {
                    errors.Add($"类名称 '{typeName}' 不是有效的 C# 标识符");
                }
            }
            
            if (errors.Count > 0)
            {
                mValidationLabel.text = string.Join("\n", errors);
                mValidationLabel.style.display = DisplayStyle.Flex;
                mValidationLabel.RemoveFromClassList("validation-success");
                mValidationLabel.AddToClassList("validation-error");
            }
            else
            {
                mValidationLabel.style.display = DisplayStyle.None;
            }
        }
        
        private static bool IsValidIdentifier(string name)
        {
            return IDENTIFIER_REGEX.IsMatch(name);
        }
        
        #endregion
    }
}
#endif
