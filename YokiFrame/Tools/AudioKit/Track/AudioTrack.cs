using System;
using System.Collections;
using UnityEngine;

namespace YokiFrame
{
    public class AudioTrack : MonoBehaviour, IAudioTrack
    {
        private AudioSource audioSource;
        private Coroutine playRoutine;
        private bool isLoop;

        public Transform Transform => transform;
        public AudioClip Clip { get; private set; }
        public bool IsPlaying { get; private set; }
        public float Volume
        {
            get => audioSource.volume;
            set => audioSource.volume = Mathf.Clamp01(value);
        }


        public event Action<IAudioTrack> OnStarted;
        public event Action<IAudioTrack> OnCompleted;
        public event Action<IAudioTrack> OnEnd;

        private void Awake() => InitSource();

        private void InitSource()
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
            }
        }

        public IAudioTrack Play(AudioClip clip, bool loop, float volume)
        {
            Clip = clip;
            audioSource.clip = clip;
            audioSource.loop = false;
            Volume = volume;

            isLoop = loop;
            AudioPlay();

            OnStarted?.Invoke(this);
            return this;
        }

        private void AudioPlay()
        {
            InitSource();
            audioSource.Play();
            IsPlaying = true;
            playRoutine = StartCoroutine(WaitForCompletion());
        }

        private IEnumerator WaitForCompletion()
        {
            while (audioSource.isPlaying)
            {
                yield return null;
            }

            CompletePlayback();
            if (isLoop)
            {
                AudioPlay();
            }
            else
            {
                EndPlayback();
            }
        }

        private void CompletePlayback()
        {
            OnCompleted?.Invoke(this);
        }

        private void EndPlayback()
        {
            IsPlaying = false;
            playRoutine = null;
            OnEnd?.Invoke(this);
        }

        public void Stop()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            audioSource.Stop();
            IsPlaying = false;
        }

        public void Pause() => audioSource.Pause();

        public void Resume() => audioSource.UnPause();

        public void End()
        {
            Stop();
            EndPlayback();
        }

        public void Dispose()
        {
            audioSource.clip = null;
            StopAllCoroutines();
            playRoutine = null;
            OnStarted = null;
            OnEnd = null;
            OnCompleted = null;
        }
    }
}