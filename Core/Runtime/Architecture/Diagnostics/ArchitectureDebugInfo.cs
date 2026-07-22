#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// Architecture 实例诊断快照，供命令桥、工作台和测试读取。
    /// </summary>
    public sealed class ArchitectureDebugInfo
    {
        /// <summary>
        /// 架构类型短名称。
        /// </summary>
        public string TypeName;

        /// <summary>
        /// 架构类型完整名称。
        /// </summary>
        public string FullName;

        /// <summary>
        /// 架构首次登记时间，使用 UTC ISO-8601 文本。
        /// </summary>
        public string CreatedAtUtc;

        /// <summary>
        /// 架构实例哈希值，用于区分同类型实例替换。
        /// </summary>
        public int InstanceHash;

        /// <summary>
        /// 当前架构实例是否仍处于存活状态。
        /// </summary>
        public bool IsAlive;

        /// <summary>
        /// 当前架构是否已经完成初始化。
        /// </summary>
        public bool Initialized;

        /// <summary>
        /// 当前架构注册的服务数量。
        /// </summary>
        public int ServiceCount;

        /// <summary>
        /// 当前架构注册服务的诊断快照。
        /// </summary>
        public List<ArchitectureServiceDebugInfo> Services = new();
    }
}
#endif
