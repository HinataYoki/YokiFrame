#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    public static partial class UIKit
    {
        /// <summary>捕获当前 Player/Editor 可直接读取的 UIKit 状态，不创建 UIRoot。</summary>
        public static UIKitRuntimeSnapshot CaptureRuntimeDiagnostics()
        {
            UIRoot root = Root;
            if (root == null)
            {
                return new UIKitRuntimeSnapshot
                {
                    HasRoot = false,
                    FocusName = null,
                    InputMode = UIInputMode.Pointer,
                    Panels = Array.Empty<string>()
                };
            }

            IReadOnlyList<IPanel> panels = GetLoadedPanels();
            var descriptions = new List<string>(panels.Count);
            var visibleCount = 0;
            for (var index = 0; index < panels.Count; index++)
            {
                IPanel panel = panels[index];
                if (panel.State == PanelState.Open) visibleCount++;
                descriptions.Add(panel.PanelName + " | " + panel.State + " | " + panel.Level);
            }
            descriptions.Sort(StringComparer.Ordinal);
            GameObject focus = root.CurrentFocus;
            return new UIKitRuntimeSnapshot
            {
                HasRoot = true,
                LoadedPanelCount = panels.Count,
                VisiblePanelCount = visibleCount,
                StackCount = GetAllStackNames().Count,
                FocusName = focus != null ? focus.name : null,
                InputMode = root.InputMode,
                Panels = descriptions
            };
        }
    }
}
#endif
