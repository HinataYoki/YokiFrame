using System;
using UnityEngine;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UIPanel - 动画控制与 UniTask 异步方法
    /// </summary>
    public abstract partial class UIPanel
    {
        #region Animation Control

        /// <summary>
        /// 设置显示动画
        /// </summary>
        public void SetShowAnimation(IUIAnimation animation)
        {
            mShowAnimation?.Stop();
            mShowAnimation = animation;
        }

        /// <summary>
        /// 设置隐藏动画
        /// </summary>
        public void SetHideAnimation(IUIAnimation animation)
        {
            mHideAnimation?.Stop();
            mHideAnimation = animation;
        }

        /// <summary>
        /// 停止当前动画
        /// </summary>
        public void StopAnimations()
        {
            mShowAnimation?.Stop();
            mHideAnimation?.Stop();
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask Async Methods

        /// <summary>
        /// [UniTask] 异步显示面板
        /// </summary>
        public async UniTask ShowUniTaskAsync(CancellationToken ct = default)
        {
            gameObject.SetActive(true);
            
            // 触发 OnWillShow
            SafeInvokeHook(OnWillShow, nameof(OnWillShow));
            EventKit.Type.Send(new PanelWillShowEvent { Panel = this });

            if (mShowAnimation is IUIAnimationUniTask uniTaskAnim)
            {
                var rectTransform = transform as RectTransform;
                await uniTaskAnim.PlayUniTaskAsync(rectTransform, ct);
            }
            else if (mShowAnimation != null)
            {
                var tcs = new UniTaskCompletionSource();
                var rectTransform = transform as RectTransform;
                mShowAnimation.Play(rectTransform, () => tcs.TrySetResult());
                await tcs.Task;
            }

            // 触发 OnShow 和 OnDidShow
            SafeInvokeHook(OnShow, nameof(OnShow));
            SafeInvokeHook(OnDidShow, nameof(OnDidShow));
            EventKit.Type.Send(new PanelDidShowEvent { Panel = this });
        }

        /// <summary>
        /// [UniTask] 异步隐藏面板
        /// </summary>
        public async UniTask HideUniTaskAsync(CancellationToken ct = default)
        {
            State = PanelState.Hide;
            
            // 触发 OnWillHide
            SafeInvokeHook(OnWillHide, nameof(OnWillHide));
            EventKit.Type.Send(new PanelWillHideEvent { Panel = this });

            if (mHideAnimation is IUIAnimationUniTask uniTaskAnim && gameObject.activeInHierarchy)
            {
                var rectTransform = transform as RectTransform;
                await uniTaskAnim.PlayUniTaskAsync(rectTransform, ct);
            }
            else if (mHideAnimation != null && gameObject.activeInHierarchy)
            {
                var tcs = new UniTaskCompletionSource();
                var rectTransform = transform as RectTransform;
                mHideAnimation.Play(rectTransform, () => tcs.TrySetResult());
                await tcs.Task;
            }

            // 触发 OnHide 和 OnDidHide
            SafeInvokeHook(OnHide, nameof(OnHide));
            gameObject.SetActive(false);
            SafeInvokeHook(OnDidHide, nameof(OnDidHide));
            EventKit.Type.Send(new PanelDidHideEvent { Panel = this });
        }

        #endregion
#endif
    }
}
