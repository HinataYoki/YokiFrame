namespace YokiFrame
{
    /// <summary>
    /// 引擎时间抽象接口，替代 Unity 的 Time.deltaTime 等
    /// </summary>
    public interface IEngineTime
    {
        /// <summary>
        /// 获取上一帧到当前帧的缩放时间间隔。
        /// </summary>
        float DeltaTime { get; }

        /// <summary>
        /// 获取上一帧到当前帧的未缩放时间间隔。
        /// </summary>
        float UnscaledDeltaTime { get; }

        /// <summary>
        /// 获取引擎启动以来经过的真实时间。
        /// </summary>
        float RealtimeSinceStartup { get; }
    }
}
