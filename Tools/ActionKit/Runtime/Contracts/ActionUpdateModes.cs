namespace YokiFrame
{
    /// <summary>
    /// 选择 controller 从宿主帧中消费的时间源。
    /// </summary>
    public enum ActionUpdateModes
    {
        /// <summary>使用受宿主时间缩放影响的 delta time。</summary>
        ScaledDeltaTime,

        /// <summary>使用不受宿主时间缩放影响的 delta time。</summary>
        UnscaledDeltaTime
    }
}
