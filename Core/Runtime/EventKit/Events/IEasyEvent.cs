#if UNITY_EDITOR || (GODOT && TOOLS)
using System;
using System.Collections.Generic;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 所有 EventKit 底层事件容器的公共契约。
    /// </summary>
    public interface IEasyEvent
    {
        /// <summary>
        /// 移除当前容器内的全部监听器。
        /// </summary>
        void UnRegisterAll();

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 获取当前容器中仍被保存的监听器数量。
        /// </summary>
        int ListenerCount { get; }

        /// <summary>
        /// 枚举当前已注册监听器，供编辑器监控或诊断面板读取。
        /// </summary>
        /// <returns>当前容器内的委托集合。</returns>
        IEnumerable<Delegate> GetListeners();
#endif
    }
}
