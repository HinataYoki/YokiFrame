#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System.Threading.Tasks;

namespace YokiFrame
{
    /// <summary>Lifecycle for in-memory host work.</summary>
    public interface IYokiFrameEngineRunLifetime
    {
        int Frames { get; }
        int StaleContextCalls { get; }
        void AdvanceFrame();
        void Invalidate();
    }

    /// <summary>Transient work is never rediscovered or replayed after a domain reload.</summary>
    public interface IYokiFrameEngineRunWork : IYokiFrameEngineRunLifetime
    {
        bool IsCompiling { get; }
        Task<YokiFrameRunResult> Start();
    }
}
#endif
