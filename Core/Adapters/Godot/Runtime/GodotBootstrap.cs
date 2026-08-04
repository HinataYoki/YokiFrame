#if GODOT
using System;
using Godot;

namespace YokiFrame
{
    /// <summary>
    /// Installer autoload 使用的 Godot Node，仅负责组合和驱动 Runtime Host 生命周期。
    /// </summary>
    public partial class GodotBootstrap : Node
    {
#if GODOT && TOOLS
        private const double COMMAND_POLL_INTERVAL_SECONDS = 0.2d;
        // 心跳仅承担低频 FileBridge 存活证明；Runtime Telemetry 通过 Shared Memory 发布。
        private const double HEARTBEAT_INTERVAL_SECONDS = 5.0d;
#endif
        private const double MICROSECONDS_PER_SECOND = 1000000.0d;

#if GODOT && TOOLS
        private GodotFileBridgeHost mFileBridgeHost;
        private double mCommandPollElapsed;
        private double mStateRefreshElapsed;
#endif
        private ulong mLastFrameTimestampMicroseconds;
        private bool mHasFrameTimestamp;
        private bool mClockFailureReported;

        /// <summary>
        /// 进入 autoload 场景树时只注册 Godot 默认资源工厂，不提前创建或覆盖 Provider。
        /// </summary>
        public override void _EnterTree()
        {
            ResKit.RegisterDefaultProviderFactory(CreateDefaultResourceProvider);
            GodotYokiFrameRuntimeSettingsInstaller.EnsureInstalled();
            GodotLogKitRuntimeInstaller.EnsureInstalled();
#if GODOT && TOOLS
            ConfigureToolingLogKitEnvironment();
#endif
        }

        /// <summary>仅在 ResKit 首次真实资源调用时构造 Godot 默认 Provider。</summary>
        /// <returns>新的 Godot ResourceLoader Provider。</returns>
        private static IResourceProvider CreateDefaultResourceProvider()
        {
            return new GodotResourceProvider();
        }

        /// <summary>
        /// 初始化游戏 Runtime；仅在 Godot Tools 构建中额外启动 Workbench Host。
        /// </summary>
        public override void _Ready()
        {
#if GODOT && TOOLS
            if (mFileBridgeHost != null)
            {
                return;
            }
#endif

            ResetFrameClock();
            GodotLogKitRuntimeInstaller.AttachPlayerOverlay(this);
#if GODOT && TOOLS
            var projectRoot = ProjectSettings.GlobalizePath("res://");
            StartFileBridgeHostSafely(projectRoot);
#endif
            SetProcess(true);
        }

#if GODOT && TOOLS
        /// <summary>
        /// 启动 Runtime FileBridge Host；admission 冲突或存储故障只降级通信能力，不阻断 Godot Node 生命周期。
        /// </summary>
        /// <param name="projectRoot">规范化 Godot 项目根目录。</param>
        private void StartFileBridgeHostSafely(string projectRoot)
        {
            GodotFileBridgeHost host = null;
            try
            {
                host = new GodotFileBridgeHost(projectRoot, GetGodotVersion());
                host.Start();
                mFileBridgeHost = host;
            }
            catch (YokiFrameHostAlreadyOwnedException exception)
            {
                host?.Dispose();
                mFileBridgeHost = null;
                LogKit.Warning("[FileBridge] Runtime Host already owned; continuing without tooling bridge: " + exception.Message);
            }
            catch (Exception exception)
            {
                host?.Dispose();
                mFileBridgeHost = null;
                LogKit.Warning("[FileBridge] Runtime Host start failed; continuing without tooling bridge: " + exception.Message);
            }
        }
#endif

