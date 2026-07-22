#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 保存一个已物化面板的唯一可写所有权；公开面板除兼容性 Data 接口外只能读取这里的状态。
    /// </summary>
    internal sealed class PanelEntry
    {
        /// <summary>
        /// 创建一个尚未打开的面板条目，并接管 Prefab lease。
        /// </summary>
        internal PanelEntry(
            UIKitController controller,
            Type panelType,
            UIPanel panel,
            IPanelPrefabLease prefabLease,
            UILevel level,
            PanelCachePolicy cachePolicy)
        {
            Controller = controller ?? throw new ArgumentNullException(nameof(controller));
            PanelType = panelType ?? throw new ArgumentNullException(nameof(panelType));
            Panel = panel != default ? panel : throw new ArgumentNullException(nameof(panel));
            PrefabLease = prefabLease ?? throw new ArgumentNullException(nameof(prefabLease));
            Level = level;
            CachePolicy = cachePolicy;
            State = PanelState.Preloaded;
        }

        internal UIKitController Controller { get; }
        internal Type PanelType { get; }
        internal UIPanel Panel { get; set; }
        internal PanelLifetimeSentinel LifetimeSentinel { get; set; }
        internal IPanelPrefabLease PrefabLease { get; set; }
        internal IUIData Data { get; set; }
        internal UILevel Level { get; set; }
        internal int SubLevel { get; set; }
        internal string Tag { get; set; }
        internal PanelState State { get; set; }
        internal PanelCachePolicy CachePolicy { get; set; }
        internal bool IsModal { get; set; }
        internal bool HasOpened { get; set; }
        internal bool IsDisposing { get; set; }
        internal bool IsBlurInProgress { get; set; }
        internal bool IsLevelRegistered { get; set; }
        internal string StackName { get; set; }
        internal LinkedListNode<PanelEntry> StackNode { get; set; }
        internal LinkedListNode<PanelEntry> ReusableNode { get; set; }
        internal GameObject ModalBlocker { get; set; }
        internal long OpenSequence { get; set; }
        internal int TransitionGeneration { get; set; }

        /// <summary>
        /// 判断条目是否属于一次已经打开但尚未关闭的使用轮次。
        /// </summary>
        internal bool IsLogicallyOpen
        {
            get
            {
                return State == PanelState.Open
                    || State == PanelState.Opening
                    || State == PanelState.Hide
                    || State == PanelState.Hiding
                    || State == PanelState.Closing;
            }
        }
    }
}
#endif
