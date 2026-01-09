#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame
{
    /// <summary>
    /// BindToolsWindow - UI 构建和逻辑
    /// </summary>
    public partial class BindToolsWindow
    {
        #region UI 构建

        /// <summary>
        /// 创建选中对象信息区域
        /// </summary>
        private void CreateSelectionSection()
        {
            var section = new VisualElement();
            section.AddToClassList("section");

            mSelectionCountLabel = new Label();
            mSelectionCountLabel.AddToClassList("selection-count");
            section.Add(mSelectionCountLabel);

            mRoot.Add(section);
        }

        /// <summary>
        /// 创建配置选项区域
        /// </summary>
        private void CreateOptionsSection()
        {
            var section = new VisualElement();
            section.AddToClassList("section");
            section.AddToClassList("options-section");

            // 递归处理子物体
            var recursiveToggle = new Toggle("递归处理子物体") { value = mRecursive };
            recursiveToggle.AddToClassList("option-toggle");
            recursiveToggle.RegisterValueChangedCallback(evt =>
            {
                mRecursive = evt.newValue;
                RefreshPreview();
            });
            section.Add(recursiveToggle);

            // 默认绑定类型
            var typeRow = new VisualElement();
            typeRow.AddToClassList("option-row");

            var typeLabel = new Label("默认类型:");
            typeLabel.AddToClassList("option-label");
            typeRow.Add(typeLabel);

            var typeField = new EnumField(mDefaultType);
            typeField.AddToClassList("option-field");
            typeField.RegisterValueChangedCallback(evt =>
            {
                mDefaultType = (BindType)evt.newValue;
                RefreshPreview();
            });
            typeRow.Add(typeField);

            section.Add(typeRow);

            // 自动建议名称
            var suggestToggle = new Toggle("使用组件类型前缀") { value = mAutoSuggestName };
            suggestToggle.AddToClassList("option-toggle");
            suggestToggle.RegisterValueChangedCallback(evt =>
            {
                mAutoSuggestName = evt.newValue;
                RefreshPreview();
            });
            section.Add(suggestToggle);

            mRoot.Add(section);
        }

        /// <summary>
        /// 创建预览区域
        /// </summary>
        private void CreatePreviewSection()
        {
            var section = new VisualElement();
            section.AddToClassList("section");
            section.AddToClassList("preview-section");

            var header = new Label("预览:");
            header.AddToClassList("section-header");
            section.Add(header);

            mPreviewContainer = new ScrollView();
            mPreviewContainer.AddToClassList("preview-container");
            section.Add(mPreviewContainer);

            mRoot.Add(section);
        }

        /// <summary>
        /// 创建操作按钮
        /// </summary>
        private void CreateActionButtons()
        {
            var buttonRow = new VisualElement();
            buttonRow.AddToClassList("button-row");

            var cancelBtn = new Button(Close) { text = "取消" };
            cancelBtn.AddToClassList("cancel-btn");
            buttonRow.Add(cancelBtn);

            mExecuteBtn = new Button(ExecuteBatchBind) { text = "添加绑定" };
            mExecuteBtn.AddToClassList("execute-btn");
            buttonRow.Add(mExecuteBtn);

            mRoot.Add(buttonRow);
        }

        #endregion

        #region 预览逻辑

        /// <summary>
        /// 选择变化时刷新预览
        /// </summary>
        private void OnSelectionChanged()
        {
            RefreshPreview();
        }

        /// <summary>
        /// 刷新预览
        /// </summary>
        private void RefreshPreview()
        {
            if (mPreviewContainer == null) return;

            mPreviewItems.Clear();
            mPreviewContainer.Clear();

            var selectedObjects = Selection.gameObjects;
            int count = selectedObjects?.Length ?? 0;

            // 更新选中数量
            if (mSelectionCountLabel != null)
            {
                mSelectionCountLabel.text = $"选中对象: {count} 个 GameObject";
            }

            if (count == 0)
            {
                var emptyLabel = new Label("请在 Hierarchy 中选择 GameObject");
                emptyLabel.AddToClassList("preview-empty");
                mPreviewContainer.Add(emptyLabel);

                if (mExecuteBtn != null)
                {
                    mExecuteBtn.SetEnabled(false);
                    mExecuteBtn.text = "添加绑定";
                }
                return;
            }

            // 收集预览项
            foreach (var go in selectedObjects)
            {
                CollectPreviewItems(go, mRecursive);
            }

            // 显示预览
            int addCount = 0;
            foreach (var item in mPreviewItems)
            {
                var row = CreatePreviewRow(item);
                mPreviewContainer.Add(row);

                if (!item.AlreadyHasBind)
                    addCount++;
            }

            // 更新执行按钮
            if (mExecuteBtn != null)
            {
                mExecuteBtn.SetEnabled(addCount > 0);
                mExecuteBtn.text = addCount > 0 ? $"添加 {addCount} 个绑定" : "无可添加的绑定";
            }
        }

        /// <summary>
        /// 收集预览项
        /// </summary>
        private void CollectPreviewItems(GameObject go, bool recursive)
        {
            if (go == null) return;

            var existingBind = go.GetComponent<AbstractBind>();
            string suggestedName = mAutoSuggestName
                ? BindNameSuggester.SuggestName(go, mDefaultType)
                : go.name;

            string componentType = GetPrimaryComponentTypeName(go);

            mPreviewItems.Add(new BindPreviewItem
            {
                GameObject = go,
                OriginalName = go.name,
                SuggestedName = suggestedName,
                ComponentType = componentType,
                AlreadyHasBind = existingBind != null
            });

            if (recursive)
            {
                int childCount = go.transform.childCount;
                for (int i = 0; i < childCount; i++)
                {
                    CollectPreviewItems(go.transform.GetChild(i).gameObject, true);
                }
            }
        }

        /// <summary>
        /// 创建预览行
        /// </summary>
        private VisualElement CreatePreviewRow(BindPreviewItem item)
        {
            var row = new VisualElement();
            row.AddToClassList("preview-row");

            if (item.AlreadyHasBind)
            {
                row.AddToClassList("preview-row-skipped");
            }

            // 原始名称
            var originalLabel = new Label(item.OriginalName);
            originalLabel.AddToClassList("preview-original");
            row.Add(originalLabel);

            // 箭头
            var arrow = new Label("→");
            arrow.AddToClassList("preview-arrow");
            row.Add(arrow);

            // 建议名称
            var suggestedLabel = new Label(item.SuggestedName);
            suggestedLabel.AddToClassList("preview-suggested");
            row.Add(suggestedLabel);

            // 组件类型
            if (!string.IsNullOrEmpty(item.ComponentType))
            {
                var typeLabel = new Label($"({item.ComponentType})");
                typeLabel.AddToClassList("preview-type");
                row.Add(typeLabel);
            }

            // 已有绑定标记
            if (item.AlreadyHasBind)
            {
                var skipLabel = new Label("[已有绑定]");
                skipLabel.AddToClassList("preview-skip");
                row.Add(skipLabel);
            }

            return row;
        }

        /// <summary>
        /// 获取主要组件类型名称
        /// </summary>
        private static string GetPrimaryComponentTypeName(GameObject go)
        {
            if (go == null) return null;

            var priorityTypes = new[]
            {
                typeof(UnityEngine.UI.Button),
                typeof(UnityEngine.UI.Toggle),
                typeof(UnityEngine.UI.Slider),
                typeof(UnityEngine.UI.InputField),
                typeof(UnityEngine.UI.Dropdown),
                typeof(UnityEngine.UI.ScrollRect),
                typeof(UnityEngine.UI.Image),
                typeof(UnityEngine.UI.RawImage),
                typeof(UnityEngine.UI.Text),
            };

            foreach (var type in priorityTypes)
            {
                if (go.GetComponent(type) != null)
                    return type.Name;
            }

            // 检查 TMP 组件
            var components = go.GetComponents<Component>();
            foreach (var comp in components)
            {
                if (comp == null) continue;
                var typeName = comp.GetType().Name;
                if (typeName.StartsWith("TMP_") || typeName.StartsWith("TextMeshPro"))
                    return typeName;
            }

            return null;
        }

        #endregion

        #region 执行逻辑

        /// <summary>
        /// 执行批量绑定
        /// </summary>
        private void ExecuteBatchBind()
        {
            var selectedObjects = Selection.gameObjects;
            if (selectedObjects == null || selectedObjects.Length == 0)
            {
                ShowResult("请先选择 GameObject", false);
                return;
            }

            var result = BindService.BatchAddBind(
                selectedObjects,
                mRecursive,
                mDefaultType,
                mAutoSuggestName);

            // 显示结果
            string message = $"添加完成: {result.SuccessCount} 成功";
            if (result.SkippedCount > 0)
                message += $", {result.SkippedCount} 跳过";
            if (result.FailedCount > 0)
                message += $", {result.FailedCount} 失败";

            ShowResult(message, result.FailedCount == 0);

            // 刷新预览
            RefreshPreview();
        }

        /// <summary>
        /// 显示结果
        /// </summary>
        private void ShowResult(string message, bool success)
        {
            if (mResultLabel == null) return;

            mResultLabel.text = message;
            mResultLabel.RemoveFromClassList("result-success");
            mResultLabel.RemoveFromClassList("result-error");
            mResultLabel.AddToClassList(success ? "result-success" : "result-error");
            mResultLabel.style.display = DisplayStyle.Flex;
        }

        #endregion
    }
}
#endif
