#if !GODOT
using System.Collections.Generic;

namespace YokiFrame
{
    public partial class UIKit
    {
        internal static UIKitDiagnosticsSnapshot CreateDiagnosticsSnapshot()
        {
            var root = UIRoot.ExistingInstance;
            var panels = new List<UIPanelDiagnosticsSnapshot>();
            var stacks = new List<UIStackDiagnosticsSnapshot>();

            if (root != default)
            {
                root.AppendDiagnosticsPanels(panels);
                root.AppendDiagnosticsStacks(stacks);
            }

            return new UIKitDiagnosticsSnapshot(
                root != default,
                "Unity.UIRoot",
                panels,
                stacks);
        }
    }
}
#endif
