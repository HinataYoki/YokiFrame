#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// AbstractBindInspector - 事件处理
    /// </summary>
    public partial class AbstractBindInspector
    {
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
            UpdateTypeConvertButtons(newType);
            ValidateFields();
            UpdateSuggestion();
            UpdateCodePreview();
            UpdateJumpToCodeButton();
        }
        
        /// <summary>
        /// 快速类型转换
        /// </summary>
        private void OnQuickConvert(BindType targetType)
        {
            var targetBind = target as AbstractBind;
            if (targetBind == null) return;
            
            if (!BindTypeConverter.CanConvert(targetBind, targetType, out string reason))
            {
                EditorUtility.DisplayDialog("无法转换", reason, "确定");
                return;
            }

            // 预览转换影响
            var preview = BindTypeConverter.Preview(targetBind, targetType);
            if (!preview.Success)
            {
                EditorUtility.DisplayDialog("转换失败", preview.ErrorMessage, "确定");
                return;
            }
            
            // 简单转换直接执行，复杂转换显示确认对话框
            bool hasFileChanges = preview.FilesToCreate.Count > 0 || 
                                  preview.FilesToDelete.Count > 0 || 
                                  preview.FilesToModify.Count > 0;
            
            if (hasFileChanges)
            {
                string message = $"将 {targetBind.Name} 从 {targetBind.Bind} 转换为 {targetType}\n\n";
                if (preview.FilesToCreate.Count > 0)
                    message += $"需要创建 {preview.FilesToCreate.Count} 个文件\n";
                if (preview.FilesToModify.Count > 0)
                    message += $"需要修改 {preview.FilesToModify.Count} 个文件\n";
                if (preview.FilesToDelete.Count > 0)
                    message += $"需要删除 {preview.FilesToDelete.Count} 个文件\n";
                message += "\n确定要执行转换吗？";
                
                if (!EditorUtility.DisplayDialog("确认转换", message, "执行", "取消"))
                    return;
            }
            
            // 执行转换
            var result = BindTypeConverter.Execute(targetBind, targetType);
            if (!result.Success)
            {
                EditorUtility.DisplayDialog("转换失败", result.ErrorMessage, "确定");
                return;
            }
            
            // 刷新 Inspector
            serializedObject.Update();
            mBindTypeField.SetValueWithoutNotify((BindType)mBindProp.enumValueIndex);
            UpdateRowVisibility((BindType)mBindProp.enumValueIndex);
            UpdateTypeConvertButtons((BindType)mBindProp.enumValueIndex);
            ValidateFields();
            UpdateCodePreview();
            UpdateJumpToCodeButton();
        }
        
        /// <summary>
        /// 应用命名建议
        /// </summary>
        private void OnApplySuggestion()
        {
            var targetBind = target as AbstractBind;
            if (targetBind == null) return;

            string suggestion = BindNameSuggester.SuggestName(targetBind.gameObject, targetBind.Bind);
            if (string.IsNullOrEmpty(suggestion)) return;
            
            serializedObject.Update();
            mNameProp.stringValue = suggestion;
            serializedObject.ApplyModifiedProperties();
            
            mNameField.SetValueWithoutNotify(suggestion);
            ValidateFields();
            UpdateSuggestion();
            UpdateCodePreview();
        }
        
        /// <summary>
        /// 跳转到代码文件
        /// </summary>
        private void OnJumpToCode()
        {
            string codePath = GetGeneratedCodePath();
            if (string.IsNullOrEmpty(codePath) || !File.Exists(codePath))
            {
                EditorUtility.DisplayDialog("文件不存在", "代码文件尚未生成", "确定");
                return;
            }
            
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(codePath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset);
            }
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
            UpdateSuggestion();
            UpdateCodePreview();
        }
        
        private void OnCustomTypeChanged(ChangeEvent<string> evt)
        {
            serializedObject.Update();
            mCustomTypeProp.stringValue = evt.newValue;
            mTypeProp.stringValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
            
            ValidateFields();
            UpdateCodePreview();
        }
        
        private void OnComponentSelected(ChangeEvent<string> evt)
        {
            serializedObject.Update();
            mAutoTypeProp.stringValue = evt.newValue;
            mTypeProp.stringValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
            
            UpdateSuggestion();
            UpdateCodePreview();
        }
        
        private void OnCommentChanged(ChangeEvent<string> evt)
        {
            serializedObject.Update();
            mCommentProp.stringValue = evt.newValue;
            serializedObject.ApplyModifiedProperties();
            
            UpdateCodePreview();
        }
    }
}
#endif
