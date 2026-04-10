#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Editor communication metadata for PoolKit runtime monitoring.
    /// </summary>
    internal sealed class PoolKitEditorChannelProvider : IEditorChannelProvider
    {
        public IEnumerable<EditorChannelDefinition> GetChannels()
        {
            yield return new EditorChannelDefinition
            {
                Kit = "PoolKit",
                Channel = DataChannels.POOL_LIST_CHANGED,
                DisplayName = "Pool List Changed",
                PayloadType = "PoolDebugInfo",
                Description = "Published when the tracked pool list changes and the monitor summary should rebuild.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "PoolKit",
                Channel = DataChannels.POOL_ACTIVE_CHANGED,
                DisplayName = "Pool Active Changed",
                PayloadType = "PoolDebugInfo",
                Description = "Published when active object counts change so pool cards and detail panels can refresh.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "PoolKit",
                Channel = DataChannels.POOL_EVENT_LOGGED,
                DisplayName = "Pool Event Logged",
                PayloadType = "PoolEvent",
                Description = "Published when PoolKit records a runtime event for the selected pool event log.",
                SupportsThrottle = false
            };
        }
    }
}
#endif
