namespace YokiFrame
{
    /// <summary>
    /// 可被工作台驱动的托管运行时后端。Unity、Godot 等宿主在 Adapter 中实现具体动作。
    /// </summary>
    public interface IManagedRuntimeWorkflowBackend
    {
        ManagedRuntimeActionDescriptor[] GetActions();

        ManagedRuntimeActionResult ExecuteAction(string actionId, string payloadJson);
    }
}
