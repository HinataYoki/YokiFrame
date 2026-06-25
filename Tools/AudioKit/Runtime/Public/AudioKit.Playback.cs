using System;

namespace YokiFrame
{
    /// <summary>
    /// AudioKit 播放 API。
    /// </summary>
    public static partial class AudioKit
    {
        /// <summary>
        /// 使用默认播放选项播放指定路径的音频。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play(string path)
        {
            return Play(path, AudioPlayOptions.Default);
        }

        /// <summary>
        /// 通过音频 ID 在指定通道播放音频。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        /// <param name="channel">播放通道。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play(int audioId, AudioChannel channel = AudioChannel.Sfx)
        {
            return Play(ResolvePath(audioId), new AudioPlayOptions
            {
                Bus = ToBus(channel),
                Loop = false,
                Volume = 1f,
                Pitch = 1f
            });
        }

        /// <summary>
        /// 使用旧版整数通道播放指定路径音频。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="channelId">旧版通道编号，0-4 对应内置通道，5+ 对应自定义通道。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play(string path, int channelId)
        {
            return Play(path, new AudioPlayOptions
            {
                Bus = ToBus(channelId),
                Loop = false,
                Volume = 1f,
                Pitch = 1f
            });
        }

        /// <summary>
        /// 通过音频 ID 使用旧版整数通道播放音频。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        /// <param name="channelId">旧版通道编号，0-4 对应内置通道，5+ 对应自定义通道。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play(int audioId, int channelId)
        {
            return Play(ResolvePath(audioId), channelId);
        }

        /// <summary>
        /// 通过音频 ID 使用指定选项播放音频。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        /// <param name="options">播放选项。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play(int audioId, AudioPlayOptions options)
        {
            return Play(ResolvePath(audioId), options);
        }

        /// <summary>
        /// 在 Music 总线上播放音乐。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="loop">是否循环播放。</param>
        /// <param name="volume">播放音量。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int PlayMusic(string path, bool loop = true, float volume = 1f)
        {
            return Play(path, new AudioPlayOptions
            {
                Bus = AudioBus.Music,
                Loop = loop,
                Volume = volume,
                Pitch = 1f
            });
        }

        /// <summary>
        /// 在 Sfx 总线上播放音效。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="volume">播放音量。</param>
        /// <param name="pitch">播放音高。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int PlaySfx(string path, float volume = 1f, float pitch = 1f)
        {
            return Play(path, new AudioPlayOptions
            {
                Bus = AudioBus.Sfx,
                Loop = false,
                Volume = volume,
                Pitch = pitch
            });
        }

        /// <summary>
        /// 在指定世界位置播放 3D 音频。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="position">播放位置。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play3D(string path, YokiVector3 position)
        {
            return Play3D(path, position, AudioPlayOptions.Default);
        }

        /// <summary>
        /// 使用指定选项在世界位置播放 3D 音频。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="position">播放位置。</param>
        /// <param name="options">播放选项。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play3D(string path, YokiVector3 position, AudioPlayOptions options)
        {
            options.Is3D = true;
            options.Position = position;
            options.FollowTarget = null;
            return Play(path, options);
        }

        /// <summary>
        /// 跟随指定引擎对象播放 3D 音频。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="followTarget">跟随目标。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play3D(string path, IEngineObject followTarget)
        {
            return Play3D(path, followTarget, AudioPlayOptions.Default);
        }

        /// <summary>
        /// 使用指定选项跟随引擎对象播放 3D 音频。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="followTarget">跟随目标。</param>
        /// <param name="options">播放选项。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play3D(string path, IEngineObject followTarget, AudioPlayOptions options)
        {
            if (followTarget == null)
                return Play3D(path, YokiVector3.Zero, options);

            options.Is3D = true;
            options.FollowTarget = followTarget;
            options.Position = followTarget.Position;
            return Play(path, options);
        }

        /// <summary>
        /// 通过音频 ID 在指定世界位置播放 3D 音频。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        /// <param name="position">播放位置。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play3D(int audioId, YokiVector3 position)
        {
            return Play3D(ResolvePath(audioId), position, AudioPlayOptions.Default);
        }

        /// <summary>
        /// 通过音频 ID 使用指定选项播放 3D 音频。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        /// <param name="position">播放位置。</param>
        /// <param name="options">播放选项。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play3D(int audioId, YokiVector3 position, AudioPlayOptions options)
        {
            return Play3D(ResolvePath(audioId), position, options);
        }

        /// <summary>
        /// 通过音频 ID 跟随指定引擎对象播放 3D 音频。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        /// <param name="followTarget">跟随目标。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play3D(int audioId, IEngineObject followTarget)
        {
            return Play3D(ResolvePath(audioId), followTarget, AudioPlayOptions.Default);
        }

        /// <summary>
        /// 通过音频 ID 使用指定选项跟随引擎对象播放 3D 音频。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        /// <param name="followTarget">跟随目标。</param>
        /// <param name="options">播放选项。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play3D(int audioId, IEngineObject followTarget, AudioPlayOptions options)
        {
            return Play3D(ResolvePath(audioId), followTarget, options);
        }

        /// <summary>
        /// 使用指定播放选项播放音频。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="options">播放选项。</param>
        /// <returns>播放 voice 标识；播放失败时返回 0。</returns>
        public static int Play(string path, AudioPlayOptions options)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));

            var backend = EnsureBackend();
            var normalizedOptions = NormalizeOptions(options);
            var info = backend.Play(path, normalizedOptions);
            if (info == null || info.VoiceId <= 0)
                return 0;

            Record("play_started", info);
            return info.VoiceId;
        }

        /// <summary>
        /// 异步播放指定路径的音频。
        /// </summary>
        /// <param name="path">音频资源路径。</param>
        /// <param name="options">播放选项。</param>
        /// <param name="onComplete">播放启动完成回调，参数为 voiceId。</param>
        public static void PlayAsync(string path, AudioPlayOptions options, Action<int> onComplete)
        {
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("path is empty", nameof(path));

            var backend = EnsureBackend();
            var normalizedOptions = NormalizeOptions(options);
            backend.PlayAsync(path, normalizedOptions, info =>
            {
                var voiceId = 0;
                if (info != null && info.VoiceId > 0)
                {
                    Record("play_started", info);
                    voiceId = info.VoiceId;
                }

                if (onComplete != null)
                    onComplete(voiceId);
            });
        }

        /// <summary>
        /// 通过音频 ID 异步播放音频。
        /// </summary>
        /// <param name="audioId">音频资源 ID。</param>
        /// <param name="options">播放选项。</param>
        /// <param name="onComplete">播放启动完成回调，参数为 voiceId。</param>
        public static void PlayAsync(int audioId, AudioPlayOptions options, Action<int> onComplete)
        {
            PlayAsync(ResolvePath(audioId), options, onComplete);
        }
    }
}
