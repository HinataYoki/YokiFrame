namespace YokiFrame
{
    /// <summary>统一动作树组装、生命周期和控制传播使用的深度上限。</summary>
    internal static class ActionTreeLimits
    {
        /// <summary>允许访问的节点深度范围为 0 到 1023。</summary>
        internal const int MAX_DEPTH = 1024;
    }
}
