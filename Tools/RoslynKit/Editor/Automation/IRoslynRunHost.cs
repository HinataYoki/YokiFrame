#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    public interface IRoslynRunHost
    {
        string SessionId { get; }
        long Generation { get; }
        string HostTarget { get; }
    }
}
#endif
