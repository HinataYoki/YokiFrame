using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 事件接口
    /// </summary>
    public interface IEasyEvent
    {
        void UnRegisterAll();
        int ListenerCount { get; }
        IEnumerable<Delegate> GetListeners();
    }
}
