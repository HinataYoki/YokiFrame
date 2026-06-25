using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// InputKit 宿主输入后端。
    /// </summary>
    public interface IInputBackend
    {
        /// <summary>后端名称。</summary>
        string BackendName { get; }

        /// <summary>当前主要输入设备类型。</summary>
        InputDeviceType CurrentDeviceType { get; }

        /// <summary>是否已连接手柄。</summary>
        bool IsGamepadConnected { get; }

        /// <summary>
        /// 轮询输入状态并写入统一输入状态缓冲。
        /// </summary>
        void Poll(IInputStateWriter writer);

        /// <summary>
        /// 设置当前启用的动作映射。
        /// </summary>
        void SetEnabledActionMaps(IReadOnlyList<string> actionMapNames);
    }
}
