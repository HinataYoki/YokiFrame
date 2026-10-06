#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>Editor-only frame host; never serialized into a scene or included in a player.</summary>
    [AddComponentMenu("")]
    public sealed class YokiFrameUnityLiveBehaviourHost : MonoBehaviour
    {
        private Action mUpdate;
        private Action mFixedUpdate;
        private Action mLateUpdate;
        private Action mEnable;
        private Action mDisable;
        private Action mDestroy;
        private Action mStart;
        private Action mAwake;
        private Action<Collision> mCollisionEnter;
        private Action<Collision> mCollisionStay;
        private Action<Collision> mCollisionExit;
        private Action<Collider> mTriggerEnter;
        private Action<Collider> mTriggerStay;
        private Action<Collider> mTriggerExit;
        private Action<ControllerColliderHit> mControllerHit;
        private readonly System.Collections.Generic.List<Coroutine> mPending =
            new System.Collections.Generic.List<Coroutine>();
        private bool mAwakened;
        private bool mActive;
        private bool mStarted;
        private bool mDisposed;
        public YokiFrameUnityLiveBehaviour Instance { get; private set; }
        public string Id { get; private set; }
        public bool Faulted { get; private set; }

        public void Configure(string id, YokiFrameUnityLiveBehaviour instance,
            Func<string, string, object[], object> invoke)
        {
            if (Instance != null || mDisposed) throw new InvalidOperationException("A live host can only be configured once.");
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A live ID is required.", nameof(id));
            Id = id;
            Instance = instance;
            instance.gameObject = gameObject;
            instance.Host = this;
            instance.LiveInvoker = invoke;
            mUpdate = Bind("Update");
            mFixedUpdate = Bind("FixedUpdate");
            mLateUpdate = Bind("LateUpdate");
            mEnable = Bind("OnEnable");
            mDisable = Bind("OnDisable");
            mDestroy = Bind("OnDestroy");
            mStart = Bind("Start");
            mAwake = Bind("Awake");
            // 物理消息是声明式的：宿主必须在编译期实现它们，Unity 才会回调；原型未声明时委托为空，直接返回。
            mCollisionEnter = Bind<Collision>("OnCollisionEnter");
            mCollisionStay = Bind<Collision>("OnCollisionStay");
            mCollisionExit = Bind<Collision>("OnCollisionExit");
            mTriggerEnter = Bind<Collider>("OnTriggerEnter");
            mTriggerStay = Bind<Collider>("OnTriggerStay");
            mTriggerExit = Bind<Collider>("OnTriggerExit");
            mControllerHit = Bind<ControllerColliderHit>("OnControllerColliderHit");
        }

        public void Activate()
        {
            if (mDisposed || Faulted || Instance == null)
                throw new InvalidOperationException("The behaviour is unavailable; reattach its ID to recover a fault.");
            if (!mAwakened)
            {
                mAwakened = true;
                Invoke(mAwake);
            }
            if (!Faulted) enabled = true;
            if (Faulted) throw new InvalidOperationException("The behaviour failed during activation.");
        }

        private Action Bind(string name)
        {
            MethodInfo method = Instance.GetType().GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) return null;
            if (method.ReturnType != typeof(void) || method.GetParameters().Length != 0
                || method.ContainsGenericParameters)
                throw new NotSupportedException(name + " must be a non-generic void callback with no parameters.");
            return (Action)Delegate.CreateDelegate(typeof(Action), Instance, method);
        }

        /// <summary>
        /// 绑定带单个参数的原型回调；原型未声明时返回 null，签名不符时在装配阶段直接拒绝。
        /// </summary>
        /// <typeparam name="T">回调参数类型。</typeparam>
        /// <param name="name">回调方法名。</param>
        /// <returns>可调用的委托；原型未声明时返回 null。</returns>
        private Action<T> Bind<T>(string name)
        {
            MethodInfo method = Instance.GetType().GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (method == null) return null;
            ParameterInfo[] parameters = method.GetParameters();
            if (method.ReturnType != typeof(void) || method.ContainsGenericParameters
                || parameters.Length != 1 || parameters[0].ParameterType != typeof(T))
                throw new NotSupportedException(name + " must be a non-generic void callback taking " + typeof(T).Name + ".");
            return (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), Instance, method);
        }

        /// <summary>
        /// 启动原型请求的延迟回调；宿主销毁时统一停止，超时后回调异常按故障处理。
        /// </summary>
        /// <param name="delaySeconds">延迟秒数，非有限值时按 0 处理。</param>
        /// <param name="callback">延迟结束后调用的委托。</param>
        /// <returns>可用于提前停止的协程句柄；宿主已销毁时返回 null。</returns>
        public Coroutine Delay(float delaySeconds, Action callback)
        {
            if (mDisposed || callback == null) return null;
            float wait = delaySeconds > 0f && !float.IsNaN(delaySeconds) && !float.IsInfinity(delaySeconds)
                ? delaySeconds
                : 0f;
            var routine = StartCoroutine(RunDelay(wait, callback));
            mPending.Add(routine);
            return routine;
        }

        /// <summary>停止一个由 <see cref="Delay"/> 启动的延迟回调；句柄为空或不属于本宿主时无副作用。</summary>
        /// <param name="routine">延迟句柄。</param>
        public void CancelDelay(Coroutine routine)
        {
            if (routine == null || !mPending.Remove(routine)) return;
            StopCoroutine(routine);
        }

        /// <summary>等待指定秒数后调用回调，并把回调异常收敛为宿主故障。</summary>
        /// <param name="delaySeconds">延迟秒数。</param>
        /// <param name="callback">延迟结束后调用的委托。</param>
        /// <returns>协程迭代器。</returns>
        private System.Collections.IEnumerator RunDelay(float delaySeconds, Action callback)
        {
            if (delaySeconds > 0f) yield return new WaitForSeconds(delaySeconds);
            else yield return null;
            if (mDisposed || Faulted) yield break;
            try { callback(); }
            catch (Exception exception)
            {
                Faulted = true;
                Debug.LogException(exception, this);
                enabled = false;
            }

            mPending.RemoveAll(routine => routine == null);
        }

        private void OnCollisionEnter(Collision collision) { if (mActive) Invoke(mCollisionEnter, collision); }
        private void OnCollisionStay(Collision collision) { if (mActive) Invoke(mCollisionStay, collision); }
        private void OnCollisionExit(Collision collision) { if (mActive) Invoke(mCollisionExit, collision); }
        private void OnTriggerEnter(Collider other) { if (mActive) Invoke(mTriggerEnter, other); }
        private void OnTriggerStay(Collider other) { if (mActive) Invoke(mTriggerStay, other); }
        private void OnTriggerExit(Collider other) { if (mActive) Invoke(mTriggerExit, other); }
        private void OnControllerColliderHit(ControllerColliderHit hit) { if (mActive) Invoke(mControllerHit, hit); }

        private void OnEnable()
        {
            if (mDisposed || Faulted) { enabled = false; return; }
            if (Instance == null || mActive) return;
            mActive = true;
            Invoke(mEnable);
        }

        private void OnDisable()
        {
            if (!mActive) return;
            mActive = false;
            Invoke(mDisable);
        }

        private void Update()
        {
            if (!mActive || Faulted) return;
            EnsureStarted();
            if (!Faulted) Invoke(mUpdate);
        }

        private void FixedUpdate() { if (mActive && !Faulted) { EnsureStarted(); if (!Faulted) Invoke(mFixedUpdate); } }
        private void LateUpdate() { if (mActive && !Faulted) { EnsureStarted(); if (!Faulted) Invoke(mLateUpdate); } }
        private void OnDestroy() { Stop(); }

        private void EnsureStarted()
        {
            if (mStarted) return;
            mStarted = true;
            Invoke(mStart);
        }

        public void Stop()
        {
            if (mDisposed) return;
            OnDisable();
            mDisposed = true;
            for (var index = 0; index < mPending.Count; index++)
            {
                if (mPending[index] != null) StopCoroutine(mPending[index]);
            }

            mPending.Clear();
            if (mAwakened) Invoke(mDestroy);
            if (Instance != null)
            {
                Instance.LiveInvoker = null;
                Instance.Host = null;
            }

            Instance = null;
            enabled = false;
        }

        private void Invoke(Action callback)
        {
            if (callback == null) return;
            try { callback(); }
            catch (Exception exception)
            {
                Faulted = true;
                Debug.LogException(exception, this);
                enabled = false;
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
                Faulted = true;
                Debug.LogException(exception, this);
                enabled = false;
            }
        }
    }
}
#endif
