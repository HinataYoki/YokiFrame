#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 对象池诊断事件类型。
    /// </summary>
    public enum PoolEventType
    {
        /// <summary>
        /// 对象被借出。
        /// </summary>
        Spawn,

        /// <summary>
        /// 对象被归还。
        /// </summary>
        Return,

        /// <summary>
        /// 对象被诊断工具强制归还。
        /// </summary>
        Forced
    }

    /// <summary>
    /// 对象池事件记录。
    /// </summary>
    public sealed class PoolEvent
    {
        /// <summary>
        /// 事件所属对象池的稳定诊断标识。
        /// </summary>
        public string PoolId;

        /// <summary>
        /// 事件类型。
        /// </summary>
        public PoolEventType EventType;

        /// <summary>
        /// 相对 PoolDebugger 初始化时刻的秒数。
        /// </summary>
        public float Timestamp;

        /// <summary>
        /// 对象池名称。
        /// </summary>
        public string PoolName;

        /// <summary>
        /// 对象显示名。
        /// </summary>
        public string ObjectName;

        /// <summary>
        /// 触发事件的调用来源。
        /// </summary>
        public string Source;

        /// <summary>
        /// 调用位置文件。
        /// </summary>
        public string SourceFile;

        /// <summary>
        /// 调用位置行号。
        /// </summary>
        public int SourceLine;

        /// <summary>
        /// 完整调用堆栈。
        /// </summary>
        public string StackTrace;

        /// <summary>
        /// 对象引用，用于诊断工具执行强制归还。
        /// </summary>
        public object ObjRef;
    }
}
#endif
