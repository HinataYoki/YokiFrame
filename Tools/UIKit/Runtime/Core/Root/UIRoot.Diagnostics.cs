#if !GODOT
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// UIRoot 的命令桥诊断快照辅助逻辑。
    /// </summary>
    public partial class UIRoot
    {
        internal void AppendDiagnosticsPanels(List<UIPanelDiagnosticsSnapshot> panels)
        {
            if (panels == default)
                return;

            foreach (var cacheEntry in mOpenedCache)
            {
                AppendDiagnosticsPanel(panels, cacheEntry.Key, cacheEntry.Value, true);
            }

            foreach (var preloadEntry in mPreloadedCache)
            {
                AppendDiagnosticsPanel(panels, preloadEntry.Key, preloadEntry.Value.Handler, true);
            }
        }

        internal void AppendDiagnosticsStacks(List<UIStackDiagnosticsSnapshot> stacks)
        {
            if (stacks == default)
                return;

            foreach (var stackEntry in mStacks)
            {
                stacks.Add(new UIStackDiagnosticsSnapshot(
                    stackEntry.Key,
                    CopyStackPanelNames(stackEntry.Value)));
            }
        }

        private void AppendDiagnosticsPanel(
            List<UIPanelDiagnosticsSnapshot> panels,
            Type panelType,
            PanelHandler handler,
            bool isCached)
        {
            if (handler == default || handler.Panel == default)
                return;

            panels.Add(new UIPanelDiagnosticsSnapshot(
                panelType,
                handler.Panel,
                isCached,
                FindDiagnosticsStackNames(handler.Panel)));
        }

        private string[] FindDiagnosticsStackNames(IPanel panel)
        {
            if (panel == default || mStacks.Count == 0)
                return Array.Empty<string>();

            var names = new List<string>();
            foreach (var stackEntry in mStacks)
            {
                var node = stackEntry.Value.First;
                while (node != default)
                {
                    if (ReferenceEquals(node.Value, panel))
                    {
                        names.Add(stackEntry.Key);
                        break;
                    }

                    node = node.Next;
                }
            }

            return names.ToArray();
        }

        private static string[] CopyStackPanelNames(PooledLinkedList<IPanel> stack)
        {
            if (stack == default || stack.Count == 0)
                return Array.Empty<string>();

            var names = new string[stack.Count];
            var node = stack.First;
            var index = 0;
            while (node != default)
            {
                names[index] = GetDiagnosticsPanelName(node.Value);
                index++;
                node = node.Next;
            }

            return names;
        }

        private static string GetDiagnosticsPanelName(IPanel panel)
        {
            if (panel == default)
                return string.Empty;

            return string.IsNullOrEmpty(panel.PanelName) ? panel.GetType().Name : panel.PanelName;
        }
    }
}
#endif
