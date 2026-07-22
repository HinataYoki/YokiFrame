using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public static partial class AudioKit
    {
        /// <summary>返回当前后端；为空时通过宿主工厂惰性创建并安装一次。工厂在锁外调用，避免重入死锁。</summary>
        private static IAudioBackend EnsureBackend()
        {
            IAudioBackend existing = sBackend;
            if (existing != null)
            {
                return existing;
            }

            Func<IAudioBackend> factory;
            lock (sLock)
            {
                if (sBackend != null)
                {
                    return sBackend;
                }

                factory = sDefaultBackendFactory;
                if (factory == null)
                {
                    throw new InvalidOperationException(
                        "AudioKit backend is not configured. Install an engine adapter or call AudioKit.SetBackend first.");
                }
            }

            IAudioBackend created = factory();
            if (created == null)
            {
                throw new InvalidOperationException("The AudioKit default backend factory returned null.");
            }

            return InstallCreatedBackend(created);
        }

        /// <summary>串行提交默认工厂结果；重入替换延迟到安装或清理回调返回后再执行。</summary>
        private static IAudioBackend InstallCreatedBackend(IAudioBackend created)
        {
            lock (sBackendTransitionLock)
            {
                bool ownsTransition = !sBackendTransitionActive;
                if (ownsTransition)
                {
                    sBackendTransitionActive = true;
                }

                try
                {
                    IAudioBackend discarded = null;
                    bool installedCreated = false;
                    lock (sLock)
                    {
                        if (sBackend != null)
                        {
                            if (!ReferenceEquals(sBackend, created)) discarded = created;
                        }
                        else
                        {
                            sBackend = created;
                            System.Threading.Interlocked.Increment(ref sBackendGeneration);
                            EnsureFrameListenerRegistrationLocked(true);
                            installedCreated = true;
                        }
                    }

                    if (discarded != null) DisposeBackend(discarded);
                    if (installedCreated) SyncBackendState(created);
                    if (ownsTransition) DrainPendingBackendTransitions();

#if UNITY_EDITOR || (GODOT && TOOLS)
                    if (installedCreated) BumpDiagnosticVersion();
#endif
                    IAudioBackend current = sBackend;
                    if (current == null)
                    {
                        throw new InvalidOperationException("AudioKit backend was cleared during default backend installation.");
                    }

                    return current;
                }
                finally
                {
                    if (ownsTransition) sBackendTransitionActive = false;
                }
            }
        }

        /// <summary>把门面保存的 Master 与全部逻辑总线状态同步到新后端。</summary>
        private static void SyncBackendState(IAudioBackend backend)
        {
            List<string> buses = new();
            lock (sLock)
            {
                AddDefaultBuses(buses);
                foreach (string bus in sRegisteredBuses)
                {
                    AddBusName(buses, bus);
                }

                foreach (string bus in sBusVolumes.Keys)
                {
                    AddBusName(buses, bus);
                }

                foreach (string bus in sMutedBuses)
                {
                    AddBusName(buses, bus);
                }
            }

            backend.SetBusVolume(AudioBus.Master, GetEffectiveMasterVolume());
            for (var index = 0; index < buses.Count; index++)
            {
                backend.SetBusVolume(buses[index], GetEffectiveBusVolume(buses[index]));
            }
        }

        /// <summary>验证并规范化播放参数，后端只接收明确跨宿主语义。</summary>
        private static AudioPlayOptions NormalizeOptions(AudioPlayOptions options)
        {
            options.Bus = NormalizePlayableBus(options.Bus);
            options.Volume = NormalizeUnitValue(options.Volume, nameof(options.Volume));
            options.Pitch = NormalizePitch(options.Pitch);
            options.FadeInDuration = NormalizeDuration(options.FadeInDuration, nameof(options.FadeInDuration));
            options.FadeOutDuration = NormalizeDuration(options.FadeOutDuration, nameof(options.FadeOutDuration));
            options.MinDistance = NormalizeDistance(options.MinDistance, 1f, nameof(options.MinDistance));
            options.MaxDistance = NormalizeDistance(options.MaxDistance, 500f, nameof(options.MaxDistance));
            if (options.MaxDistance < options.MinDistance)
            {
                options.MaxDistance = options.MinDistance;
            }

            if (!IsKnownRolloffMode(options.RolloffMode))
            {
                throw new ArgumentOutOfRangeException(nameof(options.RolloffMode), options.RolloffMode, "Unknown audio rolloff mode.");
            }

            if (options.FollowTarget != null)
            {
                options.Is3D = true;
                if (options.FollowTarget.IsAlive)
                {
                    options.Position = options.FollowTarget.Position;
                }
            }

            return options;
        }

        /// <summary>验证资源路径非空并规范化分隔符；无空白且无反斜杠时复用原引用。</summary>
        private static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Audio path cannot be empty.", nameof(path));
            }

            var needsTrim = char.IsWhiteSpace(path[0]) || char.IsWhiteSpace(path[path.Length - 1]);
            var needsSlash = false;
            for (var index = 0; index < path.Length; index++)
            {
                if (path[index] == '\\')
                {
                    needsSlash = true;
                    break;
                }
            }

            if (!needsTrim && !needsSlash)
            {
                return path;
            }

            string normalized = needsTrim ? path.Trim() : path;
            return needsSlash ? normalized.Replace('\\', '/') : normalized;
        }

        /// <summary>验证逻辑总线名称，并禁止把 Master 当作普通播放总线。</summary>
        private static string NormalizePlayableBus(string bus)
        {
            string normalized = NormalizeBus(bus, AudioBus.Sfx);
            return string.Equals(normalized, AudioBus.Master, StringComparison.OrdinalIgnoreCase)
                ? AudioBus.Sfx
                : normalized;
        }

        /// <summary>验证逻辑总线名称并应用指定空值回退。</summary>
        private static string NormalizeBus(string bus, string fallback)
        {
            if (string.IsNullOrWhiteSpace(bus))
            {
                return fallback;
            }

            return ValidateBusName(bus.Trim(), nameof(bus));
        }

        /// <summary>把有限音量限制到零到一，保留合法零值。</summary>
        private static float NormalizeUnitValue(float value, string parameterName)
        {
            EnsureFinite(value, parameterName);
            if (value < 0f) return 0f;
            return value > 1f ? 1f : value;
        }

        /// <summary>把零 pitch 解释为默认一，拒绝负数和非有限值。</summary>
        private static float NormalizePitch(float pitch)
        {
            EnsureFinite(pitch, nameof(AudioPlayOptions.Pitch));
            if (pitch == 0f) return 1f;
            if (pitch < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(AudioPlayOptions.Pitch), pitch, "Pitch cannot be negative.");
            }

            return pitch;
        }

        /// <summary>把负时长限制为零并拒绝非有限值。</summary>
        private static float NormalizeDuration(float duration, string parameterName)
        {
            EnsureFinite(duration, parameterName);
            return duration < 0f ? 0f : duration;
        }

        /// <summary>把零或负距离替换为语义默认值并拒绝非有限值。</summary>
        private static float NormalizeDistance(float distance, float fallback, string parameterName)
        {
            EnsureFinite(distance, parameterName);
            return distance <= 0f ? fallback : distance;
        }

        /// <summary>拒绝 NaN 与无穷浮点输入。</summary>
        private static void EnsureFinite(float value, string parameterName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, "Audio value must be finite.");
            }
        }

        /// <summary>判断衰减枚举是否属于公开支持集合。</summary>
        private static bool IsKnownRolloffMode(AudioRolloffMode mode) =>
            mode == AudioRolloffMode.Logarithmic || mode == AudioRolloffMode.Linear || mode == AudioRolloffMode.Custom;

        /// <summary>读取后端名称，异常时回退完整类型名以保持诊断可用。</summary>
        private static string SafeBackendName(IAudioBackend backend)
        {
            try
            {
                string name = backend.BackendName;
                return string.IsNullOrWhiteSpace(name) ? backend.GetType().FullName : name;
            }
            catch (Exception)
            {
                return backend.GetType().FullName;
            }
        }

        /// <summary>为指定后端实例创建代次安全 handle；后端已替换时返回无效句柄。</summary>
        private static AudioVoiceHandle CreateHandleForBackend(IAudioBackend backend, int voiceId)
        {
            if (voiceId <= 0 || backend == null)
            {
                return default;
            }

            lock (sLock)
            {
                if (!ReferenceEquals(sBackend, backend))
                {
                    return default;
                }

                return new AudioVoiceHandle(sBackendGeneration, voiceId);
            }
        }

        /// <summary>验证 handle 是否属于当前已创建后端代次。</summary>
        private static bool TryGetBackend(AudioVoiceHandle handle, out IAudioBackend backend)
        {
            lock (sLock)
            {
                backend = sBackend;
                return handle.IsValid && backend != null && handle.BackendGeneration == sBackendGeneration;
            }
        }

        /// <summary>向集合加入不区分大小写的唯一总线名称。</summary>
        private static void AddBusName(List<string> buses, string bus)
        {
            for (var index = 0; index < buses.Count; index++)
            {
                if (string.Equals(buses[index], bus, StringComparison.OrdinalIgnoreCase)) return;
            }

            buses.Add(bus);
        }

        /// <summary>加入 AudioKit 五个可播放默认总线。</summary>
        private static void AddDefaultBuses(List<string> buses)
        {
            AddBusName(buses, AudioBus.Music);
            AddBusName(buses, AudioBus.Sfx);
            AddBusName(buses, AudioBus.Voice);
            AddBusName(buses, AudioBus.Ambience);
            AddBusName(buses, AudioBus.UI);
        }
    }
}
