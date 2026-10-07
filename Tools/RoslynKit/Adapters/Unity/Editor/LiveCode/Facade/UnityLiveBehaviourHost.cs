#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 预编译的 Editor 帧宿主。转发生命周期、物理回调和延迟调用，不承载用户业务。
    /// 与原型外观同程序集：外观要被内存编译引用，宿主要被场景 AddComponent，两者互相持有。
    /// </summary>
    [AddComponentMenu("")]
    public sealed partial class UnityLiveBehaviourHost : MonoBehaviour
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
        private readonly List<Coroutine> mPending = new();
        private bool mAwakened;
        private bool mActive;
        private bool mStarted;
        private bool mDisposed;

        /// <summary>当前挂载的原型实例。</summary>
        public UnityLiveBehaviour Instance { get; private set; }

        /// <summary>原型 ID。</summary>
        public string Id { get; private set; }

        /// <summary>生命周期或延迟回调是否已经失败。</summary>
        public bool Faulted { get; private set; }

        /// <summary>装配一次原型，并缓存生命周期与物理回调。</summary>
        /// <param name="id">原型 ID。</param>
        /// <param name="instance">已构造的原型实例。</param>
        /// <param name="invoke">按 ID 调用其他原型的入口。</param>
        public void Configure(string id, UnityLiveBehaviour instance,
            Func<string, string, object[], object> invoke)
        {
            if (Instance != null || mDisposed) throw new InvalidOperationException("A live host can only be configured once.");
            if (instance == null) throw new ArgumentNullException(nameof(instance));
            if (string.IsNullOrEmpty(id)) throw new ArgumentException("A live ID is required.", nameof(id));
            Id = id;
            Instance = instance;
            instance.BindHost(gameObject, this, invoke);
            BindLifecycle();
            BindPhysics();
        }

        /// <summary>执行尚未跑过的 Awake，并启用帧回调。</summary>
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

        /// <summary>绑定无参生命周期回调。</summary>
        private void BindLifecycle()
        {
            mUpdate = Bind("Update");
            mFixedUpdate = Bind("FixedUpdate");
            mLateUpdate = Bind("LateUpdate");
            mEnable = Bind("OnEnable");
            mDisable = Bind("OnDisable");
            mDestroy = Bind("OnDestroy");
            mStart = Bind("Start");
            mAwake = Bind("Awake");
        }

        /// <summary>绑定 Unity 声明式物理消息。原型未声明时委托为空，签名不符则拒绝装配。</summary>
        private void BindPhysics()
        {
            mCollisionEnter = Bind<Collision>("OnCollisionEnter");
            mCollisionStay = Bind<Collision>("OnCollisionStay");
            mCollisionExit = Bind<Collision>("OnCollisionExit");
            mTriggerEnter = Bind<Collider>("OnTriggerEnter");
            mTriggerStay = Bind<Collider>("OnTriggerStay");
            mTriggerExit = Bind<Collider>("OnTriggerExit");
            mControllerHit = Bind<ControllerColliderHit>("OnControllerColliderHit");
        }

        /// <summary>绑定无参 void 回调。</summary>
        /// <param name="name">方法名。</param>
        /// <returns>可调用委托；原型未声明时返回 null。</returns>
        private Action Bind(string name)
        {
            MethodInfo method = FindMethod(name);
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
            MethodInfo method = FindMethod(name);
            if (method == null) return null;
            ParameterInfo[] parameters = method.GetParameters();
            if (method.ReturnType != typeof(void) || method.ContainsGenericParameters
                || parameters.Length != 1 || parameters[0].ParameterType != typeof(T))
                throw new NotSupportedException(name + " must be a non-generic void callback taking " + typeof(T).Name + ".");
            return (Action<T>)Delegate.CreateDelegate(typeof(Action<T>), Instance, method);
        }

        /// <summary>按实例方法名查找原型回调。</summary>
        /// <param name="name">方法名。</param>
        /// <returns>命中的方法；没有时返回 null。</returns>
        private MethodInfo FindMethod(string name)
        {
            return Instance.GetType().GetMethod(name,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
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
            var routine = StartCoroutine(RunDelay(NormalizeDelay(delaySeconds), callback));
            mPending.Add(routine);
            return routine;
        }

        /// <summary>把非法延迟收敛为 0，避免 WaitForSeconds 收到 NaN 或无穷。</summary>
        /// <param name="delaySeconds">调用方传入的秒数。</param>
        /// <returns>非负有限秒数。</returns>
        private static float NormalizeDelay(float delaySeconds)
        {
            return delaySeconds > 0f && !float.IsNaN(delaySeconds) && !float.IsInfinity(delaySeconds)
                ? delaySeconds
                : 0f;
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
        private IEnumerator RunDelay(float delaySeconds, Action callback)
        {
            if (delaySeconds > 0f) yield return new WaitForSeconds(delaySeconds);
            else yield return null;
            if (mDisposed || Faulted) yield break;
            Invoke(callback);
            mPending.RemoveAll(static routine => routine == null);
        }
    }
}
#endif
