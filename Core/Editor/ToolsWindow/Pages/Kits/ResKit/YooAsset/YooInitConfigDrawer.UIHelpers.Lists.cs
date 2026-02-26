#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && UNITY_2022_1_OR_NEWER
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - 列表字段
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        #region 列表字段

        /// <summary>
        /// 创建资源包列表字段
        /// </summary>
        private static VisualElement CreatePackageListField(UnityEditor.SerializedProperty parent, string propertyName, string label)
        {
            var container = new VisualElement();
            container.style.marginTop = Spacing.SM;
            container.style.marginBottom = Spacing.SM;

            var labelRow = new VisualElement();
            labelRow.style.flexDirection = FlexDirection.Row;
            labelRow.style.alignItems = Align.Center;
            labelRow.style.marginBottom = Spacing.XS;

            var labelElement = new Label(label);
            labelElement.style.color = new StyleColor(Colors.TextSecondary);
            labelElement.style.fontSize = 12;
            labelElement.style.flexGrow = 1;
            labelRow.Add(labelElement);

            container.Add(labelRow);

            var listContainer = new VisualElement();
            listContainer.style.backgroundColor = new StyleColor(Colors.LayerElevated);
            listContainer.style.borderTopLeftRadius = Radius.MD;
            listContainer.style.borderTopRightRadius = Radius.MD;
            listContainer.style.borderBottomLeftRadius = Radius.MD;
            listContainer.style.borderBottomRightRadius = Radius.MD;
            listContainer.style.paddingLeft = Spacing.SM;
            listContainer.style.paddingRight = Spacing.SM;
            listContainer.style.paddingTop = Spacing.SM;
            listContainer.style.paddingBottom = Spacing.SM;
            container.Add(listContainer);

            var listProp = parent.FindPropertyRelative(propertyName);

            void RefreshList()
            {
                listContainer.Clear();

                for (int i = 0; i < listProp.arraySize; i++)
                {
                    int index = i;
                    var itemProp = listProp.GetArrayElementAtIndex(i);
                    listContainer.Add(CreatePackageListItem(listProp, itemProp, index, parent, RefreshList));
                }

                listContainer.Add(CreateAddPackageButton(listProp, parent, RefreshList));
            }

            RefreshList();

            return container;
        }

        /// <summary>
        /// 创建资源包列表项
        /// </summary>
        private static VisualElement CreatePackageListItem(UnityEditor.SerializedProperty listProp, UnityEditor.SerializedProperty itemProp, int index, UnityEditor.SerializedProperty parent, Action refreshList)
        {
            var itemRow = new VisualElement();
            itemRow.style.flexDirection = FlexDirection.Row;
            itemRow.style.alignItems = Align.Center;
            itemRow.style.marginBottom = Spacing.XS;

            var indexLabel = new Label(index == 0 ? "默认" : $"#{index + 1}");
            indexLabel.style.width = 32;
            indexLabel.style.color = new StyleColor(index == 0 ? Colors.BrandPrimary : Colors.TextTertiary);
            indexLabel.style.fontSize = 10;
            indexLabel.style.unityFontStyleAndWeight = index == 0 ? FontStyle.Bold : FontStyle.Normal;
            itemRow.Add(indexLabel);

            var textField = new TextField { value = itemProp.stringValue };
            textField.style.flexGrow = 1;
            textField.RegisterValueChangedCallback(evt =>
            {
                itemProp.stringValue = evt.newValue;
                parent.serializedObject.ApplyModifiedProperties();
            });
            itemRow.Add(textField);

            if (listProp.arraySize > 1)
            {
                var deleteBtn = new Button(() =>
                {
                    listProp.DeleteArrayElementAtIndex(index);
                    parent.serializedObject.ApplyModifiedProperties();
                    refreshList();
                }) { text = "×" };
                deleteBtn.style.width = 20;
                deleteBtn.style.height = 20;
                deleteBtn.style.marginLeft = Spacing.XS;
                deleteBtn.style.backgroundColor = new StyleColor(Color.clear);
                deleteBtn.style.borderLeftWidth = 0;
                deleteBtn.style.borderRightWidth = 0;
                deleteBtn.style.borderTopWidth = 0;
                deleteBtn.style.borderBottomWidth = 0;
                deleteBtn.style.color = new StyleColor(Colors.StatusError);
                deleteBtn.style.fontSize = 14;
                deleteBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                itemRow.Add(deleteBtn);
            }

            return itemRow;
        }

        /// <summary>
        /// 创建添加资源包按钮
        /// </summary>
        private static VisualElement CreateAddPackageButton(UnityEditor.SerializedProperty listProp, UnityEditor.SerializedProperty parent, Action refreshList)
        {
            var addBtn = new Button(() =>
            {
                listProp.InsertArrayElementAtIndex(listProp.arraySize);
                var newItem = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
                newItem.stringValue = $"Package{listProp.arraySize}";
                parent.serializedObject.ApplyModifiedProperties();
                refreshList();
            }) { text = "+ 添加包" };
            addBtn.style.marginTop = Spacing.SM;
            addBtn.style.height = 24;
            addBtn.style.backgroundColor = new StyleColor(Colors.LayerCard);
            addBtn.style.borderTopLeftRadius = Radius.SM;
            addBtn.style.borderTopRightRadius = Radius.SM;
            addBtn.style.borderBottomLeftRadius = Radius.SM;
            addBtn.style.borderBottomRightRadius = Radius.SM;
            addBtn.style.borderLeftWidth = 1;
            addBtn.style.borderRightWidth = 1;
            addBtn.style.borderTopWidth = 1;
            addBtn.style.borderBottomWidth = 1;
            addBtn.style.borderLeftColor = new StyleColor(Colors.BorderDefault);
            addBtn.style.borderRightColor = new StyleColor(Colors.BorderDefault);
            addBtn.style.borderTopColor = new StyleColor(Colors.BorderDefault);
            addBtn.style.borderBottomColor = new StyleColor(Colors.BorderDefault);
            addBtn.style.color = new StyleColor(Colors.TextSecondary);
            return addBtn;
        }

        #endregion
    }
}
#endif
