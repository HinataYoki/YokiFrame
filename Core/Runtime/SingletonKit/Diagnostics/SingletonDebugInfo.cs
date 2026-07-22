#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// 表示一个单例实例的运行时诊断快照。
    /// </summary>
    public sealed class SingletonDebugInfo
    {
        /// <summary>
        /// 单例类型短名称。
        /// </summary>
        public string TypeName;

        /// <summary>
        /// 单例类型完整名称。
        /// </summary>
        public string FullName;

        /// <summary>
        /// 单例后端类型，例如 Base、Unity 或 Godot。
        /// </summary>
        public string Backend;

        /// <summary>
        /// 创建来源，例如 SingletonKit、MonoSingleton 或 GodotSingleton。
        /// </summary>
        public string Source;

        /// <summary>
        /// 实例创建时间，使用 UTC ISO-8601 文本。
        /// </summary>
        public string CreatedAtUtc;

        /// <summary>
        /// 实例哈希值，用于区分同类型实例替换。
        /// </summary>
        public int InstanceHash;

        /// <summary>
        /// 当前登记实例是否仍处于存活状态。
        /// </summary>
        public bool IsAlive;
    }
}
#endif
