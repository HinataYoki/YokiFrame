using System.Numerics;

namespace YokiFrame
{
    /// <summary>为跨宿主 3D voice 提供窄位置跟随契约。</summary>
    public interface IAudioFollowTarget
    {
        /// <summary>获取诊断使用的稳定目标名称。</summary>
        string Name { get; }

        /// <summary>获取目标当前是否仍可读取。</summary>
        bool IsAlive { get; }

        /// <summary>获取目标当前世界位置。</summary>
        Vector3 Position { get; }
    }
}
