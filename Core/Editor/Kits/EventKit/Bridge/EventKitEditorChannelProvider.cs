#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Editor communication metadata for EventKit runtime monitoring.
    /// </summary>
    internal sealed class EventKitEditorChannelProvider : IEditorChannelProvider
    {
        public IEnumerable<EditorChannelDefinition> GetChannels()
        {
            yield return new EditorChannelDefinition
            {
                Kit = "EventKit",
                Channel = DataChannels.EVENT_TRIGGERED,
                DisplayName = "Event Triggered",
                PayloadType = "(string eventType, string eventKey, string argsText)",
                Description = "Published when a runtime EventKit event is triggered and forwarded to the editor monitor.",
                SupportsThrottle = false
            };

            yield return new EditorChannelDefinition
            {
                Kit = "EventKit",
                Channel = DataChannels.EVENT_REGISTERED,
                DisplayName = "Listener Registered",
                PayloadType = "(string category, string listenerName)",
                Description = "Published when a runtime EventKit listener is registered.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "EventKit",
                Channel = DataChannels.EVENT_UNREGISTERED,
                DisplayName = "Listener Unregistered",
                PayloadType = "(string category, string listenerName)",
                Description = "Published when a runtime EventKit listener is unregistered.",
                SupportsThrottle = true
            };
        }
    }
}
#endif
