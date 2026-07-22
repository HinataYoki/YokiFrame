#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.Unity.Inspector
{
    /// <summary>
    /// 配置 InspectorKit 的按需选择列表；数据所有权和实际写回仍由业务 Inspector 管理。
    /// </summary>
    /// <typeparam name="T">由 Unity 管理生命周期的列表对象类型。</typeparam>
    public sealed class InspectorSelectionListOptions<T> where T : UnityEngine.Object
    {
        /// <summary>列表标题；为空时不显示第二级标题。</summary>
        public string Title { get; set; }

        /// <summary>没有已选项时显示的状态文本。</summary>
        public string EmptyLabel { get; set; } = "尚未添加项目";

        /// <summary>打开候选菜单的按钮文本。</summary>
        public string AddLabel { get; set; } = "+ 添加项目";

        /// <summary>允许保留的最小已选项数量。</summary>
        public int MinimumCount { get; set; }

        /// <summary>是否隐藏新增、删除入口并只读展示。</summary>
        public bool IsReadOnly { get; set; }

        /// <summary>业务回调执行后是否由列表自身重新读取 Provider 并刷新。</summary>
        public bool RefreshAfterChange { get; set; } = true;

        /// <summary>读取当前已选对象，返回顺序决定列表和生成顺序。</summary>
        public Func<IReadOnlyList<T>> SelectedItemsProvider { get; set; }

        /// <summary>读取当前可添加对象；列表通过弹出菜单按需展示这些候选。</summary>
        public Func<IReadOnlyList<T>> AvailableItemsProvider { get; set; }

        /// <summary>把对象转换为列表和候选菜单中的显示名称。</summary>
        public Func<T, string> DisplayNameFactory { get; set; }

        /// <summary>为已选项和候选菜单提供完整说明。</summary>
        public Func<T, string> TooltipFactory { get; set; }

        /// <summary>为一个已选对象创建业务详情控件；返回空时只显示对象名称。</summary>
        public Func<T, VisualElement> DetailFactory { get; set; }

        /// <summary>用户从候选菜单选择对象时执行的写回。</summary>
        public Action<T> AddItem { get; set; }

        /// <summary>用户删除已选对象时执行的写回。</summary>
        public Action<T> RemoveItem { get; set; }
    }

    public static partial class InspectorKitUi
    {
        /// <summary>
        /// 创建只常驻展示已选项、通过菜单按需添加候选对象的 Inspector 列表。
        /// </summary>
        /// <typeparam name="T">由 Unity 管理生命周期的列表对象类型。</typeparam>
        /// <param name="options">数据 Provider、显示和写回配置。</param>
        /// <returns>可以直接加入 Inspector 视觉树的列表根元素。</returns>
        public static VisualElement CreateSelectionList<T>(
            InspectorSelectionListOptions<T> options)
            where T : UnityEngine.Object
        {
            if (options == null
                || options.SelectedItemsProvider == null
                || options.AvailableItemsProvider == null)
                return CreateInfoBox("选择列表缺少数据 Provider。", InspectorInfoBoxType.Error);

            VisualElement root = new();
            root.AddToClassList("yoki-editor-inspector__list");
            root.EnableInClassList(
                "yoki-editor-inspector__list--readonly",
                options.IsReadOnly);
            AddSelectionListTitle(root, options.Title);
            VisualElement items = new();
            items.AddToClassList("yoki-editor-inspector__list-items");
            root.Add(items);
            RefreshSelectionList(items, options);
            return root;
        }

        /// <summary>添加可选的列表标题，避免业务 Inspector 重复实现标题样式。</summary>
        private static void AddSelectionListTitle(
            VisualElement root,
            string title)
        {
            if (string.IsNullOrEmpty(title))
                return;
            Label titleLabel = new(title);
            titleLabel.AddToClassList("yoki-editor-inspector__list-title");
            root.Add(titleLabel);
        }

        /// <summary>重新读取已选和候选对象，并完整刷新列表内容。</summary>
        private static void RefreshSelectionList<T>(
            VisualElement items,
            InspectorSelectionListOptions<T> options)
            where T : UnityEngine.Object
        {
            items.Clear();
            IReadOnlyList<T> selected = options.SelectedItemsProvider();
            int selectedCount = selected == null ? 0 : selected.Count;
            if (selectedCount == 0)
                items.Add(CreateInfoBox(options.EmptyLabel, InspectorInfoBoxType.Info));
            else
            {
                for (var index = 0; index < selectedCount; index++)
                {
                    T item = selected[index];
                    if (IsSelectionItemAlive(item))
                        items.Add(CreateSelectionListItem(items, options, item, index, selectedCount));
                }
            }

            if (!options.IsReadOnly)
                items.Add(CreateSelectionAddButton(items, options));
        }

        /// <summary>创建一个已选对象的名称、详情和删除入口。</summary>
        private static VisualElement CreateSelectionListItem<T>(
            VisualElement items,
            InspectorSelectionListOptions<T> options,
            T item,
            int index,
            int selectedCount)
            where T : UnityEngine.Object
        {
            VisualElement row = new();
            row.AddToClassList("yoki-editor-inspector__list-item");
            row.AddToClassList("yoki-editor-inspector__selection-list-item");
            VisualElement header = CreateSelectionItemHeader(
                items,
                options,
                item,
                index,
                selectedCount);
            row.Add(header);
            VisualElement detail = options.DetailFactory == null
                ? default
                : options.DetailFactory(item);
            if (detail != null)
            {
                detail.AddToClassList("yoki-editor-inspector__selection-list-detail");
                row.Add(detail);
            }
            return row;
        }

        /// <summary>创建已选对象头部，并按最小数量约束决定是否显示删除按钮。</summary>
        private static VisualElement CreateSelectionItemHeader<T>(
            VisualElement items,
            InspectorSelectionListOptions<T> options,
            T item,
            int index,
            int selectedCount)
            where T : UnityEngine.Object
        {
            VisualElement header = new();
            header.AddToClassList("yoki-editor-inspector__selection-list-header");
            Label marker = new("#" + (index + 1));
            marker.AddToClassList("yoki-editor-inspector__list-marker");
            marker.EnableInClassList("yoki-editor-inspector__list-marker--primary", index == 0);
            header.Add(marker);
            Label name = new(GetSelectionDisplayName(options, item));
            name.tooltip = GetSelectionTooltip(options, item);
            name.AddToClassList("yoki-editor-inspector__selection-list-name");
            header.Add(name);
            if (selectedCount > Math.Max(0, options.MinimumCount) && options.RemoveItem != null)
                header.Add(CreateSelectionRemoveButton(items, options, item));
            return header;
        }

        /// <summary>创建删除按钮，并按配置决定是否由列表自行刷新。</summary>
        private static Button CreateSelectionRemoveButton<T>(
            VisualElement items,
            InspectorSelectionListOptions<T> options,
            T item)
            where T : UnityEngine.Object
        {
            Button remove = CreateActionButton(
                "-",
                () => ApplySelectionChange(items, options, item, false),
                InspectorActionStyle.Danger,
                "移除此项");
            remove.AddToClassList("yoki-editor-inspector__list-remove");
            return remove;
        }

        /// <summary>创建打开候选菜单的按钮；没有候选项时保留禁用状态。</summary>
        private static Button CreateSelectionAddButton<T>(
            VisualElement items,
            InspectorSelectionListOptions<T> options)
            where T : UnityEngine.Object
        {
            Button add = CreateActionButton(
                options.AddLabel,
                () => ShowSelectionMenu(items, options),
                InspectorActionStyle.Default,
                "从可用对象中添加一项");
            add.AddToClassList("yoki-editor-inspector__list-add");
            IReadOnlyList<T> available = options.AvailableItemsProvider();
            add.SetEnabled(CountAliveSelectionItems(available) > 0 && options.AddItem != null);
            return add;
        }

        /// <summary>按需构建 Unity 原生候选菜单，避免大量候选常驻占用 Inspector 高度。</summary>
        private static void ShowSelectionMenu<T>(
            VisualElement items,
            InspectorSelectionListOptions<T> options)
            where T : UnityEngine.Object
        {
            IReadOnlyList<T> available = options.AvailableItemsProvider();
            if (available == null || options.AddItem == null)
                return;
            GenericMenu menu = new();
            for (var index = 0; index < available.Count; index++)
            {
                T choice = available[index];
                if (!IsSelectionItemAlive(choice))
                    continue;
                string label = GetSelectionDisplayName(options, choice);
                string tooltip = GetSelectionTooltip(options, choice);
                menu.AddItem(
                    new GUIContent(label, tooltip),
                    false,
                    () => ApplySelectionChange(items, options, choice, true));
            }
            menu.ShowAsContext();
        }

        /// <summary>执行一次业务写回，并在需要时重新读取 Provider 刷新列表。</summary>
        private static void ApplySelectionChange<T>(
            VisualElement items,
            InspectorSelectionListOptions<T> options,
            T item,
            bool add)
            where T : UnityEngine.Object
        {
            if (add)
                options.AddItem(item);
            else
                options.RemoveItem(item);
            if (options.RefreshAfterChange)
                RefreshSelectionList(items, options);
        }

        /// <summary>返回业务名称或 Unity 对象名称，保证列表始终有可识别文本。</summary>
        private static string GetSelectionDisplayName<T>(
            InspectorSelectionListOptions<T> options,
            T item)
            where T : UnityEngine.Object
        {
            string displayName = options.DisplayNameFactory == null
                ? item.name
                : options.DisplayNameFactory(item);
            return string.IsNullOrEmpty(displayName) ? item.GetType().Name : displayName;
        }

        /// <summary>返回业务提示或对象完整类型名。</summary>
        private static string GetSelectionTooltip<T>(
            InspectorSelectionListOptions<T> options,
            T item)
            where T : UnityEngine.Object
        {
            return options.TooltipFactory == null
                ? item.GetType().FullName
                : options.TooltipFactory(item) ?? string.Empty;
        }

        /// <summary>统计候选集合中仍有效的 Unity 对象数量。</summary>
        private static int CountAliveSelectionItems<T>(IReadOnlyList<T> items)
            where T : UnityEngine.Object
        {
            if (items == null)
                return 0;
            int count = 0;
            for (var index = 0; index < items.Count; index++)
            {
                if (IsSelectionItemAlive(items[index]))
                    count++;
            }
            return count;
        }

        /// <summary>使用 Unity 对象语义判断泛型列表项是否仍然有效。</summary>
        private static bool IsSelectionItemAlive<T>(T item)
            where T : UnityEngine.Object
        {
            UnityEngine.Object unityObject = item;
            return unityObject != default;
        }
    }
}
#endif
