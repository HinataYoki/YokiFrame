#if GODOT && TOOLS
using System;
using System.IO;

namespace YokiFrame
{
    /// <summary>
    /// 驱动 Godot Editor 的 FileBridge 注册、心跳、最小命令面和会话释放。
    /// </summary>
    public sealed partial class GodotEditorFileBridgeHost : IDisposable
    {
        /// <summary>
        /// Godot Editor 在项目 FileBridge registry 中的稳定 engine ID。
        /// </summary>
        public const string ENGINE_ID = "godot-editor";

        private const string EDITOR_MODE = "Editor";
        private static readonly string[] sCapabilities =
        {
            "command.send",
            "bridge.status"
        };

        private readonly string mEngineVersion;
        private readonly GodotEditorFileBridgePaths mPaths;
        private readonly YokiFrameCommandDispatcher mDispatcher;
        private bool mIsProcessingCommands;
        private string mLastError = string.Empty;
        private string mSessionId = string.Empty;
        private string mStartedAtUtc = string.Empty;
        private long mGeneration;
        private long mSequence;
        private DateTime mNextStorageCleanupUtc;

        /// <summary>
        /// 创建绑定指定 Godot 项目的 Editor Host，不访问 Runtime Kit 状态。
        /// </summary>
        /// <param name="projectRoot">Godot 项目根。</param>
        /// <param name="engineVersion">Godot 编辑器版本。</param>
        public GodotEditorFileBridgeHost(string projectRoot, string engineVersion)
        {
            if (string.IsNullOrWhiteSpace(engineVersion))
            {
                throw new ArgumentException("Godot engine version is required.", nameof(engineVersion));
            }

            mEngineVersion = engineVersion;
            mPaths = new GodotEditorFileBridgePaths(projectRoot);
            mDispatcher = CreateCommandDispatcher();
        }

        /// <summary>获取当前 Editor 会话是否已启动。</summary>
        public bool IsRunning { get; private set; }

        /// <summary>获取当前会话安全标识。</summary>
        public string SessionId => mSessionId;

        /// <summary>获取当前 Editor generation。</summary>
        public long Generation => mGeneration;

        /// <summary>获取当前状态发布序号。</summary>
        public long Sequence => mSequence;

        /// <summary>
        /// 创建新 Editor session/generation，并立即发布 registry 与首个 heartbeat。
        /// </summary>
        public void Start()
        {
            if (IsRunning)
            {
                return;
            }

            var now = DateTimeOffset.UtcNow;
            mSessionId = Guid.NewGuid().ToString("N");
            mGeneration = CreateNextGeneration(now.UtcDateTime.Ticks);
            mSequence = 0;
            mLastError = string.Empty;
            mStartedAtUtc = now.ToString("O");
            IsRunning = true;
            try
            {
                mPaths.EnsureDirectories();
                TryPruneStorage();
                PublishInitialState();
            }
            catch
            {
                IsRunning = false;
                ReleaseActiveState();
                throw;
            }
        }

        /// <summary>
        /// 只更新 heartbeat；registry 身份未变化时不重复写入。
        /// </summary>
        public void RefreshHeartbeat()
        {
            EnsureRunning();
            mSequence++;
            mPaths.EnsureDirectories();
            WriteHeartbeat();
        }

        /// <summary>
        /// 停止当前 Editor 会话并删除活动 registry/heartbeat，保留命令终态证据。
        /// </summary>
        public void Stop()
        {
            if (!IsRunning)
            {
                return;
            }

            IsRunning = false;
            ReleaseActiveState();
        }

        /// <summary>
        /// 释放 Host，语义等同于幂等 Stop。
        /// </summary>
        public void Dispose()
        {
            Stop();
        }

        /// <summary>
        /// 发布当前 generation 的首个 heartbeat 和只写一次的 registry。
        /// </summary>
        private void PublishInitialState()
        {
            mSequence++;
            WriteHeartbeat();
            WriteEngineRegistry();
        }

        /// <summary>
        /// 创建严格大于当前 generation 的新代际。
        /// </summary>
        /// <param name="utcTicks">当前 UTC ticks。</param>
        /// <returns>单调递增 generation。</returns>
        private long CreateNextGeneration(long utcTicks)
        {
            return utcTicks > mGeneration ? utcTicks : mGeneration + 1;
        }

        /// <summary>
        /// 写入只声明 FileBridge System 控制面的 Godot Editor registry。
        /// </summary>
        private void WriteEngineRegistry()
        {
            GodotEditorEngineRegistry registry = new GodotEditorEngineRegistry
            {
                Version = mEngineVersion,
                ProjectPath = mPaths.ProjectRoot.Replace('\\', '/'),
                SessionId = mSessionId,
                Generation = mGeneration,
                Mode = EDITOR_MODE,
                StartedAtUtc = mStartedAtUtc,
                RegisteredAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                Capabilities = sCapabilities
            };
            GodotEditorFileBridgeJson.WriteAtomic(
                mPaths.RegistryPath,
                GodotEditorFileBridgeJson.Serialize(registry));
        }

        /// <summary>
        /// 写入与当前 Editor session、generation 和 sequence 对齐的 heartbeat。
        /// </summary>
        private void WriteHeartbeat()
        {
            var nowUtc = DateTimeOffset.UtcNow.ToString("O");
            GodotEditorHeartbeat heartbeat = new GodotEditorHeartbeat
            {
                SessionId = mSessionId,
                Generation = mGeneration,
                Mode = EDITOR_MODE,
                Sequence = mSequence,
                CreatedAtUtc = nowUtc,
                WrittenAtUtc = nowUtc
            };
            GodotEditorFileBridgeJson.WriteAtomic(
                mPaths.HeartbeatPath,
                GodotEditorFileBridgeJson.Serialize(heartbeat));
        }

        /// <summary>
        /// 删除当前活动 registry 与 heartbeat，使编辑器退出后不会继续显示在线。
        /// </summary>
        private void ReleaseActiveState()
        {
            DeleteIfExists(mPaths.RegistryPath);
            DeleteIfExists(mPaths.HeartbeatPath);
        }

        /// <summary>
        /// 删除存在的活动状态文件，缺失时保持幂等。
        /// </summary>
        /// <param name="path">待删除路径。</param>
        private static void DeleteIfExists(string path)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        /// <summary>
        /// 拒绝在未启动或已停止的 Editor 会话上执行协议操作。
        /// </summary>
        private void EnsureRunning()
        {
            if (!IsRunning)
            {
                throw new InvalidOperationException("Godot Editor FileBridge Host is not running.");
            }
        }
    }
}
#endif
