#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Metadata describing one editor communication channel.
    /// </summary>
    /// <remarks>
    /// Each kit provides its own channel definitions from its editor assembly area so Core only owns the
    /// shared skeleton and discovery pipeline, not the details of every kit.
    /// </remarks>
    public sealed class EditorChannelDefinition
    {
        /// <summary>
        /// Owning kit name.
        /// </summary>
        public string Kit;

        /// <summary>
        /// Logical channel identifier.
        /// </summary>
        public string Channel;

        /// <summary>
        /// Human-readable display name for documentation and diagnostics.
        /// </summary>
        public string DisplayName;

        /// <summary>
        /// Payload type description.
        /// </summary>
        public string PayloadType;

        /// <summary>
        /// Purpose and usage description for the channel.
        /// </summary>
        public string Description;

        /// <summary>
        /// Whether throttled subscriptions are recommended for this channel.
        /// </summary>
        public bool SupportsThrottle;
    }

    /// <summary>
    /// Provider implemented by each kit to expose its editor communication channels.
    /// </summary>
    public interface IEditorChannelProvider
    {
        /// <summary>
        /// Returns all editor channels owned by the current kit.
        /// </summary>
        IEnumerable<EditorChannelDefinition> GetChannels();
    }

    /// <summary>
    /// Registry that discovers and caches all editor communication channel metadata.
    /// </summary>
    internal static class EditorChannelRegistry
    {
        private static List<EditorChannelDefinition> sChannels;

        /// <summary>
        /// Gets all registered editor channel definitions.
        /// </summary>
        public static IReadOnlyList<EditorChannelDefinition> Channels
        {
            get
            {
                if (sChannels == null)
                {
                    Collect();
                }

                return sChannels;
            }
        }

        /// <summary>
        /// Rebuilds the channel registry from all discovered providers.
        /// </summary>
        public static void Collect()
        {
            sChannels = new List<EditorChannelDefinition>(32);
            var channelKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var providerTypes = TypeCache.GetTypesDerivedFrom<IEditorChannelProvider>();

            foreach (var providerType in providerTypes)
            {
                if (providerType.IsAbstract || providerType.IsInterface)
                {
                    continue;
                }

                try
                {
                    if (Activator.CreateInstance(providerType) is not IEditorChannelProvider provider)
                    {
                        continue;
                    }

                    var channels = provider.GetChannels();
                    if (channels == null)
                    {
                        continue;
                    }

                    foreach (var channel in channels)
                    {
                        if (channel == null || string.IsNullOrEmpty(channel.Channel))
                        {
                            continue;
                        }

                        if (!channelKeys.Add(channel.Channel))
                        {
                            continue;
                        }

                        sChannels.Add(channel);
                    }
                }
                catch (Exception ex)
                {
                    UnityEngine.Debug.LogError($"[EditorChannelRegistry] Failed to load provider '{providerType.FullName}': {ex}");
                }
            }

            sChannels.Sort(EditorChannelComparer.Instance);
        }

        /// <summary>
        /// Enumerates channel definitions for a specific kit.
        /// </summary>
        public static IEnumerable<EditorChannelDefinition> GetChannelsByKit(string kit)
        {
            foreach (var channel in Channels)
            {
                if (string.Equals(channel.Kit, kit, StringComparison.OrdinalIgnoreCase))
                {
                    yield return channel;
                }
            }
        }

        /// <summary>
        /// Clears cached registry data.
        /// </summary>
        public static void ClearCache()
        {
            sChannels = null;
        }
    }

    internal sealed class EditorChannelComparer : IComparer<EditorChannelDefinition>
    {
        public static readonly EditorChannelComparer Instance = new();

        public int Compare(EditorChannelDefinition x, EditorChannelDefinition y)
        {
            if (ReferenceEquals(x, y)) return 0;
            if (x == null) return 1;
            if (y == null) return -1;

            int kitCompare = string.Compare(x.Kit, y.Kit, StringComparison.OrdinalIgnoreCase);
            if (kitCompare != 0) return kitCompare;

            return string.Compare(x.Channel, y.Channel, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
