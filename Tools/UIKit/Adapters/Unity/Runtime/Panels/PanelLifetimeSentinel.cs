#if UNITY_2022_3_OR_NEWER
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 独立观察受管 GameObject 销毁，避免派生 UIPanel 的 Unity 消息隐藏基类清理。
    /// </summary>
    [DefaultExecutionOrder(-32000)]
    internal sealed class PanelLifetimeSentinel : MonoBehaviour
    {
        private PanelEntry mEntry;

        /// <summary>
        /// 绑定当前 GameObject 的唯一 owner entry。
        /// </summary>
        internal void Initialize(PanelEntry entry)
        {
            mEntry = entry;
        }

        /// <summary>
        /// UIKit 主动销毁前解除通知，避免同一 lease 被重复回收。
        /// </summary>
        internal void Detach()
        {
            mEntry = null;
        }

        /// <summary>
        /// GameObject 被外部销毁时通知 Controller 释放 owner 状态。
        /// </summary>
        private void OnDestroy()
        {
            PanelEntry entry = mEntry;
            mEntry = null;
            if (entry != null) entry.Controller.NotifyPanelDestroyed(entry);
        }
    }
}
#endif
