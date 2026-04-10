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
        #region 动画控制

        /// <summary>
        /// 设置显示动画（会归还旧动画到池）
        /// </summary>
        public void SetShowAnimation(IUIAnimation animation)
        {
            if (mShowAnimation != default)
            {
                mShowAnimation.Stop();
                mShowAnimation.Recycle();
            }
            mShowAnimation = animation;
        }

        /// <summary>
        /// 设置隐藏动画（会归还旧动画到池）
        /// </summary>
        public void SetHideAnimation(IUIAnimation animation)
        {
            if (mHideAnimation != default)
            {
                mHideAnimation.Stop();
                mHideAnimation.Recycle();
            }
            mHideAnimation = animation;
        }

        /// <summary>
        /// 停止当前动画
        /// </summary>
        public void StopAnimations()
        {
            if (mShowAnimation != default) mShowAnimation.Stop();
            if (mHideAnimation != default) mHideAnimation.Stop();
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask 异步方法

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
            else if (mShowAnimation != default)
            {
                var tcs = AutoResetUniTaskCompletionSource.Create();
                var rectTransform = transform as RectTransform;
                mShowAnimation.Play(rectTransform, static state => ((AutoResetUniTaskCompletionSource)state).TrySetResult(), tcs);
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
            else if (mHideAnimation != default && gameObject.activeInHierarchy)
            {
                var tcs = AutoResetUniTaskCompletionSource.Create();
                var rectTransform = transform as RectTransform;
                mHideAnimation.Play(rectTransform, static state => ((AutoResetUniTaskCompletionSource)state).TrySetResult(), tcs);
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
