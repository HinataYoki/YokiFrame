#if UNITY_EDITOR || (GODOT && TOOLS)
namespace YokiFrame
{
    /// <summary>
    /// Architecture 服务注册诊断快照，只记录低频生命周期状态。
    /// </summary>
    public sealed class ArchitectureServiceDebugInfo
    {
        /// <summary>
        /// 服务注册类型短名称。
        /// </summary>
        public string TypeName;

        /// <summary>
        /// 服务注册类型完整名称。
        /// </summary>
        public string FullName;

        /// <summary>
        /// 服务实现类型短名称。
        /// </summary>
        public string ImplementationTypeName;

        /// <summary>
        /// 服务实现类型完整名称。
        /// </summary>
        public string ImplementationFullName;

        /// <summary>
        /// 服务是否已经完成初始化。
        /// </summary>
        public bool Initialized;

        /// <summary>
        /// 服务实例哈希值，用于区分同类型服务替换。
        /// </summary>
        public int InstanceHash;
    }
}
#endif
