using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Unity 原生音频句柄实现
    /// </summary>
    internal sealed class UnityAudioHandle : IAudioHandle, IPoolable
    {
        private AudioSource mSource;
        private int mId;
        private AudioChannel mChannel;
        private float mBaseVolume;
        private float mTargetVolume;
        private float mFadeSpeed;
        private bool mIsFadingIn;
        private bool mIsFadingOut;
        private Transform mFollowTarget;

        public int Id => mId;
        public bool IsRecycled { get; set; }
        public AudioChannel Channel => mChannel;

        public bool IsPlaying => mSource != null && mSource.isPlaying && !mIsFadingOut;

        public bool IsPaused { get; private set; }

        public float Volume
        {
            get => mBaseVolume;
            set
            {
                mBaseVolume = Mathf.Clamp01(value);
                if (mSource != null && !mIsFadingIn && !mIsFadingOut)
                {
                    mSource.volume = mBaseVolume;
                }
            }
        }

        public float Pitch
        {
            get => mSource != null ? mSource.pitch : 1f;
            set
            {
                if (mSource != null)
                {
                    mSource.pitch = Mathf.Clamp(value, 0.01f, 3f);
                }
            }
        }

        public float Time
        {
            get => mSource != null ? mSource.time : 0f;
            set
            {
                if (mSource != null)
                {
                    mSource.time = Mathf.Clamp(value, 0f, Duration);
                }
            }
        }

        public float Duration => mSource != null && mSource.clip != null ? mSource.clip.length : 0f;

        internal AudioSource Source => mSource;
        internal Transform FollowTarget => mFollowTarget;
        internal bool IsFading => mIsFadingIn || mIsFadingOut;

        /// <summary>
        /// 初始化句柄
        /// </summary>
        internal void Initialize(int id, AudioSource source, AudioChannel channel, float baseVolume, Transform followTarget = null)
        {
            mId = id;
            mSource = source;
            mChannel = channel;
            mBaseVolume = baseVolume;
            mTargetVolume = baseVolume;
            mFollowTarget = followTarget;
            mFadeSpeed = 0f;
            mIsFadingIn = false;
            mIsFadingOut = false;
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
            if (mSource == null) return true;

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
                    return true;
                }
            }

            // 检查播放完成（非循环）
            if (!mSource.isPlaying && !IsPaused && !mSource.loop)
            {
                return true;
            }

            return false;
        }

        public void Pause()
        {
            if (mSource != null && mSource.isPlaying)
            {
                mSource.Pause();
                IsPaused = true;
            }
        }

        public void Resume()
        {
            if (mSource != null && IsPaused)
            {
                mSource.UnPause();
                IsPaused = false;
            }
        }

        public void Stop()
        {
            if (mSource != null)
            {
                mSource.Stop();
                mIsFadingIn = false;
                mIsFadingOut = false;
                IsPaused = false;
            }
        }

        public void StopWithFade(float fadeDuration)
        {
            if (mSource == null || !mSource.isPlaying) return;

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
            if (mSource != null)
            {
                mSource.transform.position = position;
            }
        }

        /// <summary>
        /// 更新有效音量（通道音量 * 全局音量）
        /// </summary>
        internal void UpdateEffectiveVolume(float channelVolume, float globalVolume)
        {
            if (mSource == null) return;

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
            mId = 0;
            mChannel = AudioChannel.Sfx;
            mBaseVolume = 1f;
            mTargetVolume = 0f;
            mFadeSpeed = 0f;
            mIsFadingIn = false;
            mIsFadingOut = false;
            mFollowTarget = null;
            IsPaused = false;
        }
    }
}
