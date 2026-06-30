namespace YokiFrame
{
    /// <summary>
    /// 托管运行时后端在当前宿主中的可用状态。
    /// </summary>
    public enum ManagedRuntimeAvailability
    {
        Unknown = 0,
        Available = 1,
        Unavailable = 2,
        NotInstalled = 3,
        Unsupported = 4
    }
}
