#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 表示一次面板 Prefab 获取；每个成功结果拥有独立且幂等的释放权。
    /// </summary>
    public interface IPanelPrefabLease : IDisposable
    {
        /// <summary>获取本次 Prefab 加载使用的 ResKit location。</summary>
        string Location { get; }

        /// <summary>获取当前 lease 持有的面板 Prefab；释放后返回空值。</summary>
        GameObject Prefab { get; }
    }
}
#endif
