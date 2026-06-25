namespace YokiFrame
{
    /// <summary>
    /// YokiFrame 内置宿主引擎类型。第三方宿主可使用 Custom 并提供自定义 EngineId。
    /// </summary>
    public enum YokiFrameEngine
    {
        Unknown = 0,
        Unity = 1,
        Godot = 2,
        Custom = 1000
    }
}
