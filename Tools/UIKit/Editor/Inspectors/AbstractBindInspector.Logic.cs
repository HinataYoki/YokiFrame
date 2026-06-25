#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    public partial class AbstractBindInspector
    {
        private void EnsureDefaultValues()
        {
            var bind = target as AbstractBind;
            if (bind == null)
                return;

            var changed = false;
            if (string.IsNullOrEmpty(mNameProp.stringValue) && CurrentBindType() != BindType.Leaf)
            {
                mNameProp.stringValue = ToPascalIdentifier(bind.gameObject.name);
                changed = true;
            }

            if (string.IsNullOrEmpty(mAutoTypeProp.stringValue) && mComponentNames.Count > 0)
            {
                mAutoTypeProp.stringValue = mComponentNames[mComponentNames.Count - 1];
                changed = true;
            }

            if (string.IsNullOrEmpty(mCustomTypeProp.stringValue))
            {
                mCustomTypeProp.stringValue = ToPascalIdentifier(bind.gameObject.name);
                changed = true;
            }

            if (string.IsNullOrEmpty(mTypeProp.stringValue))
            {
                mTypeProp.stringValue = ResolveCurrentTypeName(CurrentBindType());
                changed = true;
            }

            if (changed)
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        private void ConvertTo(BindType targetType)
        {
            serializedObject.Update();
            mBindProp.enumValueIndex = (int)targetType;
            if (string.IsNullOrEmpty(mNameProp.stringValue))
                mNameProp.stringValue = ToPascalIdentifier(((AbstractBind)target).gameObject.name);
            if (targetType == BindType.Member && string.IsNullOrEmpty(mAutoTypeProp.stringValue) && mComponentNames.Count > 0)
                mAutoTypeProp.stringValue = mComponentNames[mComponentNames.Count - 1];
            if ((targetType == BindType.Element || targetType == BindType.Component) && string.IsNullOrEmpty(mCustomTypeProp.stringValue))
                mCustomTypeProp.stringValue = ToPascalIdentifier(((AbstractBind)target).gameObject.name);
            mTypeProp.stringValue = ResolveCurrentTypeName(targetType);
            serializedObject.ApplyModifiedProperties();

            if (mBindTypeField != null)
                mBindTypeField.SetValueWithoutNotify(targetType);

            RefreshInspectorState();
        }

        private void RefreshInspectorState()
        {
            serializedObject.Update();
            var bindType = CurrentBindType();
            var isLeaf = bindType == BindType.Leaf;
            var isMember = bindType == BindType.Member;
            var usesCustomType = bindType == BindType.Element || bindType == BindType.Component;

            if (mNameField != null)
                mNameField.SetEnabled(!isLeaf);
            if (mCommentField != null)
                mCommentField.SetEnabled(!isLeaf);
            if (mComponentPopup != null)
                mComponentPopup.style.display = isMember ? DisplayStyle.Flex : DisplayStyle.None;
            if (mCustomTypeField != null)
                mCustomTypeField.style.display = usesCustomType ? DisplayStyle.Flex : DisplayStyle.None;
            if (mTypeRow != null)
            {
                var typeLabel = mTypeRow.Q<Label>(className: "bind-label");
                if (typeLabel != null)
                    typeLabel.text = isMember ? "组件列表" : "类名称";
            }

            if (mToMemberButton != null)
                mToMemberButton.SetEnabled(!isLeaf && bindType != BindType.Member);
            if (mToElementButton != null)
                mToElementButton.SetEnabled(!isLeaf && bindType != BindType.Element);
            if (mToComponentButton != null)
                mToComponentButton.SetEnabled(!isLeaf && bindType != BindType.Component);

            if (mPathLabel != null)
                mPathLabel.text = GetBindPath(target as AbstractBind);
            if (mValidationLabel != null)
                RefreshValidation(bindType);
            RefreshSuggestion(bindType);
            if (mCodePreviewFoldout != null)
                mCodePreviewFoldout.style.display = isLeaf ? DisplayStyle.None : DisplayStyle.Flex;
            if (mCodePreviewLabel != null)
                mCodePreviewLabel.text = isLeaf ? "// Leaf 节点不生成代码" : BuildCodePreview();
            if (mJumpToCodeButton != null)
            {
                var codePath = GetGeneratedCodePath();
                var exists = !string.IsNullOrEmpty(codePath) && File.Exists(Path.GetFullPath(codePath));
                mJumpToCodeButton.SetEnabled(exists);
                mJumpToCodeButton.text = exists ? "跳转到代码" : "代码未生成";
            }
        }

        private void RefreshValidation(BindType bindType)
        {
            mValidationLabel.RemoveFromClassList("validation-error");
            mValidationLabel.RemoveFromClassList("validation-success");

            if (bindType == BindType.Leaf)
            {
                mValidationLabel.style.display = DisplayStyle.None;
                return;
            }

            var errors = new List<string>(3);
            if (!IsValidIdentifier(mNameProp.stringValue))
                errors.Add("字段名称不是合法 C# 标识符。");
            if ((bindType == BindType.Element || bindType == BindType.Component) && !IsValidIdentifier(mCustomTypeProp.stringValue))
                errors.Add("类名称不是合法 C# 标识符。");
            if (bindType == BindType.Member && string.IsNullOrEmpty(mAutoTypeProp.stringValue))
                errors.Add("Member 绑定需要选择组件类型。");

            if (errors.Count == 0)
            {
                mValidationLabel.style.display = DisplayStyle.None;
                return;
            }

            mValidationLabel.text = string.Join("\n", errors.ToArray());
            mValidationLabel.style.display = DisplayStyle.Flex;
            mValidationLabel.AddToClassList("validation-error");
        }

        private void RefreshSuggestion(BindType bindType)
        {
            if (mSuggestionRow == null || mSuggestionLabel == null)
                return;

            var bind = target as AbstractBind;
            if (bind == null || bindType == BindType.Leaf)
            {
                mSuggestionRow.style.display = DisplayStyle.None;
                return;
            }

            var suggestion = ToPascalIdentifier(bind.gameObject.name);
            if (string.IsNullOrEmpty(suggestion) || string.Equals(suggestion, mNameProp.stringValue, StringComparison.Ordinal))
            {
                mSuggestionRow.style.display = DisplayStyle.None;
                return;
            }

            mSuggestionLabel.text = "建议命名：" + suggestion;
            mSuggestionRow.style.display = DisplayStyle.Flex;
        }

        private void ApplySuggestion()
        {
            var bind = target as AbstractBind;
            if (bind == null)
                return;

            var suggestion = ToPascalIdentifier(bind.gameObject.name);
            serializedObject.Update();
            mNameProp.stringValue = suggestion;
            serializedObject.ApplyModifiedProperties();

            if (mNameField != null)
                mNameField.SetValueWithoutNotify(suggestion);
            RefreshInspectorState();
        }

        private string BuildCodePreview()
        {
            var typeName = FormatTypeName(mTypeProp.stringValue);
            if (string.IsNullOrEmpty(typeName))
                typeName = "GameObject";

            var fieldName = mNameProp.stringValue;
            if (string.IsNullOrEmpty(fieldName))
                fieldName = "FieldName";

            var builder = new StringBuilder(96);
            if (!string.IsNullOrEmpty(mCommentProp.stringValue))
            {
                builder.AppendLine("/// <summary>");
                builder.Append("/// ").AppendLine(mCommentProp.stringValue);
                builder.AppendLine("/// </summary>");
            }

            builder.AppendLine("[SerializeField]");
            builder.Append("private ").Append(typeName).Append(' ').Append(fieldName).Append(';');
            return builder.ToString();
        }

        private void JumpToCode()
        {
            var codePath = GetGeneratedCodePath();
            if (string.IsNullOrEmpty(codePath))
                return;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(codePath);
            if (asset != null)
                AssetDatabase.OpenAsset(asset);
        }

        private string GetGeneratedCodePath()
        {
            var bind = target as AbstractBind;
            if (bind == null)
                return string.Empty;

            var panelName = GetPanelName(bind);
            if (string.IsNullOrEmpty(panelName))
                return string.Empty;

            var scriptRoot = UIKitPanelPrefabCreator.DEFAULT_SCRIPT_FOLDER;
            var typeName = string.IsNullOrEmpty(mCustomTypeProp.stringValue) ? mNameProp.stringValue : mCustomTypeProp.stringValue;
            switch (CurrentBindType())
            {
                case BindType.Member:
                    return scriptRoot + "/" + panelName + "/" + panelName + ".Designer.cs";
                case BindType.Element:
                    return scriptRoot + "/" + panelName + "/UIElement/" + typeName + ".Designer.cs";
                case BindType.Component:
                    return scriptRoot + "/UIComponent/" + typeName + ".Designer.cs";
                default:
                    return string.Empty;
            }
        }

        private BindType CurrentBindType()
        {
            if (mBindProp == null)
                return BindType.Member;
            return (BindType)mBindProp.enumValueIndex;
        }

        private string ResolveCurrentTypeName(BindType bindType)
        {
            if (bindType == BindType.Member)
                return !string.IsNullOrEmpty(mAutoTypeProp.stringValue) ? mAutoTypeProp.stringValue : LastComponentName();
            if (bindType == BindType.Element || bindType == BindType.Component)
                return !string.IsNullOrEmpty(mCustomTypeProp.stringValue) ? mCustomTypeProp.stringValue : ToPascalIdentifier(((AbstractBind)target).gameObject.name);
            return string.Empty;
        }

        private string LastComponentName()
        {
            return mComponentNames.Count > 0 ? mComponentNames[mComponentNames.Count - 1] : string.Empty;
        }

        private int FindComponentIndex(string typeName)
        {
            for (var i = 0; i < mComponentNames.Count; i++)
            {
                if (string.Equals(mComponentNames[i], typeName, StringComparison.Ordinal))
                    return i;
            }
            return mComponentNames.Count > 0 ? mComponentNames.Count - 1 : 0;
        }

        private static string GetBindPath(AbstractBind bind)
        {
            if (bind == null)
                return string.Empty;

            var names = new List<string>(8);
            var current = bind.transform;
            while (current != null)
            {
                names.Add(current.name);
                if (current.GetComponent<UIPanel>() != null)
                    break;
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string GetPanelName(AbstractBind bind)
        {
            var current = bind.transform;
            while (current != null)
            {
                if (current.GetComponent<UIPanel>() != null)
                    return current.name;
                current = current.parent;
            }

            return bind.transform.root != null ? bind.transform.root.name : string.Empty;
        }

        private static bool IsValidIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value) || sCSharpKeywords.Contains(value))
                return false;
            if (!char.IsLetter(value[0]) && value[0] != '_')
                return false;

            for (var i = 1; i < value.Length; i++)
            {
                if (!char.IsLetterOrDigit(value[i]) && value[i] != '_')
                    return false;
            }
            return true;
        }

        private static string ToPascalIdentifier(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Item";

            var builder = new StringBuilder(value.Length);
            var upperNext = true;
            for (var i = 0; i < value.Length; i++)
            {
                var c = value[i];
                if (char.IsLetterOrDigit(c) || c == '_')
                {
                    builder.Append(upperNext ? char.ToUpperInvariant(c) : c);
                    upperNext = false;
                }
                else
                {
                    upperNext = true;
                }
            }

            if (builder.Length == 0)
                builder.Append("Item");
            if (char.IsDigit(builder[0]))
                builder.Insert(0, '_');
            return builder.ToString();
        }

        private static string FormatTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return fullName;

            var lastDot = fullName.LastIndexOf('.');
            return lastDot >= 0 && lastDot < fullName.Length - 1 ? fullName.Substring(lastDot + 1) : fullName;
        }
    }
}
#endif
