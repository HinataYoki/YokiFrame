using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Unity 原生音频句柄实现
    /// </summary>
    internal sealed class UnityAudioHandle : IAudioHandle, IPoolable
    {
        private AudioSource mSource;
        private string mPath;
        private int mChannelId;
        private float mBaseVolume;
        private float mTargetVolume;
        private float mFadeSpeed;
        private bool mIsFadingIn;
        private bool mIsFadingOut;
        private Transform mFollowTarget;
        private bool mIsStopped; // 标记是否已手动停止
        private bool mManualLifecycle; // 手动生命周期管理

        public bool IsValid => mSource != null;
        public bool IsManualLifecycle => mManualLifecycle;
        public string Path => mPath;
        public bool IsRecycled { get; set; }
        public int ChannelId => mChannelId;
        public AudioChannel Channel => mChannelId < 5 ? (AudioChannel)mChannelId : AudioChannel.Sfx;

        public bool IsPlaying => IsValid && mSource.isPlaying && !mIsFadingOut;

        public bool IsPaused { get; private set; }

        public float Volume
        {
            get => mBaseVolume;
            set
            {
                if (!IsValid) return;

                mBaseVolume = Mathf.Clamp01(value);
                if (!mIsFadingIn && !mIsFadingOut)
                {
                    mSource.volume = mBaseVolume;
                }
            }
        }

        public float Pitch
        {
            get => IsValid ? mSource.pitch : 1f;
            set
            {
                if (!IsValid) return;
                mSource.pitch = Mathf.Clamp(value, 0.01f, 3f);
            }
        }

        public float Time
        {
            get => IsValid ? mSource.time : 0f;
            set
            {
                if (!IsValid) return;
                mSource.time = Mathf.Clamp(value, 0f, Duration);
            }
        }

        public float Duration => IsValid && mSource.clip != null ? mSource.clip.length : 0f;

        internal AudioSource Source => mSource;
        internal Transform FollowTarget => mFollowTarget;
        internal bool IsFading => mIsFadingIn || mIsFadingOut;

        /// <summary>
        /// 初始化句柄
        /// </summary>
        internal void Initialize(string path, AudioSource source, int channelId, float baseVolume, bool manualLifecycle = false, Transform followTarget = null)
        {
            mPath = path;
            mSource = source;
            mChannelId = channelId;
            mBaseVolume = baseVolume;
            mTargetVolume = baseVolume;
            mFollowTarget = followTarget;
            mManualLifecycle = manualLifecycle;
            mFadeSpeed = 0f;
            mIsFadingIn = false;
            mIsFadingOut = false;
            mIsStopped = false;
            IsPaused = false;
        }

        /// <summary>
        /// 设置淡入
        /// </summary>
        internal void SetFadeIn(float duration, float targetVolume)
        {
            if (duration <= 0f)
            {
                mSource.volume = targetVolume;
                mTargetVolume = targetVolume;
                mBaseVolume = targetVolume;
                return;
            }

            mSource.volume = 0f;
            mTargetVolume = targetVolume;
            mBaseVolume = targetVolume;
            mFadeSpeed = targetVolume / duration;
            mIsFadingIn = true;
            mIsFadingOut = false;
        }

        /// <summary>
        /// 更新淡入淡出和跟随
        /// </summary>
        /// <returns>true 表示播放完成，需要回收</returns>
        internal bool UpdateFade(float deltaTime)
        {
            if (mSource == null || mIsStopped) return true;

            // 更新跟随目标位置
            if (mFollowTarget != null)
            {
                mSource.transform.position = mFollowTarget.position;
            }

            // 淡入处理
            if (mIsFadingIn)
            {
                mSource.volume += mFadeSpeed * deltaTime;
                if (mSource.volume >= mTargetVolume)
                {
                    mSource.volume = mTargetVolume;
                    mIsFadingIn = false;
                }
            }
            // 淡出处理
            else if (mIsFadingOut)
            {
                mSource.volume -= mFadeSpeed * deltaTime;
                if (mSource.volume <= 0f)
                {
                    mSource.volume = 0f;
                    mSource.Stop();
                    mIsFadingOut = false;
                    mIsStopped = true;
                    
                    // 手动生命周期模式下不自动回收
                    if (mManualLifecycle)
                    {
                        return false;
                    }
                    return true;
                }
            }

            // 检查播放完成（非循环）
            if (!mSource.isPlaying && !IsPaused && !mSource.loop)
            {
                mIsStopped = true;
                
                // 手动生命周期模式下不自动回收
                if (mManualLifecycle)
                {
                    return false;
                }
                return true;
            }

            return false;
        }

        public void Pause()
        {
            if (!IsValid)
            {
                KitLogger.Warning("[AudioKit] 尝试暂停已失效的音频句柄");
                return;
            }

            if (mSource.isPlaying)
            {
                mSource.Pause();
                IsPaused = true;
            }
        }

        public void Resume()
        {
            if (!IsValid)
            {
                KitLogger.Warning("[AudioKit] 尝试恢复已失效的音频句柄");
                return;
            }

            if (IsPaused)
            {
                mSource.UnPause();
                IsPaused = false;
            }
        }

        public void Stop()
        {
            if (!IsValid)
            {
                KitLogger.Warning("[AudioKit] 尝试停止已失效的音频句柄");
                return;
            }

            mSource.Stop();
            mIsFadingIn = false;
            mIsFadingOut = false;
            mIsStopped = true;
            IsPaused = false;
        }

        public void StopWithFade(float fadeDuration)
        {
            if (!IsValid)
            {
                KitLogger.Warning("[AudioKit] 尝试淡出已失效的音频句柄");
                return;
            }

            if (!mSource.isPlaying) return;

            if (fadeDuration <= 0f)
            {
                Stop();
                return;
            }

            mIsFadingOut = true;
            mIsFadingIn = false;
            mTargetVolume = 0f;
            mFadeSpeed = mSource.volume / fadeDuration;
        }

        public void SetPosition(Vector3 position)
        {
            if (!IsValid)
            {
                KitLogger.Warning("[AudioKit] 尝试设置已失效音频句柄的位置");
                return;
            }

            mSource.transform.position = position;
        }

        public void SetManualLifecycle(bool manual)
        {
            mManualLifecycle = manual;
        }

        public void Release()
        {
            if (!IsValid)
            {
                KitLogger.Warning("[AudioKit] 尝试释放已失效的音频句柄");
                return;
            }

            if (!mManualLifecycle)
            {
                KitLogger.Warning("[AudioKit] 仅手动生命周期模式下可调用 Release()");
                return;
            }

            // 停止播放并标记为已停止，让 Backend 在下一帧回收
            Stop();
            mManualLifecycle = false; // 解除手动模式，允许回收
        }

        /// <summary>
        /// 更新有效音量（通道音量 * 全局音量）
        /// </summary>
        internal void UpdateEffectiveVolume(float channelVolume, float globalVolume)
        {
            if (!IsValid) return;

            var effectiveVolume = mBaseVolume * channelVolume * globalVolume;
            
            if (mIsFadingIn)
            {
                // 淡入时，目标音量需要考虑通道和全局音量
                mTargetVolume = mBaseVolume * channelVolume * globalVolume;
            }
            else if (!mIsFadingOut)
            {
                mSource.volume = effectiveVolume;
            }
        }

        public void OnRecycled()
        {
            mSource = null;
            mPath = null;
            mChannelId = (int)AudioChannel.Sfx;
            mBaseVolume = 1f;
            mTargetVolume = 0f;
            mFadeSpeed = 0f;
            mIsFadingIn = false;
            mIsFadingOut = false;
            mIsStopped = false;
            mManualLifecycle = false;
            mFollowTarget = null;
            IsPaused = false;
        }
    }
}
