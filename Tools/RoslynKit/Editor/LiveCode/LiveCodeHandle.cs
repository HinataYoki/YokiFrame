#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    public sealed class LiveCodeHandle
    {
        public string Id { get; internal set; }
        public string Kind { get; internal set; }
        public int Revision { get; internal set; }
        public string SourceHash { get; internal set; }
        public string SessionId { get; internal set; }
        public string Target { get; internal set; }
        public string Status { get; internal set; }
        public RoslynBudgetStatus Budget { get; internal set; }
    }
}
#endif
