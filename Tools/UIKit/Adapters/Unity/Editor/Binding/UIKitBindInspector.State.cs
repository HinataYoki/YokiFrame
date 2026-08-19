#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;

namespace YokiFrame
{
    internal sealed partial class UIKitBindInspector
    {
        /// <summary>初始化空字段和旧 Prefab 类型字段，不覆盖已有明确配置。</summary>
        private void EnsureDefaultValues()
        {
            AbstractBind bind = target as AbstractBind;
            if (bind == default)
                return;
            bool changed = false;
            if (string.IsNullOrWhiteSpace(mName.stringValue) && bind.Bind != BindType.Leaf)
            {
                mName.stringValue = ToPascalIdentifier(bind.gameObject.name);
                changed = true;
            }
            if (string.IsNullOrWhiteSpace(mCustomType.stringValue))
            {
                mCustomType.stringValue = ToPascalIdentifier(bind.gameObject.name);
                changed = true;
            }
            if (bind.Bind == BindType.Member)
                changed |= EnsureMemberType();
            else if (bind.Bind == BindType.Element || bind.Bind == BindType.Component)
                changed |= SetStringIfDifferent(mType, mCustomType.stringValue);
            if (changed)
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
            serializedObject.Update();
        }

        /// <summary>收集同节点全部非 Bind 组件，并缓存用于 PopupField 的完整类型名。</summary>
        private void CacheComponents()
        {
            mComponents.Clear();
            AbstractBind bind = target as AbstractBind;
            if (bind == default)
                return;
            Component[] components = bind.GetComponents<Component>();
            for (var index = 0; index < components.Length; index++)
            {
                Component component = components[index];
                if (component == default || component is AbstractBind)
                    continue;
                mComponents.Add(component);
            }
        }

        /// <summary>确保 Member 拥有显式目标或稳定兼容类型。</summary>
        private bool EnsureMemberType()
        {
            if (mMemberTargets != null && mMemberTargets.arraySize > 0)
                return EnsureLegacyMemberTarget();
            Component component = mTarget.objectReferenceValue as Component;
            if (component == default && mComponents.Count > 0)
                component = FindConfiguredComponent();
            if (component == default)
                return false;
            bool changed = false;
            if (mTarget.objectReferenceValue != component)
            {
                mTarget.objectReferenceValue = component;
                changed = true;
            }
            string typeName = component.GetType().FullName;
            changed |= SetStringIfDifferent(mAutoType, typeName);
            changed |= SetStringIfDifferent(mType, typeName);
            return changed;
        }

        /// <summary>按显式目标、兼容类型和末尾组件依次选择 Member 目标。</summary>
        private Component FindConfiguredComponent()
        {
            string configured = string.IsNullOrWhiteSpace(mType.stringValue)
                ? mAutoType.stringValue
                : mType.stringValue;
            for (var index = 0; index < mComponents.Count; index++)
            {
                Type type = mComponents[index].GetType();
                if (string.Equals(type.FullName, configured, StringComparison.Ordinal)
                    || string.Equals(type.Name, configured, StringComparison.Ordinal))
                    return mComponents[index];
            }
            return mComponents[mComponents.Count - 1];
        }

