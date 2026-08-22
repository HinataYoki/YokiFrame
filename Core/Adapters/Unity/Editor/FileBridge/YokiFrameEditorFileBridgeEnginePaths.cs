#if UNITY_EDITOR
using System;

namespace YokiFrame
{
    /// <summary>
    /// 把既有静态 Unity Editor 路径类适配为共享命令存储所需的 engine 路径契约；
    /// 仅做委托转发，不持有状态，生命周期跟随静态路径类。
    /// </summary>
    internal sealed class YokiFrameEditorFileBridgeEnginePaths : IYokiFrameFileBridgeEnginePaths
    {
        /// <summary>获取宿主项目根绝对路径。</summary>
        public string ProjectRoot => YokiFrameEditorFileBridgePaths.GetProjectRoot();

        /// <summary>获取 unity-editor engine 协议根目录。</summary>
        public string EngineRoot => YokiFrameEditorFileBridgePaths.GetEngineRoot();

        /// <summary>获取待处理命令目录。</summary>
        public string CommandsRoot => YokiFrameEditorFileBridgePaths.GetCommandsRoot();

        /// <summary>获取已认领命令 processing 目录。</summary>
        public string ProcessingRoot => YokiFrameEditorFileBridgePaths.GetProcessingRoot();

        /// <summary>获取已完成命令归档目录。</summary>
        public string ArchiveRoot => YokiFrameEditorFileBridgePaths.GetArchiveRoot();

        /// <summary>获取 deadletter 目录。</summary>
        public string DeadletterRoot => YokiFrameEditorFileBridgePaths.GetDeadletterRoot();

        /// <summary>获取 terminal response 目录。</summary>
        public string ResultsRoot => YokiFrameEditorFileBridgePaths.GetResultsRoot();

        /// <summary>复核固定协议根无重解析点且未逃逸项目根。</summary>
        public void EnsureReady()
        {
            YokiFrameEditorFileBridgePaths.EnsureBridgeRootsAreSafe();
        }

        /// <summary>解析指定请求的 terminal response 完整路径。</summary>
        /// <param name="requestId">安全请求标识。</param>
        public string GetResponsePath(string requestId)
        {
            return YokiFrameEditorFileBridgePaths.GetResponsePath(requestId);
        }

        /// <summary>解析指定命令文件的归档目标路径。</summary>
        /// <param name="commandPath">原始命令文件完整路径。</param>
        public string GetArchivePath(string commandPath)
        {
            return YokiFrameEditorFileBridgePaths.GetArchivePath(commandPath);
        }

        /// <summary>解析指定 deadletter 标识的诊断文件路径。</summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        public string GetDeadletterInfoPath(string deadletterId)
        {
            return YokiFrameEditorFileBridgePaths.GetDeadletterInfoPath(deadletterId);
        }

        /// <summary>解析指定 deadletter 标识的原始请求证据路径。</summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        public string GetDeadletterRequestPath(string deadletterId)
        {
            return YokiFrameEditorFileBridgePaths.GetDeadletterRequestPath(deadletterId);
        }
    }
}
#endif
