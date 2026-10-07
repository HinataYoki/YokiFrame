#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>把宿主域状态投影成运行身份。每次读取都重新取状态，不缓存过期会话。</summary>
    public sealed class RoslynRunHost : IRoslynRunHost
    {
        private readonly Func<RoslynDomainState> mState;

        /// <summary>创建运行身份。</summary>
        /// <param name="state">当前域状态。不能为空。</param>
        public RoslynRunHost(Func<RoslynDomainState> state) =>
            mState = state ?? throw new ArgumentNullException(nameof(state));

        /// <summary>当前会话 ID。</summary>
        public string SessionId => mState().SessionId;

        /// <summary>当前域世代。重载后变化。</summary>
        public long Generation => mState().Generation;

        /// <summary>当前活动执行目标。</summary>
        public string HostTarget => mState().ActiveTarget;
    }
}
#endif
