using System;

namespace YokiFrame
{
    internal interface IResHandleDebugView
    {
        string Path { get; }
        Type AssetType { get; }
        object AssetObject { get; }
        string ProviderName { get; }
        string Source { get; }
        string SourceFile { get; }
        int SourceLine { get; }
        int RefCount { get; }
        bool IsDone { get; }
    }

    internal interface IResHandleInvalidator
    {
        void Invalidate();
    }

    internal interface IResHandleReleaser
    {
        bool TryReleaseObject(object asset);
    }
}
