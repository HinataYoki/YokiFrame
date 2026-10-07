#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 宿主当前引擎状态；由各引擎 Provider 填充，domain_state 动作与 state snapshot 共用同一份数据。
    /// </summary>
    public sealed class RoslynDomainState
    {
        /// <summary>引擎类型，例如 Unity / Godot。</summary>
        public string EngineKind = string.Empty;

        /// <summary>引擎版本文本。</summary>
        public string EngineVersion = string.Empty;

        /// <summary>宿主模式文本，例如 EditMode / PlayMode / Editor / Runtime。</summary>
        public string Mode = string.Empty;

        /// <summary>当前生效的执行目标 wire 文本。</summary>
        public string ActiveTarget = RoslynExecutionTargets.EDITOR;

        /// <summary>是否处于运行态。</summary>
        public bool IsPlaying;

        /// <summary>是否正在编译脚本。</summary>
        public bool IsCompiling;

        /// <summary>是否正在执行宿主帧工作（忙碌）。</summary>
        public bool IsBusy;

        /// <summary>宿主会话标识；不可用时为空。</summary>
        public string SessionId = string.Empty;

        /// <summary>宿主域代次；不可用时为 0。</summary>
        public long Generation;

        /// <summary>
        /// 会话身份是否可信；宿主尚未接入身份来源时必须为 false，
        /// 调用方据此判断 SessionId/Generation 是否可用于跨重载判断。
        /// </summary>
        public bool SessionIdentityAvailable;
    }
}
#endif