        /// <summary>应用 BindType 并同步当前语义的最终类型字段。</summary>
        private void ApplyBindType(BindType bindType)
        {
            serializedObject.Update();
            mBind.enumValueIndex = (int)bindType;
            if (bindType == BindType.Member)
                EnsureMemberType();
            else if (bindType == BindType.Element || bindType == BindType.Component)
                mType.stringValue = ResolveGeneratedType();
            else
                mType.stringValue = string.Empty;
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>执行快捷转换，并同步下拉字段与动态内容。</summary>
        private void ConvertTo(BindType bindType)
        {
            ApplyBindType(bindType);
            mBindTypeField.SetValueWithoutNotify(bindType);
            RefreshInspectorState();
        }

        /// <summary>写入生成类型文本，并在对应 BindType 下同步最终类型。</summary>
        private void WriteGeneratedType(string value)
        {
            serializedObject.Update();
            mCustomType.stringValue = value ?? string.Empty;
            BindType bindType = CurrentBindType();
            if (bindType == BindType.Element || bindType == BindType.Component)
                mType.stringValue = ResolveGeneratedType();
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>写入一个字符串序列化属性并提交 Undo 友好的修改。</summary>
        private void WriteString(SerializedProperty property, string value)
        {
            serializedObject.Update();
            property.stringValue = value ?? string.Empty;
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>根据当前 BindType 刷新字段可见性、校验、建议和代码入口。</summary>
        private void RefreshInspectorState(bool refreshMemberTargets = true)
        {
            serializedObject.Update();
            BindType bindType = CurrentBindType();
            bool isLeaf = bindType == BindType.Leaf;
            bool isMember = bindType == BindType.Member;
            bool isGenerated = bindType == BindType.Element || bindType == BindType.Component;
            mNameField.SetEnabled(!isLeaf);
            mCommentField.SetEnabled(!isLeaf);
            if (mMemberTargetsContainer != null)
            {
                mMemberTargetsContainer.style.display = isMember ? DisplayStyle.Flex : DisplayStyle.None;
                if (isMember && refreshMemberTargets)
                    RefreshMemberTargetEditor();
            }
            mCustomTypeField.style.display = isGenerated ? DisplayStyle.Flex : DisplayStyle.None;
            Label typeLabel = mTypeRow.Q<Label>(className: "yoki-editor-inspector__row-label");
            if (typeLabel != null)
                typeLabel.text = isMember ? "组件列表" : "类名称";
            mToMemberButton.SetEnabled(!isLeaf && !isMember);
            mToElementButton.SetEnabled(!isLeaf && bindType != BindType.Element);
            mToComponentButton.SetEnabled(!isLeaf && bindType != BindType.Component);
            mPathLabel.text = GetBindPath(target as AbstractBind);
            mCodePreviewLabel.text = isLeaf ? "// Leaf 节点不生成代码" : BuildCodePreview();
            RefreshSuggestion(bindType);
            RefreshValidation(bindType);
            RefreshJumpButton();
        }

        /// <summary>按节点名称给出可一键应用的 PascalCase 字段建议。</summary>
        private void RefreshSuggestion(BindType bindType)
        {
            InspectorKitUi.Refresh(mSuggestionContainer, body =>
            {
                AbstractBind bind = target as AbstractBind;
                if (bind == default || bindType == BindType.Leaf)
                    return;
                string suggestion = ToPascalIdentifier(bind.gameObject.name);
                if (string.Equals(suggestion, mName.stringValue, StringComparison.Ordinal))
                    return;
                Button apply = InspectorKitUi.CreateActionButton("应用", () => ApplySuggestion(suggestion));
                body.Add(InspectorKitUi.CreateInfoBox(
                    "命名建议",
                    "建议使用字段名：" + suggestion,
                    InspectorInfoBoxType.Info));
                body.Add(InspectorKitUi.CreateCompactButtonRow(apply));
            });
        }

        /// <summary>应用当前节点名称生成的字段建议。</summary>
        private void ApplySuggestion(string suggestion)
        {
            WritePrimaryName(suggestion);
            mNameField.SetValueWithoutNotify(suggestion);
            RefreshInspectorState();
        }

        /// <summary>使用当前策略解析结果构建成功、警告或错误提示。</summary>
        private void RefreshValidation(BindType bindType)
        {
            InspectorKitUi.Refresh(mValidationContainer, body =>
            {
                if (bindType == BindType.Leaf)
                    return;
                AbstractBind bind = target as AbstractBind;
                if (!UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy strategy, out string error))
                {
                    body.Add(InspectorKitUi.CreateInfoBox(error, InspectorInfoBoxType.Error));
                    return;
                }
                if (IsBuiltInMultiMember() && !TryValidateMemberTargets(out error))
                {
                    body.Add(InspectorKitUi.CreateInfoBox(error, InspectorInfoBoxType.Warning));
                    return;
                }
                if (!strategy.TryResolve(bind, out _, out _, out error))
                    body.Add(InspectorKitUi.CreateInfoBox(error, InspectorInfoBoxType.Warning));
            });
        }

        /// <summary>根据生成代码是否存在刷新跳转按钮状态和文本。</summary>
        private void RefreshJumpButton()
        {
            string path = GetGeneratedCodePath();
            bool exists = !string.IsNullOrEmpty(path)
                && File.Exists(UIKitPanelCodeLayout.ToAbsolutePath(path));
            mJumpToCodeButton.SetEnabled(exists);
            mJumpToCodeButton.text = exists ? "跳转到代码" : "代码未生成";
        }

        /// <summary>生成当前绑定对应的 Designer 字段预览。</summary>
        private string BuildCodePreview()
        {
            if (IsBuiltInMultiMember())
                return BuildMultiMemberCodePreview();
            string typeName = FormatTypeName(mType.stringValue);
            if (string.IsNullOrEmpty(typeName))
                typeName = "GameObject";
            string fieldName = string.IsNullOrWhiteSpace(mName.stringValue)
                ? "FieldName"
                : mName.stringValue;
            StringBuilder builder = new(96);
            if (!string.IsNullOrWhiteSpace(mComment.stringValue))
            {
                builder.AppendLine("/// <summary>");
                builder.Append("/// ").AppendLine(mComment.stringValue);
                builder.AppendLine("/// </summary>");
            }
            builder.Append("public ").Append(typeName).Append(' ').Append(fieldName).Append(';');
            return builder.ToString();
        }

        /// <summary>打开当前绑定所属的生成 Designer 文件。</summary>
        private void JumpToCode()
        {
            string path = GetGeneratedCodePath();
            UnityEngine.Object asset = string.IsNullOrEmpty(path)
                ? default
                : AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
            if (asset != default)
                AssetDatabase.OpenAsset(asset);
        }

        /// <summary>根据当前 BindType 和默认布局安全计算生成文件路径，非 C# 标识符的层级根只禁用跳转入口。</summary>
        private string GetGeneratedCodePath()
        {
            AbstractBind bind = target as AbstractBind;
            string panelName = GetPanelName(bind);
            if (string.IsNullOrEmpty(panelName))
                return string.Empty;
            try
            {
                CodeGenKit.RequireIdentifier(panelName, nameof(panelName));
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.CreateDefault(panelName);
            UIKitPanelCodeLayout layout = new(request);
            switch (CurrentBindType())
            {
                case BindType.Member:
                    return layout.PanelDesignerPath;
                case BindType.Element:
                    return layout.GetElementPath(ResolveGeneratedType(), true);
                case BindType.Component:
                    return layout.GetComponentPath(ResolveGeneratedType(), true);
                default:
                    return string.Empty;
            }
        }

        /// <summary>读取当前绑定枚举，缺少属性时回退 Member。</summary>
        private BindType CurrentBindType()
        {
            return mBind == null ? BindType.Member : (BindType)mBind.enumValueIndex;
        }

        /// <summary>返回自定义类型、字段名或节点名中的首个有效生成类型。</summary>
        private string ResolveGeneratedType()
        {
            if (!string.IsNullOrWhiteSpace(mCustomType.stringValue))
                return mCustomType.stringValue.Trim();
            if (!string.IsNullOrWhiteSpace(mName.stringValue))
                return mName.stringValue.Trim();
            AbstractBind bind = target as AbstractBind;
            return bind == default ? "Item" : ToPascalIdentifier(bind.gameObject.name);
        }

        /// <summary>构造从所属 UIPanel 到 Bind 节点的稳定层级路径。</summary>
        private static string GetBindPath(AbstractBind bind)
        {
            if (bind == default)
                return string.Empty;
            List<string> names = new();
            Transform current = bind.transform;
            while (current != default)
            {
                names.Add(current.name);
                if (current.GetComponent<UIPanel>() != default)
                    break;
                current = current.parent;
            }
            names.Reverse();
            return string.Join("/", names);
        }

        /// <summary>查找最近 UIPanel 的节点名，找不到时使用层级根名。</summary>
        private static string GetPanelName(AbstractBind bind)
        {
            if (bind == default)
                return string.Empty;
            Transform current = bind.transform;
            while (current != default)
            {
                if (current.GetComponent<UIPanel>() != default)
                    return current.name;
                current = current.parent;
            }
            return bind.transform.root == default ? string.Empty : bind.transform.root.name;
        }

        /// <summary>把节点名称转换为可用的 PascalCase C# 标识符。</summary>
        private static string ToPascalIdentifier(string value)
        {
            return UIKitBindMemberNaming.ToPascalIdentifier(value);
        }

        /// <summary>从完整类型名中提取 Inspector 友好的短类型名。</summary>
        private static string FormatTypeName(string fullName)
        {
            if (string.IsNullOrEmpty(fullName))
                return fullName;
            int index = fullName.LastIndexOf('.');
            return index >= 0 && index < fullName.Length - 1
                ? fullName.Substring(index + 1)
                : fullName;
        }

        /// <summary>仅在目标文本变化时写入属性，并返回是否发生修改。</summary>
        private static bool SetStringIfDifferent(SerializedProperty property, string value)
        {
            string normalized = value ?? string.Empty;
            if (string.Equals(property.stringValue, normalized, StringComparison.Ordinal))
                return false;
            property.stringValue = normalized;
            return true;
        }
    }
}
#endif
