#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Diagnostics;

namespace YokiFrame
{
    /// <summary>
    /// 发布 FsmKit 生命周期诊断事件；事件不依赖具体编辑器，因此 Unity、Godot 与工具宿主共享同一契约。
    /// </summary>
    public static class FsmEditorHook
    {
        /// <summary>状态机创建后触发。</summary>
        public static event Action<IFSM> OnFsmCreated;

        /// <summary>状态机显式释放前触发。</summary>
        public static event Action<IFSM> OnFsmDisposed;

        /// <summary>状态机完成 Clear 后触发。</summary>
        public static event Action<IFSM> OnFsmCleared;

        /// <summary>状态机成功启动后触发。</summary>
        public static event Action<IFSM, string> OnFsmStarted;

        /// <summary>普通 FSM 成功切换状态后触发。</summary>
        public static event Action<IFSM, string, string> OnStateChanged;

        /// <summary>状态成功加入后触发。</summary>
        public static event Action<IFSM, string> OnStateAdded;

        /// <summary>状态成功移除前触发。</summary>
        public static event Action<IFSM, string> OnStateRemoved;

        /// <summary>通知状态机已经创建。</summary>
        /// <param name="fsm">状态机实例。</param>
        internal static void RaiseFsmCreated(IFSM fsm) => InvokeSafely(OnFsmCreated, fsm);

        /// <summary>通知状态机即将释放。</summary>
        /// <param name="fsm">状态机实例。</param>
        internal static void RaiseFsmDisposed(IFSM fsm) => InvokeSafely(OnFsmDisposed, fsm);

        /// <summary>通知状态机已经清空。</summary>
        /// <param name="fsm">状态机实例。</param>
        internal static void RaiseFsmCleared(IFSM fsm) => InvokeSafely(OnFsmCleared, fsm);

        /// <summary>通知状态机已经启动。</summary>
        /// <param name="fsm">状态机实例。</param>
        /// <param name="state">初始状态名称。</param>
        internal static void RaiseFsmStarted(IFSM fsm, string state) => InvokeSafely(OnFsmStarted, fsm, state);

        /// <summary>通知普通 FSM 已切换状态。</summary>
        /// <param name="fsm">状态机实例。</param>
        /// <param name="from">来源状态名称。</param>
        /// <param name="to">目标状态名称。</param>
        internal static void RaiseStateChanged(IFSM fsm, string from, string to) =>
            InvokeSafely(OnStateChanged, fsm, from, to);

        /// <summary>通知状态已经加入。</summary>
        /// <param name="fsm">状态机实例。</param>
        /// <param name="state">状态名称。</param>
        internal static void RaiseStateAdded(IFSM fsm, string state) => InvokeSafely(OnStateAdded, fsm, state);

        /// <summary>通知状态即将移除。</summary>
        /// <param name="fsm">状态机实例。</param>
        /// <param name="state">状态名称。</param>
        internal static void RaiseStateRemoved(IFSM fsm, string state) => InvokeSafely(OnStateRemoved, fsm, state);

        /// <summary>逐个通知无状态参数观察者，单个观察者失败只写入调试输出。</summary>
        /// <param name="callbacks">当前事件的订阅者快照。</param>
        /// <param name="fsm">状态机实例。</param>
        private static void InvokeSafely(Action<IFSM> callbacks, IFSM fsm)
        {
            if (callbacks == null)
            {
                return;
            }

            foreach (Delegate subscriber in callbacks.GetInvocationList())
            {
                try
                {
                    ((Action<IFSM>)subscriber)(fsm);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                }
            }
        }

        /// <summary>逐个通知单状态参数观察者，保证后续观察者仍能收到事件。</summary>
        /// <param name="callbacks">当前事件的订阅者快照。</param>
        /// <param name="fsm">状态机实例。</param>
        /// <param name="state">事件关联的状态名称。</param>
        private static void InvokeSafely(Action<IFSM, string> callbacks, IFSM fsm, string state)
        {
            if (callbacks == null)
            {
                return;
            }

            foreach (Delegate subscriber in callbacks.GetInvocationList())
            {
                try
                {
                    ((Action<IFSM, string>)subscriber)(fsm, state);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                }
            }
        }

        /// <summary>逐个通知状态切换观察者，隔离任一订阅者抛出的异常。</summary>
        /// <param name="callbacks">当前事件的订阅者快照。</param>
        /// <param name="fsm">状态机实例。</param>
        /// <param name="from">来源状态名称。</param>
        /// <param name="to">目标状态名称。</param>
        private static void InvokeSafely(
            Action<IFSM, string, string> callbacks,
            IFSM fsm,
            string from,
            string to)
        {
            if (callbacks == null)
            {
                return;
            }

            foreach (Delegate subscriber in callbacks.GetInvocationList())
            {
                try
                {
                    ((Action<IFSM, string, string>)subscriber)(fsm, from, to);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine(exception);
                }
            }
        }
    }
}
#endif
