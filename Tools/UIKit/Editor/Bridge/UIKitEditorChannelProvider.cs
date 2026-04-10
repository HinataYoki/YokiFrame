#if UNITY_EDITOR
using System.Collections.Generic;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// Editor communication metadata for UIKit runtime monitoring.
    /// </summary>
    internal sealed class UIKitEditorChannelProvider : IEditorChannelProvider
    {
        public IEnumerable<EditorChannelDefinition> GetChannels()
        {
            yield return new EditorChannelDefinition
            {
                Kit = "UIKit",
                Channel = DataChannels.PANEL_OPENED,
                DisplayName = "Panel Opened",
                PayloadType = "IPanel",
                Description = "Published when a panel finishes opening.",
                SupportsThrottle = false
            };

            yield return new EditorChannelDefinition
            {
                Kit = "UIKit",
                Channel = DataChannels.PANEL_CLOSED,
                DisplayName = "Panel Closed",
                PayloadType = "IPanel",
                Description = "Published when a panel closes.",
                SupportsThrottle = false
            };

            yield return new EditorChannelDefinition
            {
                Kit = "UIKit",
                Channel = DataChannels.PANEL_STATE_CHANGED,
                DisplayName = "Panel State Changed",
                PayloadType = "(IPanel panel, PanelState state)",
                Description = "Published when a panel runtime state changes.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "UIKit",
                Channel = DataChannels.STACK_CHANGED,
                DisplayName = "Panel Stack Changed",
                PayloadType = "string stackName",
                Description = "Published when the panel stack structure changes.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "UIKit",
                Channel = DataChannels.FOCUS_CHANGED,
                DisplayName = "Focus Changed",
                PayloadType = "GameObject",
                Description = "Published when the current UI focus target changes.",
                SupportsThrottle = true
            };
        }
    }
}
#endif
