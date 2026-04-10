#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Entry point for EventKit documentation sections.
    /// </summary>
    internal static class EventKitDocData
    {
        /// <summary>
        /// Returns all EventKit documentation sections.
        /// </summary>
        internal static List<DocSection> GetAllSections()
        {
            return new List<DocSection>
            {
                EventKitDocEnum.CreateSection(),
                EventKitDocType.CreateSection(),
                EventKitDocString.CreateSection(),
                EventKitDocEasyEvent.CreateSection(),
                EventKitDocChannel.CreateSection(),
                EventKitDocAdvanced.CreateSection()
            };
        }
    }

    /// <summary>
    /// Documentation provider for EventKit.
    /// </summary>
    internal sealed class EventKitDocumentationProvider : IDocumentationModuleProvider
    {
        public IEnumerable<DocModule> GetModules()
        {
            yield return new DocModule
            {
                Name = "EventKit",
                Icon = KitIcons.EVENTKIT,
                Category = "CORE KIT",
                Description = "Typed event system with enum events, easy events, and auto unregister helpers.",
                Keywords = new List<string> { "Event", "Message", "Publish", "Subscribe" },
                Sections = EventKitDocData.GetAllSections()
            };
        }
    }
}
#endif
