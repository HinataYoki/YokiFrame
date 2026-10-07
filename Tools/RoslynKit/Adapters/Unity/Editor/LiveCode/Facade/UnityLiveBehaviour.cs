#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// Play 原型外观。用户成员编译后继承它，由同名宿主转发生命周期。
    /// 单独放在 Editor-only 程序集：内存编译需要引用它，Player 又不能带上原型类型。
    /// </summary>
    public abstract class UnityLiveBehaviour
    {
        /// <summary>当前原型挂载的场景对象。</summary>
        public GameObject gameObject { get; private set; }

        /// <summary>当前原型的预编译宿主；原型已分离时为 null，用于延迟回调等需要 MonoBehaviour 的能力。</summary>
        public UnityLiveBehaviourHost Host { get; private set; }

        /// <summary>按 ID 调用其他原型的入口；分离后清空。</summary>
        internal Func<string, string, object[], object> LiveInvoker { get; private set; }

        /// <summary>由同程序集宿主绑定场景对象、宿主和跨原型调用入口。</summary>
        /// <param name="owner">挂载对象。</param>
        /// <param name="host">帧宿主。</param>
        /// <param name="invoke">按 ID 调用其他原型的入口。</param>
        internal void BindHost(GameObject owner, UnityLiveBehaviourHost host,
            Func<string, string, object[], object> invoke)
        {
            gameObject = owner;
            Host = host;
            LiveInvoker = invoke;
        }

        /// <summary>断开宿主和跨原型调用入口，保留场景对象引用供停止回调收尾。</summary>
        internal void DetachHost()
        {
            Host = null;
            LiveInvoker = null;
        }

        /// <summary>当前场景对象的 Transform。</summary>
        public Transform transform => gameObject.transform;

        /// <summary>读取当前对象上的组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <returns>命中的组件；没有时返回 null。</returns>
        public T GetComponent<T>() where T : Component => gameObject.GetComponent<T>();

        /// <summary>读取当前对象上该类型的全部组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <returns>组件数组。</returns>
        public T[] GetComponents<T>() where T : Component => gameObject.GetComponents<T>();

        /// <summary>把当前对象上该类型的组件写入调用方列表。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="results">接收结果的列表；由调用方提供以复用存储。</param>
        public void GetComponents<T>(List<T> results) where T : Component => gameObject.GetComponents(results);

        /// <summary>读取子层级中的组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <returns>命中的组件。</returns>
        public T GetComponentInChildren<T>() where T : Component => gameObject.GetComponentInChildren<T>();

        /// <summary>读取子层级中的组件，可包含未激活对象。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="includeInactive">是否包含未激活对象。</param>
        /// <returns>命中的组件。</returns>
        public T GetComponentInChildren<T>(bool includeInactive) where T : Component =>
            gameObject.GetComponentInChildren<T>(includeInactive);

        /// <summary>读取子层级中该类型的全部组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="includeInactive">是否包含未激活对象。</param>
        /// <returns>组件数组。</returns>
        public T[] GetComponentsInChildren<T>(bool includeInactive = false) where T : Component =>
            gameObject.GetComponentsInChildren<T>(includeInactive);

        /// <summary>把子层级中的组件写入调用方列表。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="includeInactive">是否包含未激活对象。</param>
        /// <param name="results">接收结果的列表。</param>
        public void GetComponentsInChildren<T>(bool includeInactive, List<T> results) where T : Component =>
            gameObject.GetComponentsInChildren(includeInactive, results);

        /// <summary>读取父层级中的组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <returns>命中的组件。</returns>
        public T GetComponentInParent<T>() where T : Component => gameObject.GetComponentInParent<T>();

        /// <summary>读取父层级中的组件，可包含未激活对象。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="includeInactive">是否包含未激活对象。</param>
        /// <returns>命中的组件。</returns>
        public T GetComponentInParent<T>(bool includeInactive) where T : Component =>
            gameObject.GetComponentInParent<T>(includeInactive);

        /// <summary>读取父层级中该类型的全部组件。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="includeInactive">是否包含未激活对象。</param>
        /// <returns>组件数组。</returns>
        public T[] GetComponentsInParent<T>(bool includeInactive = false) where T : Component =>
            gameObject.GetComponentsInParent<T>(includeInactive);

        /// <summary>把父层级中的组件写入调用方列表。</summary>
        /// <typeparam name="T">组件类型。</typeparam>
        /// <param name="includeInactive">是否包含未激活对象。</param>
        /// <param name="results">接收结果的列表。</param>
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
        public void CancelDelay(Coroutine routine)
        {
            if (Host != null) Host.CancelDelay(routine);
        }

        /// <summary>按 ID 调用另一个仍挂载的原型方法。</summary>
        /// <param name="id">目标原型 ID。</param>
        /// <param name="method">目标 public 实例方法名。</param>
        /// <param name="arguments">按声明顺序传入的参数。</param>
        /// <returns>目标方法返回值。</returns>
        public object CallLive(string id, string method, params object[] arguments)
        {
            if (LiveInvoker == null) throw new InvalidOperationException("This prototype is no longer attached.");
            return LiveInvoker(id, method, arguments);
        }

        /// <summary>克隆一个 Unity 对象。</summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="original">被克隆对象。</param>
        /// <returns>克隆结果。</returns>
        protected static T Instantiate<T>(T original) where T : Object => Object.Instantiate(original);

        /// <summary>在指定姿态克隆一个 Unity 对象。</summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="original">被克隆对象。</param>
        /// <param name="position">世界坐标。</param>
        /// <param name="rotation">世界旋转。</param>
        /// <returns>克隆结果。</returns>
        protected static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object =>
            Object.Instantiate(original, position, rotation);

        /// <summary>销毁一个 Unity 对象。</summary>
        /// <param name="target">目标对象。</param>
        protected static void Destroy(Object target) => Object.Destroy(target);

        /// <summary>延迟销毁一个 Unity 对象。</summary>
        /// <param name="target">目标对象。</param>
        /// <param name="delay">延迟秒数。</param>
        protected static void Destroy(Object target, float delay) => Object.Destroy(target, delay);
    }
}
#endif
