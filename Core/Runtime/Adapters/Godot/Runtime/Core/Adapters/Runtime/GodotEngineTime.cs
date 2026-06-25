#if GODOT
using Godot;
using YokiFrame;

namespace YokiFrame.Godot
{
    /// <summary>
    /// IEngineTime 的 Godot 实现。
    /// </summary>
    public sealed class GodotEngineTime : IEngineTime
    {
        private float mDeltaTime;
        private float mUnscaledDeltaTime;

        public float DeltaTime => mDeltaTime;
        public float UnscaledDeltaTime => mUnscaledDeltaTime;
        public float RealtimeSinceStartup => Time.GetTicksMsec() / 1000.0f;

        public void UpdateFrameTime(double delta)
        {
            var frameDelta = (float)delta;
            mDeltaTime = frameDelta;
            mUnscaledDeltaTime = frameDelta;
        }
    }
}
#endif
