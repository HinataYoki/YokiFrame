#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Editor communication metadata for ResKit runtime monitoring.
    /// </summary>
    internal sealed class ResKitEditorChannelProvider : IEditorChannelProvider
    {
        public IEnumerable<EditorChannelDefinition> GetChannels()
        {
            yield return new EditorChannelDefinition
            {
                Kit = "ResKit",
                Channel = DataChannels.RES_LIST_CHANGED,
                DisplayName = "Resource List Changed",
                PayloadType = "int",
                Description = "Published when the loaded resource snapshot changes and the monitor should rebuild its category view.",
                SupportsThrottle = true
            };

            yield return new EditorChannelDefinition
            {
                Kit = "ResKit",
                Channel = DataChannels.RES_UNLOADED,
                DisplayName = "Resource Unloaded",
                PayloadType = "ResDebugger.UnloadRecord",
                Description = "Published when ResKit detects an unload and appends a history entry for diagnostics.",
                SupportsThrottle = true
            };
        }
    }
}
#endif
