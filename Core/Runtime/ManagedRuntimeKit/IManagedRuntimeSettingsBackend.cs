namespace YokiFrame
{
    /// <summary>
    /// 可选的托管运行时设置后端。Core 只传递 JSON，具体设置模型留给宿主 Adapter。
    /// </summary>
    public interface IManagedRuntimeSettingsBackend
    {
        string GetSettingsJson();

        ManagedRuntimeActionResult SaveSettings(string payloadJson);
    }
}
