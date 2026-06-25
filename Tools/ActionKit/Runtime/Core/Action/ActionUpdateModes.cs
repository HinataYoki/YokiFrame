namespace YokiFrame
{
    /// <summary>
    /// ActionKit 调度时间源。
    /// </summary>
    public enum ActionUpdateModes
    {
        /// <summary>
        /// 使用宿主缩放时间。
        /// </summary>
        ScaledDeltaTime,

        /// <summary>
        /// 使用宿主非缩放时间。
        /// </summary>
        UnscaledDeltaTime,
    }
}
