namespace YokiFrame
{
    /// <summary>
    /// 输入后端写入动作状态的统一接口。
    /// </summary>
    public interface IInputStateWriter
    {
        /// <summary>
        /// 写入一个输入动作在当前帧的状态。
        /// </summary>
        void SetAction(string actionName, bool isPressed, float value);
    }
}
