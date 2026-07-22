using System;

namespace YokiFrame
{
    public static partial class AudioKit
    {
        private const int MAX_BUS_NAME_LENGTH = 128;

        /// <summary>显式注册自定义逻辑总线，使其在首次播放前即可被工具和后端观察。</summary>
        /// <param name="bus">非空且不超过 128 字符的逻辑总线名称。</param>
        /// <returns>本次新增自定义注册时返回 true；默认或已注册总线返回 false。</returns>
        public static bool RegisterBus(string bus)
        {
            string normalized = NormalizeRequiredBus(bus);
            if (IsBuiltInBus(normalized)) return false;
            IAudioBackend backend;
            lock (sLock)
            {
                if (!sRegisteredBuses.Add(normalized)) return false;
                backend = sBackend;
            }

            if (backend != null) backend.SetBusVolume(normalized, GetEffectiveBusVolume(normalized));
#if UNITY_EDITOR || (GODOT && TOOLS)
            BumpDiagnosticVersion();
#endif
            return true;
        }

        /// <summary>移除自定义总线声明；活动 voice、音量或静音仍可让该总线保持动态可见。</summary>
        /// <param name="bus">准备移除的自定义总线名称。</param>
        /// <returns>实际移除一项显式注册时返回 true。</returns>
        public static bool UnregisterBus(string bus)
        {
            if (string.IsNullOrWhiteSpace(bus)) return false;
            string normalized = NormalizeBus(bus, AudioBus.Sfx);
            if (IsBuiltInBus(normalized)) return false;
            bool removed;
            lock (sLock) removed = sRegisteredBuses.Remove(normalized);
#if UNITY_EDITOR || (GODOT && TOOLS)
            if (removed) BumpDiagnosticVersion();
#endif
            return removed;
        }

        /// <summary>判断总线是否属于默认集合或已经由项目显式注册。</summary>
        /// <param name="bus">待查询的总线名称。</param>
        /// <returns>默认或显式注册总线返回 true。</returns>
        public static bool IsBusRegistered(string bus)
        {
            if (string.IsNullOrWhiteSpace(bus)) return false;
            string normalized = NormalizeBus(bus, AudioBus.Sfx);
            if (IsBuiltInBus(normalized)) return true;
            lock (sLock) return sRegisteredBuses.Contains(normalized);
        }

        /// <summary>判断名称是否属于 Master 或五个内置可播放总线。</summary>
        private static bool IsBuiltInBus(string bus)
        {
            return string.Equals(bus, AudioBus.Master, StringComparison.OrdinalIgnoreCase)
                || string.Equals(bus, AudioBus.Music, StringComparison.OrdinalIgnoreCase)
                || string.Equals(bus, AudioBus.Sfx, StringComparison.OrdinalIgnoreCase)
                || string.Equals(bus, AudioBus.Voice, StringComparison.OrdinalIgnoreCase)
                || string.Equals(bus, AudioBus.Ambience, StringComparison.OrdinalIgnoreCase)
                || string.Equals(bus, AudioBus.UI, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>验证显式注册名称非空并复用统一长度约束。</summary>
        private static string NormalizeRequiredBus(string bus)
        {
            if (string.IsNullOrWhiteSpace(bus))
                throw new ArgumentException("Audio bus name cannot be empty.", nameof(bus));
            return ValidateBusName(bus.Trim(), nameof(bus));
        }

        /// <summary>限制总线名称长度，避免诊断裁剪后产生不可操作的错误名称。</summary>
        private static string ValidateBusName(string bus, string parameterName)
        {
            if (bus.Length > MAX_BUS_NAME_LENGTH)
                throw new ArgumentOutOfRangeException(parameterName, bus, "Audio bus name cannot exceed 128 characters.");
            for (var index = 0; index < bus.Length; index++)
            {
                if (char.IsControl(bus[index]))
                    throw new ArgumentException("Audio bus name cannot contain control characters.", parameterName);
            }
            return bus;
        }
    }
}
