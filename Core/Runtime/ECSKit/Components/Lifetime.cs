namespace YokiFrame.ECS
{
    /// <summary>
    /// 生命周期组件 - 超时后自动销毁
    /// </summary>
    public struct Lifetime : IComponentData
    {
        public float Duration;
        public float Elapsed;
        
        public Lifetime(float duration)
        {
            Duration = duration;
            Elapsed = 0;
        }
        
        public bool IsExpired => Elapsed >= Duration;
        public float Remaining => Duration - Elapsed;
        public float Percent => Duration > 0 ? Elapsed / Duration : 1;
    }
}
