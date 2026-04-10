#if UNITY_EDITOR
using System.Collections.Generic;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// Editor communication metadata for BuffKit runtime monitoring.
    /// </summary>
    internal sealed class BuffKitEditorChannelProvider : IEditorChannelProvider
    {
        public IEnumerable<EditorChannelDefinition> GetChannels()
        {
            yield return new EditorChannelDefinition
            {
                Kit = "BuffKit",
                Channel = DataChannels.BUFF_ADDED,
                DisplayName = "Buff Added",
                PayloadType = "BuffAddedEvent",
                Description = "Published when a buff is added.",
                SupportsThrottle = false
            };

            yield return new EditorChannelDefinition
            {
                Kit = "BuffKit",
                Channel = DataChannels.BUFF_REMOVED,
                DisplayName = "Buff Removed",
                PayloadType = "BuffRemovedEvent",
                Description = "Published when a buff is removed.",
                SupportsThrottle = false
            };

            yield return new EditorChannelDefinition
            {
                Kit = "BuffKit",
                Channel = DataChannels.BUFF_CONTAINER_CREATED,
                DisplayName = "Buff Container Created",
                PayloadType = "Kit-defined payload",
                Description = "Published when a buff container is created.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "BuffKit",
                Channel = DataChannels.BUFF_CONTAINER_DISPOSED,
                DisplayName = "Buff Container Disposed",
                PayloadType = "Kit-defined payload",
                Description = "Published when a buff container is disposed.",
                SupportsThrottle = true
            };
        }
    }
}
#endif
