#if UNITY_2022_3_OR_NEWER
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>UIKit 运行时调试使用的只读状态快照。</summary>
    public sealed class UIKitRuntimeSnapshot
    {
        /// <summary>当前是否存在 UIRoot。</summary>
        public bool HasRoot { get; internal set; }

        /// <summary>已物化面板数量。</summary>
        public int LoadedPanelCount { get; internal set; }

        /// <summary>可见面板数量。</summary>
        public int VisiblePanelCount { get; internal set; }

        /// <summary>命名栈数量。</summary>
        public int StackCount { get; internal set; }

        /// <summary>当前焦点名称。</summary>
        public string FocusName { get; internal set; }

        /// <summary>当前输入模式。</summary>
        public UIInputMode InputMode { get; internal set; }

        /// <summary>按类型名排序的面板描述。</summary>
        public IReadOnlyList<string> Panels { get; internal set; }
    }
}
#endif
