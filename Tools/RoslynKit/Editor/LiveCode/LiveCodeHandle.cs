#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>一次 LiveCode 挂接的句柄。字段由管理器写入，调用方只读。</summary>
    public sealed class LiveCodeHandle
    {
        /// <summary>行为或补丁 ID。</summary>
        public string Id { get; internal set; }

        /// <summary>句柄种类，例如 behaviour。</summary>
        public string Kind { get; internal set; }

        /// <summary>同一 ID 被替换后的递增修订号。</summary>
        public int Revision { get; internal set; }

        /// <summary>成员源码哈希。</summary>
        public string SourceHash { get; internal set; }

        /// <summary>挂接时的会话 ID。会话变化后句柄不再有效。</summary>
        public string SessionId { get; internal set; }

        /// <summary>挂接时的执行目标。</summary>
        public string Target { get; internal set; }

        /// <summary>当前状态说明。</summary>
        public string Status { get; internal set; }

        /// <summary>挂接完成时的程序集预算快照。</summary>
        public RoslynBudgetStatus Budget { get; internal set; }
    }
}
#endif
