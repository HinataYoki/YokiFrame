#if YOKIFRAME_FMOD_SUPPORT
using UnityEngine;
using FMOD.Studio;
using FMODUnity;

namespace YokiFrame
{
    /// <summary>
    /// FMOD 音频句柄实现 - 封装 FMOD.Studio.EventInstance
    /// </summary>
    internal sealed class FmodAudioHandle : IAudioHandle, IPoolable
    {
        private EventInstance mInstance;
        private EventDescription mDescription;
        private string mPath;
        private int mChannelId;
        private float mBaseVolume;
        private float mTargetVolume;
        private float mFadeSpeed;
        private bool mIsFadingIn;
        private bool mIsFadingOut;
        private Transform mFollowTarget;
        private int mDurationMs;

        public string Path => mPath;
        public bool IsRecycled { get; set; }
        public int ChannelId => mChannelId;
        public AudioChannel Channel => mChannelId < 5 ? (AudioChannel)mChannelId : AudioChannel.Sfx;

        public bool IsPlaying
        {
            get
            {
                if (!mInstance.isValid()) return false;
                mInstance.getPlaybackState(out var state);
                return state == PLAYBACK_STATE.PLAYING || state == PLAYBACK_STATE.STARTING;
            }
        }

        public bool IsPaused { get; private set; }

        public float Volume
        {
            get => mBaseVolume;
            set
            {
                mBaseVolume = Mathf.Clamp01(value);
                if (mInstance.isValid() && !mIsFadingIn && !mIsFadingOut)
                {
                    mInstance.setVolume(mBaseVolume);
                }
            }
        }

        public float Pitch
        {
            get
            {
                if (!mInstance.isValid()) return 1f;
                mInstance.getPitch(out var pitch);
                return pitch;
            }
            set
            {
                if (mInstance.isValid())
                {
                    mInstance.setPitch(Mathf.Clamp(value, 0.01f, 3f));
                }
            }
        }

        public float Time
        {
            get
            {
                if (!mInstance.isValid()) return 0f;
                mInstance.getTimelinePosition(out var positionMs);
                return positionMs / 1000f;
            }
            set
            {
                if (mInstance.isValid())
                {
                    var positionMs = Mathf.Clamp((int)(value * 1000f), 0, mDurationMs);
                    mInstance.setTimelinePosition(positionMs);
                }
            }
        }

        public float Duration => mDurationMs / 1000f;

        internal EventInstance Instance => mInstance;
        internal Transform FollowTarget => mFollowTarget;
        internal bool IsFading => mIsFadingIn || mIsFadingOut;

        /// <summary>
        /// 初始化句柄
        /// </summary>
        internal void Initialize(string path, EventInstance instance, EventDescription description, 
            int channelId, float baseVolume, Transform followTarget = null)
        {
            mPath = path;
            mInstance = instance;
            mDescription = description;
            mChannelId = channelId;
            mBaseVolume = baseVolume;
            mTargetVolume = baseVolume;
            mFollowTarget = followTarget;
            mFadeSpeed = 0f;
            mIsFadingIn = false;
            mIsFadingOut = false;
            IsPaused = false;

            // 获取事件时长
            if (description.isValid())
            {
                description.getLength(out mDurationMs);
            }
            else
            {
                mDurationMs = 0;
            }
        }

        /// <summary>
        /// 设置淡入
        /// </summary>
        internal void SetFadeIn(float duration, float targetVolume)
        {
            if (duration <= 0f)
            {
                mInstance.setVolume(targetVolume);
                mTargetVolume = targetVolume;
                mBaseVolume = targetVolume;
                return;
            }

            mInstance.setVolume(0f);
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
            if (!mInstance.isValid()) return true;

            // 更新跟随目标位置
            if (mFollowTarget != null)
            {
                var attributes = RuntimeUtils.To3DAttributes(mFollowTarget);
                mInstance.set3DAttributes(attributes);
            }

            // 淡入处理
            if (mIsFadingIn)
            {
                mInstance.getVolume(out var currentVolume);
                var newVolume = currentVolume + mFadeSpeed * deltaTime;
                if (newVolume >= mTargetVolume)
                {
                    mInstance.setVolume(mTargetVolume);
                    mIsFadingIn = false;
                }
                else
                {
                    mInstance.setVolume(newVolume);
                }
            }
            // 淡出处理
            else if (mIsFadingOut)
            {
                mInstance.getVolume(out var currentVolume);
                var newVolume = currentVolume - mFadeSpeed * deltaTime;
                if (newVolume <= 0f)
                {
                    mInstance.setVolume(0f);
                    mInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                    mIsFadingOut = false;
                    return true;
                }
                else
                {
                    mInstance.setVolume(newVolume);
                }
            }

            // 检查播放完成
            mInstance.getPlaybackState(out var state);
            if (state == PLAYBACK_STATE.STOPPED && !IsPaused)
            {
                return true;
            }

            return false;
        }

        public void Pause()
        {
            if (mInstance.isValid() && IsPlaying)
            {
                mInstance.setPaused(true);
                IsPaused = true;
            }
        }

        public void Resume()
        {
            if (mInstance.isValid() && IsPaused)
            {
                mInstance.setPaused(false);
                IsPaused = false;
            }
        }

        public void Stop()
        {
            if (mInstance.isValid())
            {
                mInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
                mIsFadingIn = false;
                mIsFadingOut = false;
                IsPaused = false;
            }
        }

        public void StopWithFade(float fadeDuration)
        {
            if (!mInstance.isValid() || !IsPlaying) return;

            if (fadeDuration <= 0f)
            {
                Stop();
                return;
            }

            // FMOD 原生支持淡出，使用 ALLOWFADEOUT 模式
            // 但为了与 Unity 后端行为一致，我们手动控制淡出
            mIsFadingOut = true;
            mIsFadingIn = false;
            mTargetVolume = 0f;
            mInstance.getVolume(out var currentVolume);
            mFadeSpeed = currentVolume / fadeDuration;
        }

        public void SetPosition(Vector3 position)
        {
            if (mInstance.isValid())
            {
                var attributes = RuntimeUtils.To3DAttributes(position);
                mInstance.set3DAttributes(attributes);
            }
        }

        /// <summary>
        /// 更新有效音量（通道音量 * 全局音量）
        /// </summary>
        internal void UpdateEffectiveVolume(float channelVolume, float globalVolume)
        {
            if (!mInstance.isValid()) return;

            var effectiveVolume = mBaseVolume * channelVolume * globalVolume;

            if (mIsFadingIn)
            {
                mTargetVolume = mBaseVolume * channelVolume * globalVolume;
            }
            else if (!mIsFadingOut)
            {
                mInstance.setVolume(effectiveVolume);
            }
        }

        /// <summary>
        /// 释放 FMOD 实例
        /// </summary>
        internal void ReleaseInstance()
        {
            if (mInstance.isValid())
            {
                mInstance.release();
            }
        }

        public void OnRecycled()
        {
            // 释放 FMOD 实例
            ReleaseInstance();

            mInstance.clearHandle();
            mDescription.clearHandle();
            mPath = null;
            mChannelId = (int)AudioChannel.Sfx;
            mBaseVolume = 1f;
            mTargetVolume = 0f;
            mFadeSpeed = 0f;
            mIsFadingIn = false;
            mIsFadingOut = false;
            mFollowTarget = null;
            mDurationMs = 0;
            IsPaused = false;
        }
    }
}
#endif
