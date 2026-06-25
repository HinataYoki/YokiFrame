#if !GODOT
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
        public string PanelName => GetType().Name;

        public UILevel Level
        {
            get { return Handler != default ? Handler.Level : default; }
            set
            {
                if (Handler != default)
                    Handler.Level = value;
            }
        }

        public string Tag
        {
            get { return Handler != default ? Handler.Tag : null; }
            set
            {
                if (Handler != default)
                    Handler.Tag = value;
            }
        }

        IUIData IPanel.Data
        {
            get { return Handler != default ? Handler.Data : null; }
            set
            {
                if (Handler != default)
                    Handler.Data = value;
            }
        }

        /// <summary>
        /// 标记是否正在销毁（防止 OnDestroy 时访问单例）
        /// </summary>
        private bool mIsDestroying;
        private bool mHideLifecycleCompleted;

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
        /// 是否在导航模式下自动聚焦默认元素
        /// </summary>
        [Header("焦点配置")]
        [Tooltip("是否在导航模式下自动聚焦默认元素（仅手柄/键盘模式生效）")]
        [SerializeField] protected bool mAutoFocusOnShow = false;

        /// <summary>
        /// 默认焦点元素
        /// </summary>
        [Tooltip("默认聚焦的元素（为空则查找第一个 Selectable）")]
        [SerializeField] protected Selectable mDefaultSelectable;

        /// <summary>
        /// 是否在导航模式下自动聚焦
        /// </summary>
        public virtual bool AutoFocusOnShow => mAutoFocusOnShow;

        /// <summary>
        /// 获取默认焦点元素
        /// </summary>
        public Selectable GetDefaultSelectable() => mDefaultSelectable;

        /// <summary>
        /// 设置默认焦点元素
        /// </summary>
        public void SetDefaultSelectable(Selectable selectable) => mDefaultSelectable = selectable;

        /// <summary>
        /// 设置是否自动聚焦
        /// </summary>
        public void SetAutoFocusOnShow(bool value) => mAutoFocusOnShow = value;

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
            mHideLifecycleCompleted = false;
            State = PanelState.Open;
            OnOpen(data);
        }

        public void Show()
        {
            mHideLifecycleCompleted = false;
            gameObject.SetActive(true);
            ShowInternal(null);
        }

        /// <summary>
        /// 显示面板（带回调）
        /// </summary>
        public void Show(Action onComplete)
        {
            mHideLifecycleCompleted = false;
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

            // 通知焦点系统（防止销毁时访问单例）
            if (!mIsDestroying && UIRoot.sInstanceInternal != default)
            {
                UIRoot.Instance.OnPanelShowFocus(this);
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
            // 通知焦点系统（防止销毁时访问单例）
            if (!mIsDestroying && UIRoot.sInstanceInternal != default)
            {
                UIRoot.Instance.OnPanelHideFocus(this);
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
            mHideLifecycleCompleted = true;
        }

        void IPanel.Close()
        {
            if (!mHideLifecycleCompleted)
                Hide();
            State = PanelState.Close;

            foreach (var action in mOnClosed)
            {
                action?.Invoke();
            }
            mOnClosed.Clear();
            OnClose();
        }

        public void OnClosed(Action onClosed) => mOnClosed.Add(onClosed);
    }
}
#endif
