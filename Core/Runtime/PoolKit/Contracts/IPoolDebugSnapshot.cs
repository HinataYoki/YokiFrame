#if UNITY_EDITOR || (GODOT && TOOLS)
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 供 Editor/Tools 诊断按需读取缓存对象的内部契约，避免借还热路径复制整池明细。
    /// </summary>
    internal interface IPoolDebugSnapshot
    {
        /// <summary>
        /// 复制不超过指定数量的当前缓存对象，并返回实际缓存对象总数。
        /// </summary>
        /// <param name="result">接收缓存对象引用的列表。</param>
        /// <param name="maxCount">允许复制的最大对象数；负数按零处理。</param>
        /// <returns>当前缓存对象总数，不受明细复制上限影响。</returns>
        int CopyInactiveObjects(List<object> result, int maxCount);
    }
}
#endif
