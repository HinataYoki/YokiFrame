#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    public sealed class RoslynRunHost : IRoslynRunHost
    {
        private readonly Func<RoslynDomainState> mState;
        public RoslynRunHost(Func<RoslynDomainState> state) =>
            mState = state ?? throw new ArgumentNullException(nameof(state));
        public string SessionId => mState().SessionId;
        public long Generation => mState().Generation;
        public string HostTarget => mState().ActiveTarget;
    }
}
#endif
