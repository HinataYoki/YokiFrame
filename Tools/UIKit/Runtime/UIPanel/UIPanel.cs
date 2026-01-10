using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    public abstract partial class UIPanel : MonoBehaviour, IPanel
    {
        public Transform Transform => transform;
        public PanelState State { get; set; }
        public PanelHandler Handler { get; set; }

        #region 动画配置
        
        /// <summary>
        /// 显示动画配置（支持多态序列化）
        /// </summary>
        [SerializeReference] protected UIAnimationConfig mShowAnimationConfig;
        
        /// <summary>
        /// 隐藏动画配置（支持多态序列化）
        /// </summary>
        [SerializeReference] protected UIAnimationConfig mHideAnimationConfig;
        
        /// <summary>
        /// 当前显示动画实例
        /// </summary>
        protected IUIAnimation mShowAnimation;
        
        /// <summary>
        /// 当前隐藏动画实例
        /// </summary>
        protected IUIAnimation mHideAnimation;
        
        #endregion

        #region 焦点配置
        
        /// <summary>
        /// 默认焦点元素
        /// </summary>
        [SerializeField] protected Selectable mDefaultSelectable;

        /// <summary>
        /// 获取默认焦点元素
        /// </summary>
        public Selectable GetDefaultSelectable() => mDefaultSelectable;

        /// <summary>
        /// 设置默认焦点元素
        /// </summary>
        public void SetDefaultSelectable(Selectable selectable) => mDefaultSelectable = selectable;
        
        #endregion



        private List<Action> mOnClosed = new();

        protected virtual void Awake()
        {
            // 创建动画实例
            if (mShowAnimationConfig != null)
            {
                mShowAnimation = UIAnimationFactory.Create(mShowAnimationConfig);
            }
            if (mHideAnimationConfig != null)
            {
                mHideAnimation = UIAnimationFactory.Create(mHideAnimationConfig);
            }
        }

        public void Init(IUIData data = null) => OnInit(data);

        public void Open(IUIData data = null)
        {
            State = PanelState.Open;
            OnOpen(data);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            ShowInternal(null);
        }

        /// <summary>
        /// 显示面板（带回调）
        /// </summary>
        public void Show(Action onComplete)
        {
            gameObject.SetActive(true);
            ShowInternal(onComplete);
        }

        private void ShowInternal(Action onComplete)
        {
            // 触发 OnWillShow
            SafeInvokeHook(OnWillShow, nameof(OnWillShow));
            EventKit.Type.Send(new PanelWillShowEvent { Panel = this });

#if YOKIFRAME_UNITASK_SUPPORT
            // UniTask 环境：使用异步播放
            if (mShowAnimation is IUIAnimationUniTask uniTaskAnim)
            {
                var rectTransform = transform as RectTransform;
                PlayShowAnimationUniTask(uniTaskAnim, rectTransform, onComplete).Forget();
            }
            else if (mShowAnimation != default)
            {
                var rectTransform = transform as RectTransform;
                var tcs = AutoResetUniTaskCompletionSource.Create();
                mShowAnimation.Play(rectTransform, static state => ((AutoResetUniTaskCompletionSource)state).TrySetResult(), tcs);
                PlayShowAnimationFallback(tcs.Task, onComplete).Forget();
            }
            else
            {
                CompleteShow();
                onComplete?.Invoke();
            }
#else
            // 无 UniTask：使用回调方式
            if (mShowAnimation != default)
            {
                var rectTransform = transform as RectTransform;
                mShowAnimation.Play(rectTransform, () =>
                {
                    CompleteShow();
                    onComplete?.Invoke();
                });
            }
            else
            {
                CompleteShow();
                onComplete?.Invoke();
            }
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        private async UniTaskVoid PlayShowAnimationUniTask(IUIAnimationUniTask anim, RectTransform target, Action onComplete)
        {
            try
            {
                await anim.PlayUniTaskAsync(target, this.GetCancellationTokenOnDestroy());
                CompleteShow();
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // 面板销毁时取消，忽略
            }
        }
        
        private async UniTaskVoid PlayShowAnimationFallback(UniTask task, Action onComplete)
        {
            try
            {
                await task;
                CompleteShow();
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // 忽略取消
            }
        }
#endif

        private void CompleteShow()
        {
            // 触发 OnShow
            SafeInvokeHook(OnShow, nameof(OnShow));
            
            // 触发 OnDidShow
            SafeInvokeHook(OnDidShow, nameof(OnDidShow));
            EventKit.Type.Send(new PanelDidShowEvent { Panel = this });

            // 通知焦点系统
            var focusSystem = UIFocusSystem.Instance;
            if (focusSystem != default)
            {
                focusSystem.OnPanelShow(this);
            }
        }

        public void Hide()
        {
            HideInternal(true, null);
        }

        /// <summary>
        /// 隐藏面板（带回调）
        /// </summary>
        public void Hide(Action onComplete)
        {
            HideInternal(true, onComplete);
        }

        private void HideInternal(bool deactivate, Action onComplete)
        {
            State = PanelState.Hide;
            
            // 触发 OnWillHide
            SafeInvokeHook(OnWillHide, nameof(OnWillHide));
            EventKit.Type.Send(new PanelWillHideEvent { Panel = this });

#if YOKIFRAME_UNITASK_SUPPORT
            // UniTask 环境：使用异步播放
            if (mHideAnimation is IUIAnimationUniTask uniTaskAnim && gameObject.activeInHierarchy)
            {
                var rectTransform = transform as RectTransform;
                PlayHideAnimationUniTask(uniTaskAnim, rectTransform, deactivate, onComplete).Forget();
            }
            else if (mHideAnimation != default && gameObject.activeInHierarchy)
            {
                var rectTransform = transform as RectTransform;
                var tcs = AutoResetUniTaskCompletionSource.Create();
                mHideAnimation.Play(rectTransform, static state => ((AutoResetUniTaskCompletionSource)state).TrySetResult(), tcs);
                PlayHideAnimationFallback(tcs.Task, deactivate, onComplete).Forget();
            }
            else
            {
                CompleteHide(deactivate);
                onComplete?.Invoke();
            }
#else
            // 无 UniTask：使用回调方式
            if (mHideAnimation != default && gameObject.activeInHierarchy)
            {
                var rectTransform = transform as RectTransform;
                mHideAnimation.Play(rectTransform, () =>
                {
                    CompleteHide(deactivate);
                    onComplete?.Invoke();
                });
            }
            else
            {
                CompleteHide(deactivate);
                onComplete?.Invoke();
            }
#endif
        }

#if YOKIFRAME_UNITASK_SUPPORT
        private async UniTaskVoid PlayHideAnimationUniTask(IUIAnimationUniTask anim, RectTransform target, bool deactivate, Action onComplete)
        {
            try
            {
                await anim.PlayUniTaskAsync(target, this.GetCancellationTokenOnDestroy());
                CompleteHide(deactivate);
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // 面板销毁时取消，忽略
            }
        }
        
        private async UniTaskVoid PlayHideAnimationFallback(UniTask task, bool deactivate, Action onComplete)
        {
            try
            {
                await task;
                CompleteHide(deactivate);
                onComplete?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // 忽略取消
            }
        }
#endif

        private void CompleteHide(bool deactivate)
        {
            // 通知焦点系统
            var focusSystem = UIFocusSystem.Instance;
            if (focusSystem != default)
            {
                focusSystem.OnPanelHide(this);
            }

            // 触发 OnHide
            SafeInvokeHook(OnHide, nameof(OnHide));
            
            if (deactivate)
            {
                gameObject.SetActive(false);
            }
            
            // 触发 OnDidHide
            SafeInvokeHook(OnDidHide, nameof(OnDidHide));
            EventKit.Type.Send(new PanelDidHideEvent { Panel = this });
        }

        void IPanel.Close()
        {
            Hide();
            State = PanelState.Close;

            // 通知焦点系统
            var focusSystem = UIFocusSystem.Instance;
            if (focusSystem != default)
            {
                focusSystem.OnPanelClose(this);
            }

            foreach (var action in mOnClosed)
            {
                action?.Invoke();
            }
            mOnClosed.Clear();
            OnClose();
        }

        public void OnClosed(Action onClosed) => mOnClosed.Add(onClosed);

        #region 生命周期钩子 - 基础

        protected virtual void OnInit(IUIData data = null) { }
        protected virtual void OnOpen(IUIData data = null) { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnClose() { }

        #endregion

        #region 生命周期钩子 - 扩展

        /// <summary>
        /// 在显示动画开始前调用
        /// </summary>
        protected virtual void OnWillShow() { }
        
        /// <summary>
        /// 在显示动画完成后调用
        /// </summary>
        protected virtual void OnDidShow() { }
        
        /// <summary>
        /// 在隐藏动画开始前调用
        /// </summary>
        protected virtual void OnWillHide() { }
        
        /// <summary>
        /// 在隐藏动画完成后调用
        /// </summary>
        protected virtual void OnDidHide() { }
        
        /// <summary>
        /// 当面板成为栈顶面板时调用
        /// </summary>
        protected virtual void OnFocus() 
        {
            EventKit.Type.Send(new PanelFocusEvent { Panel = this });
        }
        
        /// <summary>
        /// 当面板失去栈顶位置时调用
        /// </summary>
        protected virtual void OnBlur() 
        {
            EventKit.Type.Send(new PanelBlurEvent { Panel = this });
        }
        
        /// <summary>
        /// 当面板从栈中恢复时调用
        /// </summary>
        protected virtual void OnResume() 
        {
            EventKit.Type.Send(new PanelResumeEvent { Panel = this });
        }
        
        // 供 UIStackManager 调用的内部方法
        internal void InvokeFocus() => OnFocus();
        internal void InvokeBlur() => OnBlur();
        internal void InvokeResume() => OnResume();

        #endregion

        #region 安全钩子调用

        /// <summary>
        /// 安全调用生命周期钩子，捕获异常并记录日志
        /// </summary>
        private void SafeInvokeHook(Action hook, string hookName)
        {
            try
            {
                hook?.Invoke();
            }
            catch (Exception e)
            {
                KitLogger.Error($"[UIKit] {GetType().Name}.{hookName} threw exception: {e.Message}\n{e.StackTrace}");
            }
        }

        #endregion

        protected virtual void OnBeforeDestroy()
        {
            StopAnimations();
            RecycleAnimations();
            ClearUIComponents();
        }
        
        /// <summary>
        /// 标记是否已清理，防止重复清理
        /// </summary>
        private bool mIsCleanedUp;
        
        /// <summary>
        /// 销毁前清理资源（由 UIKit 在 DestroyPanel 前调用）
        /// </summary>
        void IPanel.Cleanup()
        {
            if (mIsCleanedUp) return;
            mIsCleanedUp = true;
            OnBeforeDestroy();
        }
        
        /// <summary>
        /// 归还动画到对象池
        /// </summary>
        private void RecycleAnimations()
        {
            if (mShowAnimation != default)
            {
                mShowAnimation.Recycle();
                mShowAnimation = null;
            }
            if (mHideAnimation != default)
            {
                mHideAnimation.Recycle();
                mHideAnimation = null;
            }
        }

        protected virtual void ClearUIComponents() { }

        protected void CloseSelf() => UIKit.ClosePanel(this);

        private void OnDestroy()
        {
            // 如果已通过 Cleanup 清理，跳过
            if (mIsCleanedUp) return;
            mIsCleanedUp = true;
            OnBeforeDestroy();
        }
    }
}