        /// <summary>
        /// 按固定间隔驱动命令轮询和状态刷新，不在 Node 中实现协议解析或业务 handler。
        /// </summary>
        /// <param name="delta">本帧秒数。</param>
        public override void _Process(double delta)
        {
            GodotLogKitPlayerOverlay.ProcessPendingSettings();
            float scaledDeltaTime = NormalizeDeltaTime(delta);
            float unscaledDeltaTime = ReadUnscaledDeltaTime();
            YokiFrameUpdateDispatcher.Tick(scaledDeltaTime, unscaledDeltaTime);

#if GODOT && TOOLS
            if (mFileBridgeHost == null)
            {
                return;
            }

            ProcessFastChannelRequestsSafely(mFileBridgeHost);
            RefreshChangedTelemetrySafely(mFileBridgeHost);
            mCommandPollElapsed += delta;
            if (mCommandPollElapsed >= COMMAND_POLL_INTERVAL_SECONDS)
            {
                mCommandPollElapsed = 0d;
                ProcessPendingCommandsSafely(mFileBridgeHost);
            }

            mStateRefreshElapsed += delta;
            if (mStateRefreshElapsed >= HEARTBEAT_INTERVAL_SECONDS)
            {
                mStateRefreshElapsed = 0d;
                RefreshHeartbeatSafely(mFileBridgeHost);
            }
#endif
        }

#if GODOT && TOOLS
        /// <summary>
        /// 隔离 FastChannel 单帧异常；通信故障只记录到当前 Host 诊断，不阻断后续阶段。
        /// </summary>
        /// <param name="host">当前 Runtime FileBridge Host。</param>
        private static void ProcessFastChannelRequestsSafely(GodotFileBridgeHost host)
        {
            try
            {
                host.ProcessPendingFastChannelRequests();
            }
            catch (Exception exception)
            {
                host.RecordRuntimeError(exception);
            }
        }

        /// <summary>
        /// 隔离 Shared Memory telemetry 刷新异常，保留 FileBridge 命令轮询机会。
        /// </summary>
        /// <param name="host">当前 Runtime FileBridge Host。</param>
        private static void RefreshChangedTelemetrySafely(GodotFileBridgeHost host)
        {
            try
            {
                host.RefreshChangedTelemetry();
            }
            catch (Exception exception)
            {
                host.RecordRuntimeError(exception);
            }
        }

        /// <summary>
        /// 隔离 FileBridge 命令批次异常；已 claim 的命令由协调器负责 terminal evidence。
        /// </summary>
        /// <param name="host">当前 Runtime FileBridge Host。</param>
        private static void ProcessPendingCommandsSafely(GodotFileBridgeHost host)
        {
            try
            {
                host.ProcessPendingCommands();
            }
            catch (Exception exception)
            {
                host.RecordRuntimeError(exception);
            }
        }

        /// <summary>
        /// 隔离 heartbeat 写入异常，避免状态盘故障中断 Runtime 主循环。
        /// </summary>
        /// <param name="host">当前 Runtime FileBridge Host。</param>
        private static void RefreshHeartbeatSafely(GodotFileBridgeHost host)
        {
            try
            {
                host.RefreshHeartbeat();
            }
            catch (Exception exception)
            {
                host.RecordRuntimeError(exception);
            }
        }
#endif

        /// <summary>
        /// 退出场景树时释放活动 registry 和 heartbeat，避免残留在线会话。
        /// </summary>
        public override void _ExitTree()
        {
#if GODOT && TOOLS
            if (mFileBridgeHost != null)
            {
                mFileBridgeHost.Dispose();
                mFileBridgeHost = null;
            }

            LogKitHostEnvironment.Reset();
#endif

            YokiFrameUpdateDispatcher.ResetListeners();
            ClearFrameClock();
            GodotLogKitRuntimeInstaller.Shutdown();
            ResKit.ResetRuntimeDefaults();
            LogKit.Reset();
            KitSettings.Reset();
        }

        /// <summary>
        /// 在宿主 Ready 时建立单调时钟基线，避免首个 Process 把进程启动时长当成本帧时间。
        /// </summary>
        private void ResetFrameClock()
        {
            mHasFrameTimestamp = TryReadFrameTimestamp(out mLastFrameTimestampMicroseconds);
        }

