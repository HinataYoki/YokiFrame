#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 面板视图基类；生命周期只由 UIKit owner 调度。
    /// </summary>
    public abstract partial class UIPanel : MonoBehaviour, IPanel
    {
        private readonly List<Action> mClosedCallbacks = new();
        private PanelEntry mOwner;
        private bool mBeforeDestroyInvoked;

        /// <inheritdoc />
        public Transform Transform => transform;

        /// <inheritdoc />
        public string PanelName => GetType().Name;

        /// <inheritdoc />
        public UILevel Level => mOwner == null ? default : mOwner.Level;

        /// <inheritdoc />
        public int SubLevel => mOwner == null ? 0 : mOwner.SubLevel;

        /// <inheritdoc />
        public string Tag => mOwner == null ? null : mOwner.Tag;

        /// <inheritdoc />
        IUIData IPanel.Data
        {
            get => mOwner == null ? null : mOwner.Data;
            set
            {
                if (mOwner != null) mOwner.Data = value;
            }
        }

        /// <inheritdoc />
        public PanelState State => mOwner == null ? PanelState.Close : mOwner.State;

        /// <inheritdoc />
        public PanelCachePolicy CachePolicy => mOwner == null ? PanelCachePolicy.Transient : mOwner.CachePolicy;

        /// <inheritdoc />
        public bool IsModal => mOwner != null && mOwner.IsModal;

        /// <inheritdoc />
        public string StackName => mOwner == null ? null : mOwner.StackName;

        /// <inheritdoc />
        public void Show()
        {
            UIKit.ShowPanel(this);
        }

        /// <inheritdoc />
        public void Hide()
        {
            UIKit.HidePanel(this);
        }

        /// <inheritdoc />
        public void Close()
        {
            UIKit.ClosePanel(this);
        }

        /// <summary>
        /// 登记当前打开轮次关闭后的回调；回调执行一次后自动移除。
        /// </summary>
        /// <param name="callback">关闭完成后执行的回调。</param>
        public void OnClosed(Action callback)
        {
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            mClosedCallbacks.Add(callback);
        }

        /// <summary>
        /// 把面板绑定到唯一 owner；只允许物化阶段调用一次。
        /// </summary>
        internal void AttachOwner(PanelEntry owner)
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (mOwner != null && !ReferenceEquals(mOwner, owner))
                throw new InvalidOperationException("UIPanel is already owned by another UIKit entry.");
            InitializeAnimationConfigs();
            mOwner = owner;
        }

        /// <summary>
        /// 在 UIKit 主动销毁前解除 owner，避免 Unity OnDestroy 再次回收同一 lease。
        /// </summary>
        internal void DetachOwner()
        {
            mOwner = null;
            mClosedCallbacks.Clear();
        }

        /// <summary>
        /// 由 Unity 销毁回调通知 owner 处理外部销毁；主动销毁已提前解除 owner。
        /// </summary>
        private void OnDestroy()
        {
            InvokeBeforeDestroy();
            mOwner = null;
            mClosedCallbacks.Clear();
        }
    }
}
#endif
