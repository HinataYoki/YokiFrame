namespace YokiFrame
{
    /// <summary>
    /// 接收宿主每帧的缩放与非缩放时间，并在宿主生命周期重置时关闭活动状态。
    /// </summary>
    public interface IYokiFrameUpdateListener
    {
        /// <summary>
        /// 使用宿主提供的两个时间源推进一次纯 C# Runtime 更新。
        /// </summary>
        /// <param name="scaledDeltaTime">受宿主时间缩放影响的秒数。</param>
        /// <param name="unscaledDeltaTime">不受宿主时间缩放影响的秒数。</param>
        void OnFrameUpdate(float scaledDeltaTime, float unscaledDeltaTime);

        /// <summary>
        /// 宿主停止、重载或重新进入运行态前清理活动状态；监听注册本身继续保留。
        /// </summary>
        void OnHostReset();
    }
}
