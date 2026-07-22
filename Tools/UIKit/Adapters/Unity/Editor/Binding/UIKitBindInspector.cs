#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;

namespace YokiFrame
{
    /// <summary>
    /// 使用 InspectorKit 提供 Bind 类型、字段、目标、校验、代码预览和跳转入口。
    /// </summary>
    [CustomEditor(typeof(AbstractBind), true)]
    [CanEditMultipleObjects]
    internal sealed partial class UIKitBindInspector : UnityEditor.Editor
    {
        private const string CODE_PREVIEW_STATE_KEY = "UIKit.Bind.CodePreview";
        private readonly List<Component> mComponents = new();
        private SerializedProperty mBind;
        private SerializedProperty mName;
        private SerializedProperty mAutoType;
        private SerializedProperty mCustomType;
        private SerializedProperty mType;
        private SerializedProperty mComment;
        private SerializedProperty mTarget;
        private SerializedProperty mMemberTargets;
        private EnumField mBindTypeField;
        private TextField mNameField;
        private TextField mCustomTypeField;
        private TextField mCommentField;
        private VisualElement mMemberTargetsContainer;
        private VisualElement mTypeRow;
        private VisualElement mSuggestionContainer;
        private Label mPathLabel;
        private Label mCodePreviewLabel;
        private VisualElement mValidationContainer;
        private Button mToMemberButton;
        private Button mToElementButton;
        private Button mToComponentButton;
        private Button mJumpToCodeButton;

        /// <summary>缓存兼容字段，并收集当前节点可绑定的组件。</summary>
        private void OnEnable()
        {
            mBind = serializedObject.FindProperty(nameof(AbstractBind.Bind));
            mName = serializedObject.FindProperty(nameof(AbstractBind.Name));
            mAutoType = serializedObject.FindProperty(nameof(AbstractBind.AutoType));
            mCustomType = serializedObject.FindProperty(nameof(AbstractBind.CustomType));
            mType = serializedObject.FindProperty(nameof(AbstractBind.Type));
            mComment = serializedObject.FindProperty(nameof(AbstractBind.Comment));
            mTarget = serializedObject.FindProperty("mTarget");
            mMemberTargets = serializedObject.FindProperty("mMemberTargets");
            CacheComponents();
        }

        /// <summary>创建与旧版布局一致、由 InspectorKit 统一着色和排版的视觉树。</summary>
        public override VisualElement CreateInspectorGUI()
        {
            serializedObject.Update();
            EnsureDefaultValues();
            VisualElement root = InspectorKitUi.CreateRoot();
            VisualElement panel = InspectorKitUi.CreatePanel(default);
            root.Add(panel);
            panel.Add(CreateBindTypeRow());
            panel.Add(CreateQuickConvertRow());
            panel.Add(CreateNameRow());
            panel.Add(CreateTypeRow());
            panel.Add(CreateCommentRow());
            mSuggestionContainer = new VisualElement();
            panel.Add(mSuggestionContainer);
            panel.Add(CreatePathRow());
            panel.Add(CreateCodePreview());
            mValidationContainer = new VisualElement();
            panel.Add(mValidationContainer);
            panel.Add(CreateJumpButton());
            RefreshInspectorState();
            return root;
        }

        /// <summary>创建绑定语义下拉字段，并在变化时同步最终类型。</summary>
        private VisualElement CreateBindTypeRow()
        {
            mBindTypeField = new EnumField(CurrentBindType());
            mBindTypeField.RegisterValueChangedCallback(evt =>
            {
                ApplyBindType((BindType)evt.newValue);
                RefreshInspectorState();
            });
            return InspectorKitUi.CreateStackedFieldRow("绑定类型", mBindTypeField);
        }

        /// <summary>创建 Member、Element 和 Component 的快捷转换按钮。</summary>
        private VisualElement CreateQuickConvertRow()
        {
            mToMemberButton = InspectorKitUi.CreateActionButton("-> Member", () => ConvertTo(BindType.Member));
            mToElementButton = InspectorKitUi.CreateActionButton("-> Element", () => ConvertTo(BindType.Element));
            mToComponentButton = InspectorKitUi.CreateActionButton("-> Component", () => ConvertTo(BindType.Component));
            VisualElement buttons = InspectorKitUi.CreateCompactButtonRow(
                mToMemberButton,
                mToElementButton,
                mToComponentButton);
            return InspectorKitUi.CreateStackedFieldRow("快速转换", buttons);
        }

        /// <summary>创建生成字段名称输入，并在变化后刷新校验和预览。</summary>
        private VisualElement CreateNameRow()
        {
            mNameField = new TextField { value = mName.stringValue };
            mNameField.RegisterValueChangedCallback(evt =>
            {
                WritePrimaryName(evt.newValue);
                RefreshInspectorState();
            });
            return InspectorKitUi.CreateStackedFieldRow("字段名称", mNameField);
        }

        /// <summary>创建 Member 组件下拉与生成类型输入的共用区域。</summary>
        private VisualElement CreateTypeRow()
        {
            VisualElement content = new();
            content.AddToClassList("yoki-editor-inspector__row-field");
            if (mComponents.Count > 0)
            {
                mMemberTargetsContainer = new VisualElement();
                content.Add(mMemberTargetsContainer);
            }
            else
            {
                content.Add(InspectorKitUi.CreateInfoBox(
                    "当前节点没有可绑定组件。",
                    InspectorInfoBoxType.Warning));
            }

            mCustomTypeField = new TextField { value = mCustomType.stringValue };
            mCustomTypeField.RegisterValueChangedCallback(evt =>
            {
                WriteGeneratedType(evt.newValue);
                RefreshInspectorState();
            });
            content.Add(mCustomTypeField);
            mTypeRow = InspectorKitUi.CreateStackedFieldRow("组件列表", content);
            return mTypeRow;
        }

        /// <summary>创建生成字段的可选注释输入。</summary>
        private VisualElement CreateCommentRow()
        {
            mCommentField = new TextField { value = mComment.stringValue };
            mCommentField.RegisterValueChangedCallback(evt =>
            {
                WriteString(mComment, evt.newValue);
                RefreshInspectorState();
            });
            return InspectorKitUi.CreateStackedFieldRow("注释", mCommentField);
        }

        /// <summary>创建当前 Bind 相对所属 UIPanel 的只读路径。</summary>
        private VisualElement CreatePathRow()
        {
            mPathLabel = new Label();
            mPathLabel.AddToClassList("yoki-editor-inspector__secondary-text");
            return InspectorKitUi.CreateStackedFieldRow("路径", mPathLabel);
        }

        /// <summary>创建可持久化展开状态的代码预览区。</summary>
        private VisualElement CreateCodePreview()
        {
            return InspectorKitUi.CreateFoldoutSection(
                "代码预览",
                CODE_PREVIEW_STATE_KEY,
                InspectorCardInitialState.Collapsed,
                body =>
                {
                    mCodePreviewLabel = new Label();
                    mCodePreviewLabel.AddToClassList("yoki-editor-inspector__code-block");
                    body.Add(mCodePreviewLabel);
                });
        }

        /// <summary>创建打开生成代码的主操作按钮。</summary>
        private VisualElement CreateJumpButton()
        {
            mJumpToCodeButton = InspectorKitUi.CreateActionButton(
                "跳转到代码",
                JumpToCode,
                InspectorActionStyle.Primary);
            return InspectorKitUi.CreateButtonRow(mJumpToCodeButton);
        }
    }
}
#endif
