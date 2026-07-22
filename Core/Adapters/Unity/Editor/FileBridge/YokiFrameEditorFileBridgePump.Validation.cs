#if UNITY_EDITOR

namespace YokiFrame
{
    /// <summary>为 FileBridge pump 提供 Unity 诊断会话身份和命令描述。</summary>
    internal static partial class YokiFrameEditorFileBridgePump
    {
        /// <summary>创建当前 Unity Editor 会话的只读身份快照。</summary>
        /// <returns>诊断命令响应使用的会话身份。</returns>
        private static YokiFrameUnityHarnessContext CreateHarnessContext()
        {
            return new YokiFrameUnityHarnessContext
            {
                engineId = YokiFrameEditorFileBridgePaths.ENGINE_ID,
                mode = GetEditorMode(),
                sessionId = sSessionId,
                generation = sGeneration,
                sequence = sSequence
            };
        }

        /// <summary>返回 YokiFrame 自有的 Unity 只读诊断命令描述。</summary>
        /// <returns>验证命令描述数组。</returns>
        private static YokiFrameCommandDescriptor[] CreateHarnessCommandDescriptors()
        {
            return YokiFrameUnityHarnessObservationCommandHandler.CreateCommandDescriptors();
        }
    }
}

#endif
