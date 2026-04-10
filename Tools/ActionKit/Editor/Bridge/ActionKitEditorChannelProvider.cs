#if UNITY_EDITOR
using System.Collections.Generic;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// Editor communication metadata for ActionKit runtime monitoring.
    /// </summary>
    internal sealed class ActionKitEditorChannelProvider : IEditorChannelProvider
    {
        public IEnumerable<EditorChannelDefinition> GetChannels()
        {
            yield return new EditorChannelDefinition
            {
                Kit = "ActionKit",
                Channel = DataChannels.ACTION_STARTED,
                DisplayName = "Action Started",
                PayloadType = "IAction",
                Description = "Published when an action begins execution.",
                SupportsThrottle = false
            };

            yield return new EditorChannelDefinition
            {
                Kit = "ActionKit",
                Channel = DataChannels.ACTION_FINISHED,
                DisplayName = "Action Finished",
                PayloadType = "IAction",
                Description = "Published when an action finishes execution.",
                SupportsThrottle = false
            };

            yield return new EditorChannelDefinition
            {
                Kit = "ActionKit",
                Channel = DataChannels.ACTION_PROGRESS,
                DisplayName = "Action Progress",
                PayloadType = "(ulong actionId, float progress)",
                Description = "Published when action progress changes. Throttled subscriptions are recommended.",
                SupportsThrottle = true
            };
        }
    }
}
#endif
