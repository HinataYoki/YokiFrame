#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// AbstractBindInspector - 验证和更新逻辑
    /// </summary>
    public partial class AbstractBindInspector
    {
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

            var targetBind = target as AbstractBind;
            if (targetBind == null)
            {
                mValidationLabel.style.display = DisplayStyle.None;
                return;
            }
            
            var errors = new List<string>(4);
            
            // 使用 BindValidator 进行完整验证
            var validationResult = BindValidator.ValidateIdentifier(mNameProp.stringValue, targetBind.gameObject);
            if (validationResult.HasValue)
            {
                var result = validationResult.Value;
                errors.Add(result.Message);
                if (!string.IsNullOrEmpty(result.SuggestedFix))
                {
                    errors.Add($"    {result.SuggestedFix}");
                }
            }
            
            // 验证类名称（Element/Component）
            if (bindType is BindType.Element or BindType.Component)
            {
                var typeResult = BindValidator.ValidateIdentifier(mCustomTypeProp.stringValue, targetBind.gameObject);
                if (typeResult.HasValue)
                {
                    var result = typeResult.Value;
                    errors.Add($"类名称: {result.Message}");
                    if (!string.IsNullOrEmpty(result.SuggestedFix))
                    {
                        errors.Add($"    {result.SuggestedFix}");
                    }
                }
            }
            
            // 检查命名冲突（需要找到父 Panel）
            var panelRoot = FindPanelRoot(targetBind.gameObject);
            if (panelRoot != null)
            {
                var tree = BindService.CollectBindTree(panelRoot);
                if (tree != null)
                {
                    // 检测命名冲突
                    var conflictResults = new List<BindValidationResult>(4);
                    BindValidator.DetectNameConflicts(tree, conflictResults);
                    
                    // 查找与当前节点相关的冲突
                    foreach (var conflict in conflictResults)
                    {
                        if (conflict.Target == targetBind.gameObject)
                        {
                            errors.Add(conflict.Message);
                            if (!string.IsNullOrEmpty(conflict.SuggestedFix))
                            {
                                errors.Add($"    {conflict.SuggestedFix}");
                            }
                        }
                    }
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
        
        /// <summary>
        /// 查找包含此 GameObject 的 UIPanel 根节点
        /// </summary>
        private static GameObject FindPanelRoot(GameObject obj)
        {
            if (obj == null) return null;
            
            var current = obj.transform;
            while (current != null)
            {
                if (current.GetComponent<UIPanel>() != null)
                {
                    return current.gameObject;
                }
                current = current.parent;
            }
            return null;
        }
        
        /// <summary>
        /// 更新类型转换按钮状态
        /// </summary>
        private void UpdateTypeConvertButtons(BindType currentType)
        {
            if (mTypeConvertRow == null) return;
            
            var isLeaf = currentType == BindType.Leaf;
            mTypeConvertRow.style.display = isLeaf ? DisplayStyle.None : DisplayStyle.Flex;
            
            // 禁用当前类型的按钮
            var btnMember = mTypeConvertRow.Q<Button>("btn-to-member");
            var btnElement = mTypeConvertRow.Q<Button>("btn-to-element");
            var btnComponent = mTypeConvertRow.Q<Button>("btn-to-component");
            
            if (btnMember != null)
                btnMember.SetEnabled(currentType != BindType.Member);
            if (btnElement != null)
                btnElement.SetEnabled(currentType != BindType.Element);
            if (btnComponent != null)
                btnComponent.SetEnabled(currentType != BindType.Component);
        }

        /// <summary>
        /// 更新命名建议
        /// </summary>
        private void UpdateSuggestion()
        {
            if (mSuggestionRow == null) return;
            
            var targetBind = target as AbstractBind;
            var bindType = (BindType)mBindProp.enumValueIndex;
            
            if (targetBind == null || bindType == BindType.Leaf)
            {
                mSuggestionRow.style.display = DisplayStyle.None;
                return;
            }
            
            string currentName = mNameProp.stringValue;
            string suggestion = BindNameSuggester.SuggestName(targetBind.gameObject, bindType);
            
            // 如果当前名称与建议相同，隐藏建议
            if (string.IsNullOrEmpty(suggestion) || currentName == suggestion)
            {
                mSuggestionRow.style.display = DisplayStyle.None;
                return;
            }
            
            // 获取组件类型信息
            string componentInfo = "";
            if (bindType == BindType.Member && mComponentNames.Count > 0)
            {
                string typeName = mAutoTypeProp.stringValue;
                componentInfo = $" (基于 {FormatComponentName(typeName)})";
            }
            
            mSuggestionLabel.text = $"{suggestion}{componentInfo}";
            mSuggestionRow.style.display = DisplayStyle.Flex;
        }
        
        /// <summary>
        /// 更新绑定路径显示
        /// </summary>
        private void UpdateBindPath()
        {
            if (mBindPathLabel == null) return;
            
            var targetBind = target as AbstractBind;
            if (targetBind == null)
            {
                mBindPathLabel.text = "";
                return;
            }
            
            string path = BindService.GetBindPath(targetBind);
            mBindPathLabel.text = path;
        }

        /// <summary>
        /// 更新代码预览
        /// </summary>
        private void UpdateCodePreview()
        {
            if (mCodePreviewLabel == null || mCodePreviewFoldout == null) return;
            
            var targetBind = target as AbstractBind;
            var bindType = (BindType)mBindProp.enumValueIndex;
            
            if (targetBind == null || bindType == BindType.Leaf)
            {
                mCodePreviewFoldout.style.display = DisplayStyle.None;
                return;
            }
            
            string preview = BindCodePreview.GenerateFieldPreview(targetBind);
            mCodePreviewLabel.text = preview;
            mCodePreviewFoldout.style.display = DisplayStyle.Flex;
        }
        
        /// <summary>
        /// 更新跳转到代码按钮
        /// </summary>
        private void UpdateJumpToCodeButton()
        {
            if (mJumpToCodeBtn == null) return;
            
            var bindType = (BindType)mBindProp.enumValueIndex;
            if (bindType == BindType.Leaf)
            {
                mJumpToCodeBtn.style.display = DisplayStyle.None;
                return;
            }
            
            string codePath = GetGeneratedCodePath();
            bool fileExists = !string.IsNullOrEmpty(codePath) && File.Exists(codePath);
            
            mJumpToCodeBtn.style.display = DisplayStyle.Flex;
            mJumpToCodeBtn.SetEnabled(fileExists);
            mJumpToCodeBtn.text = fileExists ? "跳转到代码" : "代码未生成";
        }
        
        /// <summary>
        /// 获取生成的代码文件路径
        /// </summary>
        private string GetGeneratedCodePath()
        {
            var targetBind = target as AbstractBind;
            if (targetBind == null) return null;
            
            var bindType = (BindType)mBindProp.enumValueIndex;
            string scriptPath = UIKitCreateConfig.Instance.ScriptGeneratePath;
            string panelName = GetPanelName(targetBind);
            string bindName = mNameProp.stringValue;
            
            if (string.IsNullOrEmpty(panelName) || string.IsNullOrEmpty(bindName))
                return null;
            
            return bindType switch
            {
                BindType.Member => $"{scriptPath}/{panelName}/{panelName}.Designer.cs",
                BindType.Element => $"{scriptPath}/{panelName}/UIElement/{bindName}.Designer.cs",
                BindType.Component => $"{scriptPath}/UIComponent/{bindName}.Designer.cs",
                _ => null
            };
        }
        
        /// <summary>
        /// 获取 Panel 名称
        /// </summary>
        private static string GetPanelName(AbstractBind bind)
        {
            if (bind == null) return null;
            
            var current = bind.transform;
            while (current != null)
            {
                var panel = current.GetComponent<UIPanel>();
                if (panel != null)
                    return current.name;
                current = current.parent;
            }
            
            // 如果没找到 UIPanel，使用根节点名称
            var root = bind.transform.root;
            return root != null ? root.name : null;
        }
    }
}
#endif
