using System;

namespace YokiFrame
{
    /// <summary>
    /// 托管运行时后端暴露给框架和工具链的能力位。
    /// </summary>
    [Flags]
    public enum ManagedRuntimeCapabilities
    {
        None = 0,
        HostExecution = 1 << 0,
        AssemblyInspection = 1 << 1,
        DynamicAssemblyLoad = 1 << 2,
        AotCompilation = 1 << 3,
        Interpreter = 1 << 4,
        HotUpdateAssembly = 1 << 5,
        BuildPipelineControl = 1 << 6,
        Diagnostics = 1 << 7
    }
}
