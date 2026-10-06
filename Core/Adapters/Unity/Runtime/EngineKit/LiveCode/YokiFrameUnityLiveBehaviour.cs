#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>Editor-only prototype facade in a runtime assembly so Unity can attach its host.</summary>
    public abstract class YokiFrameUnityLiveBehaviour
    {
        public GameObject gameObject { get; internal set; }

        /// <summary>当前原型的预编译宿主；原型已分离时为 null，用于延迟回调等需要 MonoBehaviour 的能力。</summary>
        public YokiFrameUnityLiveBehaviourHost Host { get; internal set; }

        internal Func<string, string, object[], object> LiveInvoker { get; set; }
        public Transform transform => gameObject.transform;
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();
        public T[] GetComponents<T>() where T : Component => gameObject.GetComponents<T>();
        public void GetComponents<T>(List<T> results) where T : Component => gameObject.GetComponents(results);
        public T GetComponentInChildren<T>() where T : Component => gameObject.GetComponentInChildren<T>();
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component =>
            gameObject.GetComponentInChildren<T>(includeInactive);
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component =>
            gameObject.GetComponentsInChildren<T>(includeInactive);
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) where T : Component =>
            gameObject.GetComponentsInChildren(includeInactive, results);
        public T GetComponentInParent<T>() where T : Component => gameObject.GetComponentInParent<T>();
        public T GetComponentInParent<T>(bool includeInactive) where T : Component =>
            gameObject.GetComponentInParent<T>(includeInactive);
        public T[] GetComponentsInParent<T>(bool includeInactive = false) where T : Component =>
            gameObject.GetComponentsInParent<T>(includeInactive);
        public void GetComponentsInParent<T>(bool includeInactive, List<T> results) where T : Component =>
            gameObject.GetComponentsInParent(includeInactive, results);

        /// <summary>
        /// 延迟调用一次；延迟期间原型的生命周期回调异常与 Update 走同一套故障处理。
        /// </summary>
        /// <param name="delaySeconds">延迟秒数；非有限值按 0 处理。</param>
        /// <param name="callback">延迟结束后调用的委托。</param>
        /// <returns>可用于提前取消的句柄；原型已分离时返回 null。</returns>
        public Coroutine Delay(float delaySeconds, Action callback) =>
            Host == null ? null : Host.Delay(delaySeconds, callback);

        /// <summary>取消一个由 <see cref="Delay"/> 启动的延迟调用。</summary>
        /// <param name="routine">延迟句柄。</param>
        public void CancelDelay(Coroutine routine) => Host?.CancelDelay(routine);

        public object CallLive(string id, string method, params object[] arguments)
        {
            if (LiveInvoker == null) throw new InvalidOperationException("This prototype is no longer attached.");
            return LiveInvoker(id, method, arguments);
        }
        protected static T Instantiate<T>(T original) where T : Object => Object.Instantiate(original);
        protected static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object =>
            Object.Instantiate(original, position, rotation);
        protected static void Destroy(Object target) => Object.Destroy(target);
        protected static void Destroy(Object target, float delay) => Object.Destroy(target, delay);
    }
}
#endif
