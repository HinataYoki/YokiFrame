#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine.UIElements;

namespace YokiFrame.Unity.Inspector
{
    /// <summary>
    /// InspectorKit 的序列化字符串列表配置。
    /// 该配置只描述列表语义，具体增删布局由 InspectorKit 统一实现。
    /// </summary>
    public sealed class InspectorStringListOptions
    {
        /// <summary>列表标题。</summary>
        public string Title { get; set; }

        /// <summary>新增按钮文本。</summary>
        public string AddLabel { get; set; } = "+ 添加项";

        /// <summary>允许保留的最小元素数量。</summary>
        public int MinimumCount { get; set; }

        /// <summary>按索引创建新元素的默认值。</summary>
        public Func<int, string> DefaultValueFactory { get; set; }

        /// <summary>按索引生成行左侧标记文本。</summary>
        public Func<int, string> MarkerFactory { get; set; }

        /// <summary>是否只读展示列表并隐藏新增、删除入口。</summary>
        public bool IsReadOnly { get; set; }
    }

    public static partial class InspectorKitUi
    {
        /// <summary>
        /// 创建可增删、可绑定 Unity SerializedProperty 数组的字符串列表编辑器。
        /// </summary>
        /// <param name="listProperty">字符串数组属性。</param>
        /// <param name="options">列表标题、默认值和最小数量配置。</param>
        /// <returns>列表编辑器视觉树。</returns>
        public static VisualElement CreateStringList(
            SerializedProperty listProperty,
            InspectorStringListOptions options)
        {
            if (listProperty == null)
                return CreateInfoBox("未找到字符串列表序列化字段。", InspectorInfoBoxType.Error);

            InspectorStringListOptions resolved = options ?? new InspectorStringListOptions();
            VisualElement root = new();
            root.AddToClassList("yoki-editor-inspector__list");
            root.EnableInClassList(
                "yoki-editor-inspector__list--readonly",
                resolved.IsReadOnly);
            if (!string.IsNullOrEmpty(resolved.Title))
            {
                Label title = new(resolved.Title);
                title.AddToClassList("yoki-editor-inspector__list-title");
                root.Add(title);
            }

            VisualElement items = new();
            items.AddToClassList("yoki-editor-inspector__list-items");
            root.Add(items);
            RefreshStringList(items, listProperty, resolved);
            return root;
        }

        /// <summary>按当前 SerializedObject 状态重绘字符串列表行。</summary>
        private static void RefreshStringList(
            VisualElement items,
            SerializedProperty sourceProperty,
            InspectorStringListOptions options)
        {
            SerializedProperty list = sourceProperty.serializedObject.FindProperty(sourceProperty.propertyPath);
            if (list == null)
                return;

            items.Clear();
            for (int index = 0; index < list.arraySize; index++)
                items.Add(CreateStringListItem(list, index, options, items));

            if (!options.IsReadOnly)
            {
                Button addButton = CreateActionButton(
                    options.AddLabel,
                    () => AddStringListItem(list, options, items),
                    InspectorActionStyle.Default,
                    "向列表添加一项");
                addButton.AddToClassList("yoki-editor-inspector__list-add");
                items.Add(addButton);
            }
        }

        /// <summary>创建一个带标记、编辑框和删除按钮的列表项。</summary>
        private static VisualElement CreateStringListItem(
            SerializedProperty list,
            int index,
            InspectorStringListOptions options,
            VisualElement items)
        {
            VisualElement row = new();
            row.AddToClassList("yoki-editor-inspector__list-item");

            string markerText = options.MarkerFactory == null
                ? "#" + (index + 1)
                : options.MarkerFactory(index);
            Label marker = new(markerText ?? string.Empty);
            marker.AddToClassList("yoki-editor-inspector__list-marker");
            marker.EnableInClassList(
                "yoki-editor-inspector__list-marker--primary",
                index == 0);
            row.Add(marker);

            SerializedProperty item = list.GetArrayElementAtIndex(index);
            TextField field = new()
            {
                value = item.stringValue,
                isReadOnly = options.IsReadOnly
            };
            field.AddToClassList("yoki-editor-inspector__list-field");
            if (!options.IsReadOnly)
            {
                field.RegisterValueChangedCallback(evt =>
                {
                    item.stringValue = evt.newValue ?? string.Empty;
                    item.serializedObject.ApplyModifiedProperties();
                });
            }
            row.Add(field);

            if (!options.IsReadOnly
                && list.arraySize > Math.Max(0, options.MinimumCount))
            {
                Button remove = CreateActionButton(
                    "-",
                    () => RemoveStringListItem(list, index, items, options),
                    InspectorActionStyle.Danger,
                    "删除此项");
                remove.AddToClassList("yoki-editor-inspector__list-remove");
                row.Add(remove);
            }

            return row;
        }

        /// <summary>向序列化列表插入默认字符串元素并刷新列表。</summary>
        private static void AddStringListItem(
            SerializedProperty list,
            InspectorStringListOptions options,
            VisualElement items)
        {
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            SerializedProperty item = list.GetArrayElementAtIndex(index);
            item.stringValue = options.DefaultValueFactory == null
                ? "Item" + (index + 1)
                : options.DefaultValueFactory(index) ?? string.Empty;
            list.serializedObject.ApplyModifiedProperties();
            RefreshStringList(items, list, options);
        }

        /// <summary>删除指定序列化列表元素并刷新列表。</summary>
        private static void RemoveStringListItem(
            SerializedProperty list,
            int index,
            VisualElement items,
            InspectorStringListOptions options)
        {
            if (list.arraySize <= Math.Max(0, options.MinimumCount))
                return;

            list.DeleteArrayElementAtIndex(index);
            list.serializedObject.ApplyModifiedProperties();
            RefreshStringList(items, list, options);
        }
    }
}
#endif
