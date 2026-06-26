#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// Bind 组件的自定义 Inspector。
    /// </summary>
    [CustomEditor(typeof(AbstractBind), true)]
    [CanEditMultipleObjects]
    public partial class AbstractBindInspector : Editor
    {
        private const string TOKENS_STYLE_SHEET_PATH = "Core/Runtime/Adapters/Unity/Editor/UISystem/Styling/Tokens/YokiTokens.uss";
        private const string CORE_STYLE_SHEET_PATH = "Core/Runtime/Adapters/Unity/Editor/UISystem/Styling/Core/YokiCoreComponents.uss";
        private const string STYLE_SHEET_PATH = "Tools/UIKit/Editor/Bind/BindInspectorStyles.uss";
        private const string CODE_PREVIEW_FOLDOUT_KEY = "YokiFrame.UIKit.AbstractBindInspector.CodePreview";

        private static readonly HashSet<string> sCSharpKeywords = new HashSet<string>(StringComparer.Ordinal)
        {
            "abstract", "as", "base", "bool", "break", "byte", "case", "catch",
            "char", "checked", "class", "const", "continue", "decimal", "default",
            "delegate", "do", "double", "else", "enum", "event", "explicit",
            "extern", "false", "finally", "fixed", "float", "for", "foreach",
            "goto", "if", "implicit", "in", "int", "interface", "internal",
            "is", "lock", "long", "namespace", "new", "null", "object",
            "operator", "out", "override", "params", "private", "protected",
            "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
            "sizeof", "stackalloc", "static", "string", "struct", "switch",
            "this", "throw", "true", "try", "typeof", "uint", "ulong",
            "unchecked", "unsafe", "ushort", "using", "virtual", "void",
            "volatile", "while"
        };

        private readonly List<string> mComponentNames = new(16);

        private SerializedProperty mBindProp;
        private SerializedProperty mNameProp;
        private SerializedProperty mAutoTypeProp;
        private SerializedProperty mCustomTypeProp;
        private SerializedProperty mTypeProp;
        private SerializedProperty mCommentProp;

        private EnumField mBindTypeField;
        private TextField mNameField;
        private PopupField<string> mComponentPopup;
        private TextField mCustomTypeField;
        private TextField mCommentField;
        private Label mPathLabel;
        private Label mValidationLabel;
        private Label mCodePreviewLabel;
        private Label mSuggestionLabel;
        private Button mJumpToCodeButton;
        private Button mToMemberButton;
        private Button mToElementButton;
        private Button mToComponentButton;
        private Button mApplySuggestionButton;
        private VisualElement mTypeRow;
        private VisualElement mSuggestionRow;
        private Foldout mCodePreviewFoldout;

        private void OnEnable()
        {
            mBindProp = serializedObject.FindProperty("Bind");
            mNameProp = serializedObject.FindProperty("Name");
            mAutoTypeProp = serializedObject.FindProperty("AutoType");
            mCustomTypeProp = serializedObject.FindProperty("CustomType");
            mTypeProp = serializedObject.FindProperty("Type");
            mCommentProp = serializedObject.FindProperty("Comment");
            CacheComponentNames();
        }

        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            EnsureDefaultValues();

            var root = new VisualElement();
            root.AddToClassList("bind-inspector");

            AddStyleSheet(root, TOKENS_STYLE_SHEET_PATH);
            AddStyleSheet(root, CORE_STYLE_SHEET_PATH);
            AddStyleSheet(root, STYLE_SHEET_PATH);

            var container = new VisualElement();
            container.AddToClassList("bind-container");
            root.Add(container);

            CreateBindTypeRow(container);
            CreateQuickConvertRow(container);
            CreateNameRow(container);
            CreateTypeRow(container);
            CreateCommentRow(container);
            CreateValidationRow(container);
            CreateSuggestionRow(container);
            CreatePathRow(container);
            CreateCodePreview(container);
            CreateJumpToCodeButton(container);

            RefreshInspectorState();
            return root;
        }

        private static void AddStyleSheet(VisualElement root, string styleSheetPath)
        {
            if (root == null || string.IsNullOrEmpty(styleSheetPath))
                return;

            var styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UIKitEditorAssetPaths.ResolveStyleSheetPath(styleSheetPath));
            if (styleSheet == null)
                return;

            for (var i = 0; i < root.styleSheets.count; i++)
            {
                if (root.styleSheets[i] == styleSheet)
                    return;
            }

            root.styleSheets.Add(styleSheet);
        }

        private void CacheComponentNames()
        {
            mComponentNames.Clear();
            var bind = target as AbstractBind;
            if (bind == null)
                return;

            var components = bind.GetComponents<Component>();
            for (var i = 0; i < components.Length; i++)
            {
                var component = components[i];
                if (component != null && !(component is AbstractBind))
                    mComponentNames.Add(component.GetType().FullName);
            }
        }

        private void CreateBindTypeRow(VisualElement root)
        {
            var row = CreateRow("绑定类型");
            mBindTypeField = new EnumField(CurrentBindType());
            mBindTypeField.AddToClassList("bind-field");
            mBindTypeField.AddToClassList("yoki-field-row__field");
            mBindTypeField.AddToClassList("bind-dropdown-field");
            mBindTypeField.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                mBindProp.enumValueIndex = (int)(BindType)evt.newValue;
                mTypeProp.stringValue = ResolveCurrentTypeName((BindType)evt.newValue);
                serializedObject.ApplyModifiedProperties();
                RefreshInspectorState();
            });
            row.Add(mBindTypeField);
            root.Add(row);
        }

        private void CreateQuickConvertRow(VisualElement root)
        {
            var row = CreateRow("快速转换");
            row.AddToClassList("type-convert-row");

            var buttons = new VisualElement();
            buttons.AddToClassList("bind-field");
            buttons.AddToClassList("type-convert-buttons");

            mToMemberButton = CreateConvertButton("→ Member", BindType.Member);
            mToElementButton = CreateConvertButton("→ Element", BindType.Element);
            mToComponentButton = CreateConvertButton("→ Component", BindType.Component);
            buttons.Add(mToMemberButton);
            buttons.Add(mToElementButton);
            buttons.Add(mToComponentButton);

            row.Add(buttons);
            root.Add(row);
        }

        private Button CreateConvertButton(string text, BindType targetType)
        {
            var button = new Button(() => ConvertTo(targetType)) { text = text };
            button.AddToClassList("type-convert-btn");
            return button;
        }

        private void CreateNameRow(VisualElement root)
        {
            var row = CreateRow("字段名称");
            mNameField = new TextField { value = mNameProp.stringValue };
            mNameField.AddToClassList("bind-field");
            mNameField.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                mNameProp.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                RefreshInspectorState();
            });
            row.Add(mNameField);
            root.Add(row);
        }

        private void CreateTypeRow(VisualElement root)
        {
            mTypeRow = CreateRow("组件列表");
            var content = new VisualElement();
            content.AddToClassList("bind-field");
            content.AddToClassList("bind-type-stack");

            if (mComponentNames.Count > 0)
            {
                var selectedIndex = FindComponentIndex(mAutoTypeProp.stringValue);
                mComponentPopup = new PopupField<string>(mComponentNames, selectedIndex, FormatTypeName, FormatTypeName);
                mComponentPopup.AddToClassList("bind-type-field");
                mComponentPopup.AddToClassList("yoki-field-row__field");
                mComponentPopup.AddToClassList("bind-dropdown-field");
                mComponentPopup.RegisterValueChangedCallback(evt =>
                {
                    serializedObject.Update();
                    mAutoTypeProp.stringValue = evt.newValue;
                    if (CurrentBindType() == BindType.Member)
                        mTypeProp.stringValue = evt.newValue;
                    serializedObject.ApplyModifiedProperties();
                    RefreshInspectorState();
                });
                content.Add(mComponentPopup);
            }
            else
            {
                var empty = new Label("无可用组件");
                empty.AddToClassList("bind-no-component");
                content.Add(empty);
            }

            mCustomTypeField = new TextField { value = mCustomTypeProp.stringValue };
            mCustomTypeField.AddToClassList("bind-type-field");
            mCustomTypeField.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                mCustomTypeProp.stringValue = evt.newValue;
                if (CurrentBindType() == BindType.Element || CurrentBindType() == BindType.Component)
                    mTypeProp.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                RefreshInspectorState();
            });
            content.Add(mCustomTypeField);

            mTypeRow.Add(content);
            root.Add(mTypeRow);
        }

        private void CreateCommentRow(VisualElement root)
        {
            var row = CreateRow("注释");
            mCommentField = new TextField { value = mCommentProp.stringValue };
            mCommentField.AddToClassList("bind-field");
            mCommentField.RegisterValueChangedCallback(evt =>
            {
                serializedObject.Update();
                mCommentProp.stringValue = evt.newValue;
                serializedObject.ApplyModifiedProperties();
                RefreshInspectorState();
            });
            row.Add(mCommentField);
            root.Add(row);
        }

        private void CreateValidationRow(VisualElement root)
        {
            mValidationLabel = new Label();
            mValidationLabel.AddToClassList("validation-label");
            root.Add(mValidationLabel);
        }

        private void CreateSuggestionRow(VisualElement root)
        {
            mSuggestionRow = CreateRow("建议");
            mSuggestionRow.AddToClassList("suggestion-row");

            var content = new VisualElement();
            content.AddToClassList("bind-field");
            content.AddToClassList("suggestion-content");

            mSuggestionLabel = new Label();
            mSuggestionLabel.AddToClassList("suggestion-text");
            content.Add(mSuggestionLabel);

            mApplySuggestionButton = new Button(ApplySuggestion) { text = "应用" };
            mApplySuggestionButton.AddToClassList("suggestion-apply-btn");
            content.Add(mApplySuggestionButton);

            mSuggestionRow.Add(content);
            root.Add(mSuggestionRow);
        }

        private void CreatePathRow(VisualElement root)
        {
            var row = CreateRow("路径");
            row.AddToClassList("bind-path-row");
            mPathLabel = new Label();
            mPathLabel.AddToClassList("bind-field");
            mPathLabel.AddToClassList("bind-path-text");
            row.Add(mPathLabel);
            root.Add(row);
        }

        private void CreateCodePreview(VisualElement root)
        {
            mCodePreviewFoldout = new Foldout
            {
                text = "代码预览",
                value = SessionState.GetBool(CODE_PREVIEW_FOLDOUT_KEY, false)
            };
            mCodePreviewFoldout.AddToClassList("code-preview-foldout");
            mCodePreviewFoldout.RegisterValueChangedCallback(evt => SessionState.SetBool(CODE_PREVIEW_FOLDOUT_KEY, evt.newValue));
            mCodePreviewLabel = new Label();
            mCodePreviewLabel.AddToClassList("code-preview-text");
            mCodePreviewFoldout.Add(mCodePreviewLabel);
            root.Add(mCodePreviewFoldout);
        }

        private void CreateJumpToCodeButton(VisualElement root)
        {
            mJumpToCodeButton = new Button(JumpToCode) { text = "跳转到代码" };
            mJumpToCodeButton.AddToClassList("jump-to-code-btn");
            root.Add(mJumpToCodeButton);
        }

        private static VisualElement CreateRow(string label)
        {
            var row = new VisualElement();
            row.AddToClassList("bind-row");

            var labelElement = new Label(label);
            labelElement.AddToClassList("bind-label");
            row.Add(labelElement);
            return row;
        }
    }
}
#endif
