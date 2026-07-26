#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace YokiFrame.Unity
{
    public sealed partial class UnityAudioKitBackend
    {
        /// <summary>从 PoolKit 取得 AudioSource 租约，宿主已销毁时重建后再启用。</summary>
        private PooledAudioSource RentSource()
        {
            EnsureRoot();
            PooledAudioSource lease = mSourcePool.Allocate();
            if (lease.Source == null) lease.Source = CreateSource();
            lease.Source.gameObject.SetActive(true);
            return lease;
        }

        /// <summary>创建一个挂在 AudioKit 根对象下的 AudioSource 宿主。</summary>
        private AudioSource CreateSource()
        {
            GameObject voiceObject = new("AudioKitVoice");
            voiceObject.transform.SetParent(mRoot.transform, false);
            AudioSource created = voiceObject.AddComponent<AudioSource>();
            created.playOnAwake = false;
            return created;
        }

        /// <summary>创建一个可池化 AudioSource 租约；只由 PoolKit 空缓存工厂调用。</summary>
        private PooledAudioSource CreateSourceLease() => new(CreateSource());

        /// <summary>重置池化 AudioSource，供 PoolKit 回收回调统一执行。</summary>
        private static void ResetSourceLease(PooledAudioSource lease)
        {
            if (lease == null || lease.Source == null) return;
            AudioSource source = lease.Source;
            source.Stop();
            source.clip = null;
            source.loop = false;
            source.pitch = 1f;
            source.volume = 1f;
            source.spatialBlend = 0f;
            source.minDistance = 1f;
            source.maxDistance = 500f;
            source.rolloffMode = UnityEngine.AudioRolloffMode.Logarithmic;
            source.transform.localPosition = UnityEngine.Vector3.zero;
            source.gameObject.SetActive(false);
        }

        /// <summary>归还 voice 的 AudioSource 租约；容量淘汰与销毁统一由 Core PoolKit 负责。</summary>
        private void ReturnSource(PooledAudioSource lease)
        {
            if (lease == null) return;
            mSourcePool.Recycle(lease);
        }

        /// <summary>移除指定索引 voice 并回收其 AudioSource。</summary>
        private void ReleaseVoiceAt(int index)
        {
            VoiceState voice = mVoices[index];
            mVoices.RemoveAt(index);
            ReturnSource(voice.SourceLease);
#if UNITY_EDITOR
            AudioKit.NotifyBackendDiagnosticStateChanged();
#endif
        }

        /// <summary>更新存活跟随目标位置，并把跨宿主坐标映射到 Unity Transform。</summary>
        private static void UpdateFollowTarget(VoiceState voice)
        {
            if (voice.FollowTarget != null && voice.FollowTarget.IsAlive)
            {
                voice.Position = voice.FollowTarget.Position;
            }

            if (!voice.Is3D || voice.Source == null) return;
            System.Numerics.Vector3 position = voice.Position;
            voice.Source.transform.position = new UnityEngine.Vector3(position.X, position.Y, position.Z);
        }

        /// <summary>确保承载池化 AudioSource 的常驻根对象存在。</summary>
        private void EnsureRoot()
        {
            if (mRoot != null) return;
            mRoot = new GameObject("YokiFrameAudioKit");
            if (Application.isPlaying) UnityEngine.Object.DontDestroyOnLoad(mRoot);
        }

        /// <summary>验证当前调用运行在创建后端的 Unity 主线程。</summary>
        private void EnsureUnityThread()
        {
            if (Thread.CurrentThread.ManagedThreadId != mUnityThreadId)
            {
                throw new InvalidOperationException("AudioKit Unity backend must create and control AudioSource on the Unity thread.");
            }
        }

        /// <summary>在已捕获 Unity SynchronizationContext 上执行宿主操作。</summary>
        private Task<T> InvokeOnUnityThreadAsync<T>(Func<T> operation, CancellationToken token)
        {
            if (Thread.CurrentThread.ManagedThreadId == mUnityThreadId)
            {
                token.ThrowIfCancellationRequested();
                return Task.FromResult(operation());
            }

            if (mUnityContext == null)
            {
                return Task.FromException<T>(new InvalidOperationException("Unity SynchronizationContext is unavailable."));
            }

            TaskCompletionSource<T> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            mUnityContext.Post(_ => CompleteUnityOperation(operation, token, completion), null);
            return completion.Task;
        }

        /// <summary>执行一次已回到 Unity 主线程的操作并提交终态。</summary>
        private static void CompleteUnityOperation<T>(
            Func<T> operation,
            CancellationToken token,
            TaskCompletionSource<T> completion)
        {
            try
            {
                token.ThrowIfCancellationRequested();
                completion.TrySetResult(operation());
            }
            catch (OperationCanceledException)
            {
                completion.TrySetCanceled(token);
            }
            catch (Exception exception)
            {
                completion.TrySetException(exception);
            }
        }

        /// <summary>按运行态选择延迟销毁或立即销毁。</summary>
        private static void DestroyObject(UnityEngine.Object target)
        {
            if (target == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
#endif
