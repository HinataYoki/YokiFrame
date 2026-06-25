using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 所有 EasyEvent 容器的运行时公共契约。
    /// </summary>
    public interface IEasyEvent
    {
        /// <summary>移除事件容器中的全部监听器。</summary>
        void UnRegisterAll();

        /// <summary>当前已注册监听器数量。</summary>
        int ListenerCount { get; }

        /// <summary>枚举全部已注册委托，用于诊断或编辑器检查。</summary>
        IEnumerable<Delegate> GetListeners();
    }
}