        /// <summary>
        /// 读取本帧与上帧单调时间差；首帧、计数回退或读取异常时返回零，避免时间跳跃。
        /// </summary>
        /// <returns>不受 Engine.TimeScale 影响的有限非负秒数。</returns>
        private float ReadUnscaledDeltaTime()
        {
            if (!TryReadFrameTimestamp(out ulong currentTimestamp))
            {
                mHasFrameTimestamp = false;
                return 0f;
            }

            if (!mHasFrameTimestamp || currentTimestamp < mLastFrameTimestampMicroseconds)
            {
                mLastFrameTimestampMicroseconds = currentTimestamp;
                mHasFrameTimestamp = true;
                return 0f;
            }

            ulong elapsedMicroseconds = currentTimestamp - mLastFrameTimestampMicroseconds;
            mLastFrameTimestampMicroseconds = currentTimestamp;
            return NormalizeDeltaTime(elapsedMicroseconds / MICROSECONDS_PER_SECOND);
        }

        /// <summary>
        /// 从 Godot Time 读取不会受游戏时间缩放影响的微秒计数；异常只报告一次，防止日志风暴。
        /// </summary>
        /// <param name="timestamp">成功时返回当前单调微秒计数。</param>
        /// <returns>读取成功时返回 true。</returns>
        private bool TryReadFrameTimestamp(out ulong timestamp)
        {
            try
            {
                timestamp = Time.GetTicksUsec();
                mClockFailureReported = false;
                return true;
            }
            catch (Exception exception)
            {
                timestamp = 0UL;
                if (!mClockFailureReported)
                {
                    mClockFailureReported = true;
                    LogKit.Warning("[FrameLoop] Godot monotonic clock read failed: " + exception.Message);
                }

                return false;
            }
        }

        /// <summary>
        /// 把 Godot double 时间转换为 Core 使用的 float，并把负数、NaN、无穷和溢出值降为零。
        /// </summary>
        /// <param name="deltaTime">Godot 提供或单调时钟计算出的秒数。</param>
        /// <returns>可安全投递给 Core 的有限非负秒数。</returns>
        private static float NormalizeDeltaTime(double deltaTime)
        {
            if (deltaTime < 0d || double.IsNaN(deltaTime) || double.IsInfinity(deltaTime) || deltaTime > float.MaxValue)
            {
                return 0f;
            }

            return (float)deltaTime;
        }

        /// <summary>
        /// 清除退出宿主代际后的时钟状态，避免同一 Node 生命周期外复用旧时间基线。
        /// </summary>
        private void ClearFrameClock()
        {
            mLastFrameTimestampMicroseconds = 0UL;
            mHasFrameTimestamp = false;
            mClockFailureReported = false;
        }

#if GODOT && TOOLS
        /// <summary>
        /// 配置 Godot Tools Play Mode 使用的 LogKit 文件位置和工具能力，不创建默认 logger。
        /// </summary>
        private static void ConfigureToolingLogKitEnvironment()
        {
            LogKitHostEnvironment.Configure(
                ProjectSettings.GlobalizePath("user://LogFiles"),
                true,
                true,
                false,
                true,
                false);
        }

        /// <summary>
        /// 读取 Godot 自身版本；失败时返回固定宿主基线，保证 Tools Host 仍可发布诊断。
        /// </summary>
        /// <returns>Godot 版本文本。</returns>
        private static string GetGodotVersion()
        {
            try
            {
                var versionInfo = Engine.GetVersionInfo();
                if (versionInfo.TryGetValue("string", out var version))
                {
                    var versionText = version.ToString();
                    return string.IsNullOrWhiteSpace(versionText) ? "4.7" : versionText;
                }
            }
            catch (Exception)
            {
                return "4.7";
            }

            return "4.7";
        }
#endif
    }
}
#endif
