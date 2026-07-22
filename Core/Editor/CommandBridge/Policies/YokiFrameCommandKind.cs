#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
namespace YokiFrame
{
    /// <summary>
    /// 描述 CommandBridge 命令的风险等级，供策略层决定是否需要额外确认或权限。
    /// </summary>
    public enum YokiFrameCommandKind
    {
        /// <summary>
        /// 只读取状态，不修改项目文件、运行时对象或外部系统。
        /// </summary>
        ReadOnly,

        /// <summary>
        /// 维护型命令，可刷新缓存或重新发布状态，但不触碰用户业务资产。
        /// </summary>
        Maintenance,

        /// <summary>
        /// 用户显式触发的普通变更命令。
        /// </summary>
        UserAction,

        /// <summary>
        /// 可能覆盖、删除、迁移或影响用户资产的危险命令。
        /// </summary>
        Dangerous
    }
}
#endif
