#if !GODOT
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 诊断快照。
    /// 这些数据只服务 CommandBridge、Tauri 和 AI 查询，真实 UI 创建和销毁仍由宿主 backend 执行。
    /// </summary>
    internal sealed class UIKitDiagnosticsSnapshot
    {
        public UIKitDiagnosticsSnapshot(
            bool isInitialized,
            string backendName,
            List<UIPanelDiagnosticsSnapshot> panels,
            List<UIStackDiagnosticsSnapshot> stacks)
        {
            IsInitialized = isInitialized;
            BackendName = backendName ?? string.Empty;
            Panels = panels ?? new List<UIPanelDiagnosticsSnapshot>();
            Stacks = stacks ?? new List<UIStackDiagnosticsSnapshot>();
        }

        public bool IsInitialized { get; }

        public string BackendName { get; }

        public List<UIPanelDiagnosticsSnapshot> Panels { get; }

        public List<UIStackDiagnosticsSnapshot> Stacks { get; }
    }

    internal readonly struct UIPanelDiagnosticsSnapshot
    {
        internal UIPanelDiagnosticsSnapshot(Type panelType, IPanel panel, bool isCached, string[] stackNames)
        {
            Panel = panel;
            PanelTypeName = GetTypeName(panelType);
            PanelName = GetPanelName(panel, PanelTypeName);
            LevelName = panel != null ? panel.Level.ToString() : default(UILevel).ToString();
            LevelOrder = panel != null ? panel.Level.Order : 0;
            State = panel != null ? panel.State : PanelState.Close;
            Tag = panel != null ? panel.Tag ?? string.Empty : string.Empty;
            DataTypeName = panel != null && panel.Data != null ? GetTypeName(panel.Data.GetType()) : string.Empty;
            IsCached = isCached;
            StackNames = stackNames ?? new string[0];
        }

        // 仅用于 UIKit 内部做引用去重，不写入命令桥 JSON。
        internal IPanel Panel { get; }

        public string PanelName { get; }

        public string PanelTypeName { get; }

        public string LevelName { get; }

        public int LevelOrder { get; }

        public PanelState State { get; }

        public string Tag { get; }

        public string DataTypeName { get; }

        public bool IsCached { get; }

        public string[] StackNames { get; }

        private static string GetPanelName(IPanel panel, string fallback)
        {
            if (panel != null && !string.IsNullOrEmpty(panel.PanelName))
                return panel.PanelName;

            return fallback ?? string.Empty;
        }

        private static string GetTypeName(Type type)
        {
            if (type == null)
                return string.Empty;

            return type.FullName ?? type.Name;
        }
    }

    internal readonly struct UIStackDiagnosticsSnapshot
    {
        public UIStackDiagnosticsSnapshot(string stackName, string[] panelNames)
        {
            StackName = stackName ?? UIRoot.DEFAULT_STACK;
            PanelNames = panelNames ?? new string[0];
        }

        public string StackName { get; }

        public string[] PanelNames { get; }

        public int Depth
        {
            get { return PanelNames.Length; }
        }

        public string TopPanelName
        {
            get { return PanelNames.Length > 0 ? PanelNames[PanelNames.Length - 1] : string.Empty; }
        }
    }
}
#endif
