#if UNITY_EDITOR
using System;
using UnityEngine;

namespace YokiFrame
{
    public sealed partial class UnityLiveBehaviourHost
    {
        /// <summary>转发碰撞进入；原型未声明时委托为空。</summary>
        /// <param name="collision">碰撞信息。</param>
        private void OnCollisionEnter(Collision collision) { if (mActive) Invoke(mCollisionEnter, collision); }

        /// <summary>转发碰撞持续。</summary>
        /// <param name="collision">碰撞信息。</param>
        private void OnCollisionStay(Collision collision) { if (mActive) Invoke(mCollisionStay, collision); }

        /// <summary>转发碰撞离开。</summary>
        /// <param name="collision">碰撞信息。</param>
        private void OnCollisionExit(Collision collision) { if (mActive) Invoke(mCollisionExit, collision); }

        /// <summary>转发触发进入。</summary>
        /// <param name="other">进入的碰撞体。</param>
        private void OnTriggerEnter(Collider other) { if (mActive) Invoke(mTriggerEnter, other); }

        /// <summary>转发触发持续。</summary>
        /// <param name="other">停留的碰撞体。</param>
        private void OnTriggerStay(Collider other) { if (mActive) Invoke(mTriggerStay, other); }

        /// <summary>转发触发离开。</summary>
        /// <param name="other">离开的碰撞体。</param>
        private void OnTriggerExit(Collider other) { if (mActive) Invoke(mTriggerExit, other); }

        /// <summary>转发角色控制器碰撞。</summary>
        /// <param name="hit">碰撞信息。</param>
        private void OnControllerColliderHit(ControllerColliderHit hit) { if (mActive) Invoke(mControllerHit, hit); }

        /// <summary>启用帧回调并转发 OnEnable。</summary>
        private void OnEnable()
        {
            if (mDisposed || Faulted) { enabled = false; return; }
            if (Instance == null || mActive) return;
            mActive = true;
            Invoke(mEnable);
        }

        /// <summary>停用帧回调并转发 OnDisable。</summary>
        private void OnDisable()
        {
            if (!mActive) return;
            mActive = false;
            Invoke(mDisable);
        }

        /// <summary>保证 Start 只跑一次，然后转发 Update。</summary>
        private void Update()
        {
            if (!mActive || Faulted) return;
            EnsureStarted();
            if (!Faulted) Invoke(mUpdate);
        }

        /// <summary>转发 FixedUpdate。</summary>
        private void FixedUpdate()
        {
            if (!mActive || Faulted) return;
            EnsureStarted();
            if (!Faulted) Invoke(mFixedUpdate);
        }

        /// <summary>转发 LateUpdate。</summary>
        private void LateUpdate()
        {
            if (!mActive || Faulted) return;
            EnsureStarted();
            if (!Faulted) Invoke(mLateUpdate);
        }

        /// <summary>对象销毁时停止原型并清掉延迟调用。</summary>
        private void OnDestroy() { Stop(); }

        /// <summary>第一次帧回调前转发 Start。</summary>
        private void EnsureStarted()
        {
            if (mStarted) return;
            mStarted = true;
            Invoke(mStart);
        }

        /// <summary>停止帧回调、延迟调用和原型引用。重复调用无副作用。</summary>
        public void Stop()
        {
            if (mDisposed) return;
            OnDisable();
            mDisposed = true;
            StopPending();
            if (mAwakened) Invoke(mDestroy);
            DetachInstance();
            enabled = false;
        }

        /// <summary>停止尚未完成的延迟协程。</summary>
        private void StopPending()
        {
            for (var index = 0; index < mPending.Count; index++)
            {
                if (mPending[index] != null) StopCoroutine(mPending[index]);
            }

            mPending.Clear();
        }

        /// <summary>断开原型对宿主和跨原型调用入口的引用。</summary>
        private void DetachInstance()
        {
            if (Instance != null) Instance.DetachHost();

            Instance = null;
        }

        /// <summary>调用无参回调，并把异常收敛为宿主故障。</summary>
        /// <param name="callback">原型回调；为空时直接返回。</param>
        private void Invoke(Action callback)
        {
            if (callback == null) return;
            try { callback(); }
            catch (Exception exception)
            {
                MarkFaulted(exception);
            }
        }

        /// <summary>调用带单参数的原型回调，并把异常收敛为宿主故障。</summary>
        /// <typeparam name="T">回调参数类型。</typeparam>
        /// <param name="callback">原型回调；为空时直接返回。</param>
        /// <param name="argument">回调参数。</param>
        private void Invoke<T>(Action<T> callback, T argument)
        {
            if (callback == null) return;
            try { callback(argument); }
            catch (Exception exception)
            {
                MarkFaulted(exception);
            }
        }

        /// <summary>记录故障并停用帧回调，避免同一异常每帧重复抛出。</summary>
        /// <param name="exception">原型回调抛出的异常。</param>
        private void MarkFaulted(Exception exception)
        {
            Faulted = true;
            Debug.LogException(exception, this);
            enabled = false;
        }
    }
}
#endif
