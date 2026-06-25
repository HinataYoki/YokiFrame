using System;

namespace YokiFrame
{
    public static partial class ResKit
    {
        private static IResourceProvider EnsureProvider()
        {
            if (sProvider == null)
                throw new InvalidOperationException("ResKit provider is not configured. Call ResKit.SetProvider from an engine adapter first.");

            return sProvider;
        }
    }
}
