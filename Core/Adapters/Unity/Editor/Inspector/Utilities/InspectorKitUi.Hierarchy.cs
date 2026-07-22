#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.Unity.Inspector
{
    /// <summary>InspectorKit 的紧凑层级列表与图例组件。</summary>
    public static partial class InspectorKitUi
    {
        private const int HIERARCHY_INDENT_WIDTH = 16;

        /// <summary>创建用于承载紧凑层级项的深色列表容器。</summary>
        /// <returns>可以直接添加层级项的容器。</returns>
        public static VisualElement CreateHierarchyView()
        {
            VisualElement view = new();
            view.AddToClassList("yoki-editor-inspector__hierarchy");
            return view;
        }

        /// <summary>创建可折叠、可选择的紧凑层级项。</summary>
        /// <param name="depth">从零开始的缩进层级。</param>
        /// <param name="marker">节点类型标记。</param>
        /// <param name="name">节点名称。</param>
        /// <param name="detail">节点类型或补充信息。</param>
        /// <param name="category">节点分类文本。</param>
        /// <param name="accent">节点强调色。</param>
        /// <param name="hasChildren">是否显示折叠按钮。</param>
        /// <param name="expanded">当前是否展开。</param>
        /// <param name="onToggle">折叠按钮回调。</param>
        /// <param name="onSelected">节点选择回调。</param>
        /// <returns>可以直接加入层级容器的视觉项。</returns>
        public static VisualElement CreateHierarchyItem(
            int depth,
            string marker,
            string name,
            string detail,
            string category,
            Color accent,
            bool hasChildren,
            bool expanded,
            Action onToggle,
            Action onSelected)
        {
            VisualElement row = CreateHierarchyRow(depth, onSelected);
            VisualElement card = CreateHierarchyCard(accent);
            card.Add(CreateHierarchyToggle(hasChildren, expanded, onToggle));
            card.Add(CreateHierarchyLabel(marker, "yoki-editor-inspector__hierarchy-marker", accent));
            card.Add(CreateHierarchyLabel(name, "yoki-editor-inspector__hierarchy-name", default));
            if (!string.IsNullOrEmpty(detail))
                card.Add(CreateHierarchyLabel("(" + detail + ")", "yoki-editor-inspector__hierarchy-detail", default));
            if (!string.IsNullOrEmpty(category))
                card.Add(CreateHierarchyLabel("- " + category, "yoki-editor-inspector__hierarchy-category", accent));
            row.Add(card);
            return row;
        }

        /// <summary>创建紧凑层级列表的图例容器。</summary>
        /// <param name="items">图例项。</param>
        /// <returns>允许自动换行的图例容器。</returns>
        public static VisualElement CreateHierarchyLegend(params VisualElement[] items)
        {
            VisualElement legend = new();
            legend.AddToClassList("yoki-editor-inspector__hierarchy-legend");
            if (items == null)
                return legend;
            for (var index = 0; index < items.Length; index++)
            {
                if (items[index] != null)
                    legend.Add(items[index]);
            }
            return legend;
        }

        /// <summary>创建带颜色标记的单个层级图例项。</summary>
        /// <param name="marker">节点标记。</param>
        /// <param name="text">图例文本。</param>
        /// <param name="accent">标记颜色。</param>
        /// <returns>层级图例项。</returns>
        public static VisualElement CreateHierarchyLegendItem(string marker, string text, Color accent)
        {
            VisualElement item = new();
            item.AddToClassList("yoki-editor-inspector__hierarchy-legend-item");
            item.Add(CreateHierarchyLabel(marker, "yoki-editor-inspector__hierarchy-legend-marker", accent));
            item.Add(CreateHierarchyLabel(text, "yoki-editor-inspector__hierarchy-legend-text", default));
            return item;
        }

        /// <summary>创建包含稳定缩进和选择回调的层级行。</summary>
        private static VisualElement CreateHierarchyRow(int depth, Action onSelected)
        {
            VisualElement row = new();
            row.AddToClassList("yoki-editor-inspector__hierarchy-row");
            if (depth > 0)
            {
                VisualElement indent = new();
                indent.AddToClassList("yoki-editor-inspector__hierarchy-indent");
                int width = depth * HIERARCHY_INDENT_WIDTH;
                indent.style.width = width;
                indent.style.minWidth = width;
                row.Add(indent);
            }
            if (onSelected != null)
                row.RegisterCallback<ClickEvent>(_ => onSelected());
            return row;
        }

        /// <summary>创建带强调边的层级内容卡片。</summary>
        private static VisualElement CreateHierarchyCard(Color accent)
        {
            VisualElement card = new();
            card.AddToClassList("yoki-editor-inspector__hierarchy-card");
            card.style.borderLeftColor = new StyleColor(accent);
            return card;
        }

        /// <summary>创建层级折叠按钮或等宽占位。</summary>
        private static VisualElement CreateHierarchyToggle(
            bool hasChildren,
            bool expanded,
            Action onToggle)
        {
            if (!hasChildren)
            {
                VisualElement spacer = new();
                spacer.AddToClassList("yoki-editor-inspector__hierarchy-toggle-spacer");
                return spacer;
            }

            Button button = new() { text = expanded ? "v" : ">" };
            button.AddToClassList("yoki-editor-inspector__hierarchy-toggle");
            button.RegisterCallback<ClickEvent>(evt =>
            {
                onToggle?.Invoke();
                evt.StopPropagation();
            });
            return button;
        }

        /// <summary>创建层级文本，并按需应用强调色。</summary>
        private static Label CreateHierarchyLabel(string text, string className, Color accent)
        {
            Label label = new(text ?? string.Empty);
            label.AddToClassList(className);
            if (accent != default)
                label.style.color = new StyleColor(accent);
            return label;
        }
    }
}
#endif
