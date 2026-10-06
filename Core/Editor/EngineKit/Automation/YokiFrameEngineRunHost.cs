#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    public sealed class YokiFrameEngineRunHost : IYokiFrameEngineRunHost
    {
        private readonly Func<YokiFrameEngineDomainState> mState;
        public YokiFrameEngineRunHost(Func<YokiFrameEngineDomainState> state) =>
            mState = state ?? throw new ArgumentNullException(nameof(state));
        public string SessionId => mState().SessionId;
        public long Generation => mState().Generation;
        public string HostTarget => mState().ActiveTarget;
    }
}
#endif
