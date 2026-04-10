#if UNITY_EDITOR
using System.Collections.Generic;
using YokiFrame.EditorTools;

namespace YokiFrame
{
    /// <summary>
    /// Editor communication metadata for FsmKit runtime monitoring.
    /// </summary>
    internal sealed class FsmKitEditorChannelProvider : IEditorChannelProvider
    {
        public IEnumerable<EditorChannelDefinition> GetChannels()
        {
            yield return new EditorChannelDefinition
            {
                Kit = "FsmKit",
                Channel = DataChannels.FSM_LIST_CHANGED,
                DisplayName = "FSM List Changed",
                PayloadType = "IFSM",
                Description = "Published when the active FSM set changes so the monitor list can rebuild.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "FsmKit",
                Channel = DataChannels.FSM_STATE_CHANGED,
                DisplayName = "FSM State Changed",
                PayloadType = "IFSM",
                Description = "Published when an FSM changes runtime state and the detail panels need to refresh.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "FsmKit",
                Channel = DataChannels.FSM_HISTORY_LOGGED,
                DisplayName = "FSM History Logged",
                PayloadType = "FsmDebugger.TransitionEntry",
                Description = "Published when FsmKit appends a transition history entry for timeline diagnostics.",
                SupportsThrottle = false
            };
        }
    }
}
#endif
