#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 定义宿主无关的 engine 协议路径契约；共享命令存储与状态发布器只依赖本契约，
    /// 各宿主 Paths 类型负责提供等价实现，避免三宿主重复维护同一套目录移动逻辑。
    /// </summary>
    internal interface IYokiFrameFileBridgeEnginePaths
    {
        /// <summary>获取宿主项目根绝对路径。</summary>
        string ProjectRoot { get; }

        /// <summary>获取当前 engine 的协议根目录。</summary>
        string EngineRoot { get; }

        /// <summary>获取待处理命令目录。</summary>
        string CommandsRoot { get; }

        /// <summary>获取已认领命令 processing 目录。</summary>
        string ProcessingRoot { get; }

        /// <summary>获取已完成命令归档目录。</summary>
        string ArchiveRoot { get; }

        /// <summary>获取 deadletter 目录。</summary>
        string DeadletterRoot { get; }

        /// <summary>获取 terminal response 目录。</summary>
        string ResultsRoot { get; }

        /// <summary>复核当前协议路径仍安全（无重解析点、未逃逸项目根）；由每轮命令处理前调用。</summary>
        void EnsureReady();

        /// <summary>解析指定请求的 terminal response 完整路径。</summary>
        /// <param name="requestId">安全请求标识。</param>
        string GetResponsePath(string requestId);

        /// <summary>解析指定命令文件的归档目标路径。</summary>
        /// <param name="commandPath">原始命令文件完整路径。</param>
        string GetArchivePath(string commandPath);

        /// <summary>解析指定 deadletter 标识的诊断文件路径。</summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        string GetDeadletterInfoPath(string deadletterId);

        /// <summary>解析指定 deadletter 标识的原始请求证据路径。</summary>
        /// <param name="deadletterId">安全 deadletter 标识。</param>
        string GetDeadletterRequestPath(string deadletterId);
    }
}
#endif
