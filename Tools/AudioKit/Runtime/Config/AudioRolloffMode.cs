namespace YokiFrame
{
    /// <summary>
    /// 跨引擎距离衰减模式。AudioKit 只保存语义，Unity/Godot 的具体枚举映射留在 Adapter。
    /// </summary>
    public enum AudioRolloffMode
    {
        Logarithmic = 0,
        Linear = 1,
        Custom = 2
    }
}
