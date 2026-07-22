using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>提供跨宿主 AudioKit 门面和后端生命周期所有权。</summary>
    public static partial class AudioKit
    {
        private static readonly object sLock = new();
        // 后端替换、默认实例安装和帧推进共用该短生命周期锁，避免在同步状态或 Update 期间释放实例。
        private static readonly object sBackendTransitionLock = new();
        private static readonly Queue<IAudioBackend> sPendingBackendTransitions = new();
        private static readonly Dictionary<string, float> sBusVolumes = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> sMutedBuses = new(StringComparer.OrdinalIgnoreCase);
        private static readonly HashSet<string> sRegisteredBuses = new(StringComparer.OrdinalIgnoreCase);
        private static readonly AudioKitFrameListener sFrameListener = new();
        private static Func<IAudioBackend> sDefaultBackendFactory;
        // volatile：帧 Update 热路径无锁读取；写入仍在 sLock 内完成。
        private static volatile IAudioBackend sBackend;
        private static IAudioResourceLoader sResourceLoader;
        private static long sBackendGeneration;
        private static float sMasterVolume = 1f;
        private static bool sMasterMuted;
        private static bool sFrameListenerRegistered;
        private static bool sBackendTransitionActive;

        /// <summary>获取当前已创建后端名称；默认工厂尚未使用时返回 None。</summary>
        public static string BackendName
        {
            get
            {
                lock (sLock)
                {
                    return sBackend == null ? "None" : SafeBackendName(sBackend);
                }
            }
        }

        /// <summary>获取当前是否已经创建或显式设置后端。</summary>
        public static bool HasBackend => sBackend != null;

        /// <summary>显式安装后端并接管其生命周期；会释放旧后端且跳过默认工厂。</summary>
        public static void SetBackend(IAudioBackend backend)
        {
            if (backend == null)
            {
                throw new ArgumentNullException(nameof(backend));
            }

            ReplaceBackend(backend);
        }

        /// <summary>获取当前已创建后端；读取不会触发默认工厂。</summary>
        public static IAudioBackend GetBackend() => sBackend;

        /// <summary>释放当前后端并保留已注册默认工厂和逻辑音量配置。</summary>
        public static void ClearBackend() => ReplaceBackend(null);

        /// <summary>清空当前会话后端、音量、资源加载器和工具诊断，但保留宿主默认工厂。</summary>
        public static void Reset()
        {
            ReplaceBackend(null);
            lock (sLock)
            {
                sResourceLoader = null;
                sMasterVolume = 1f;
                sMasterMuted = false;
                sBusVolumes.Clear();
                sMutedBuses.Clear();
                sRegisteredBuses.Clear();
#if UNITY_EDITOR || (GODOT && TOOLS)
                ResetDiagnosticsLocked();
#endif
            }
        }

        /// <summary>注册宿主默认后端工厂；注册不会创建或覆盖显式后端。</summary>
        internal static void RegisterDefaultBackendFactory(Func<IAudioBackend> factory)
        {
            if (factory == null)
            {
                throw new ArgumentNullException(nameof(factory));
            }

            lock (sLock)
            {
                sDefaultBackendFactory = factory;
            }
        }

        /// <summary>清除上一宿主代次全部静态状态和默认工厂。</summary>
        internal static void ResetRuntimeDefaults()
        {
            Reset();
            lock (sLock)
            {
                sDefaultBackendFactory = null;
            }
        }

        /// <summary>串行替换当前后端，完成旧实例清理和新实例状态同步后再允许下一次转换。</summary>
        private static void ReplaceBackend(IAudioBackend replacement)
        {
            lock (sBackendTransitionLock)
            {
                sPendingBackendTransitions.Enqueue(replacement);
                if (sBackendTransitionActive)
                {
                    // Monitor 对同线程可重入；延迟执行可避免在后端回调尚未返回时 Dispose 当前实例。
                    return;
                }

                sBackendTransitionActive = true;
                try
                {
                    DrainPendingBackendTransitions();
                }
                finally
                {
                    sBackendTransitionActive = false;
                }
            }
        }

        /// <summary>按请求顺序应用重入期间积累的后端替换，确保每个已接管实例都得到释放。</summary>
        private static void DrainPendingBackendTransitions()
        {
            while (sPendingBackendTransitions.Count > 0)
            {
                ApplyBackendReplacement(sPendingBackendTransitions.Dequeue());
            }
        }

        /// <summary>应用一次已串行化的后端替换，并完成旧实例清理和新实例状态同步。</summary>
        private static void ApplyBackendReplacement(IAudioBackend replacement)
        {
            IAudioBackend previous;
            lock (sLock)
            {
                previous = sBackend;
                if (ReferenceEquals(previous, replacement))
                {
                    return;
                }

                sBackend = replacement;
                System.Threading.Interlocked.Increment(ref sBackendGeneration);
                EnsureFrameListenerRegistrationLocked(replacement != null);
            }

            DisposeBackend(previous);
            if (replacement != null)
            {
                SyncBackendState(replacement);
            }
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersion();
#endif
        }

        /// <summary>根据是否存在后端安装或移除稳定帧监听者。</summary>
        private static void EnsureFrameListenerRegistrationLocked(bool required)
        {
            if (required && !sFrameListenerRegistered)
            {
                YokiFrameUpdateDispatcher.Register(sFrameListener);
                sFrameListenerRegistered = true;
            }
            else if (!required && sFrameListenerRegistered)
            {
                YokiFrameUpdateDispatcher.Unregister(sFrameListener);
                sFrameListenerRegistered = false;
            }
        }

        /// <summary>停止、卸载并释放 AudioKit 已接管的后端。</summary>
        private static void DisposeBackend(IAudioBackend backend)
        {
            if (backend == null)
            {
                return;
            }

            // 每个阶段独立兜底，避免 StopAll/UnloadAll 任一步异常跳过 Dispose，留下宿主对象或资源租约。
            try
            {
                backend.StopAll();
            }
            catch (Exception exception)
            {
                TryLogBackendCleanupFailure("StopAll", exception);
            }

            try
            {
                backend.UnloadAll();
            }
            catch (Exception exception)
            {
                TryLogBackendCleanupFailure("UnloadAll", exception);
            }

            try
            {
                backend.Dispose();
            }
            catch (Exception exception)
            {
                TryLogBackendCleanupFailure("Dispose", exception);
            }
        }

        /// <summary>尽力记录后端清理异常；日志实现失败时不得中断剩余清理阶段。</summary>
        private static void TryLogBackendCleanupFailure(string stage, Exception exception)
        {
            try
            {
                LogKit.Error("[AudioKit] Backend " + stage + " failed during disposal: " + exception);
            }
            catch (Exception)
            {
                // 清理路径不能依赖日志后端可用，否则一次日志异常会再次造成资源泄漏。
            }
        }

        /// <summary>把宿主帧转发当前后端，并在宿主重置时释放会话后端。</summary>
        private sealed class AudioKitFrameListener : IYokiFrameUpdateListener
        {
            /// <summary>使用缩放时间推进音频淡变和播放状态。无锁读 volatile 后端引用，避免每帧争用 sLock。</summary>
            public void OnFrameUpdate(float scaledDeltaTime, float unscaledDeltaTime)
            {
                // Update 不能与后端 Dispose 并发；后端自身仍在锁外执行，避免占用状态锁。
                lock (sBackendTransitionLock)
                {
                    if (sBackendTransitionActive)
                    {
                        return;
                    }

                    sBackendTransitionActive = true;
                    IAudioBackend backend = sBackend;
                    try
                    {
                        if (backend != null)
                        {
                            backend.Update(scaledDeltaTime);
                        }

                        DrainPendingBackendTransitions();
                    }
                    finally
                    {
                        sBackendTransitionActive = false;
                    }
                }
            }

            /// <summary>宿主退出当前代次时释放所有 voice 和资源。</summary>
            public void OnHostReset() => ClearBackend();
        }
    }
}
