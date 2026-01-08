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
    public abstract class UIPanel : MonoBehaviour, IPanel
    {
        public Transform Transform => transform;
        public PanelState State { get; set; }
        public PanelHandler Handler { get; set; }

        #region 动画配置
        
        /// <summary>
        /// 显示动画配置
        /// </summary>
        [SerializeField] protected UIAnimationConfig mShowAnimationConfig;
        
        /// <summary>
        /// 隐藏动画配置
        /// </summary>
        [SerializeField] protected UIAnimationConfig mHideAnimationConfig;
        
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

        #region Canvas 分离配置

        /// <summary>
        /// 动态元素列表
        /// </summary>
        protected readonly List<UIDynamicElement> mDynamicElements = new();

        /// <summary>
        /// 静态元素列表
        /// </summary>
        protected readonly List<UIStaticElement> mStaticElements = new();

        /// <summary>
        /// 动态 Canvas 引用
        /// </summary>
        [SerializeField] protected Canvas mDynamicCanvas;

        /// <summary>
        /// 静态 Canvas 引用
        /// </summary>
        [SerializeField] protected Canvas mStaticCanvas;

        /// <summary>
        /// 动态 Canvas
        /// </summary>
        public Canvas DynamicCanvas
        {
            get => mDynamicCanvas;
            set => mDynamicCanvas = value;
        }

        /// <summary>
        /// 静态 Canvas
        /// </summary>
        public Canvas StaticCanvas
        {
            get => mStaticCanvas;
            set => mStaticCanvas = value;
        }

        /// <summary>
        /// 注册动态元素
        /// </summary>
        internal void RegisterDynamicElement(UIDynamicElement element)
        {
            if (element == null || mDynamicElements.Contains(element)) return;
            mDynamicElements.Add(element);
            
            // 如果有动态 Canvas，移动元素
            if (mDynamicCanvas != null && element.AutoMoveToCanvas)
            {
                element.transform.SetParent(mDynamicCanvas.transform, true);
            }
        }

        /// <summary>
        /// 注销动态元素
        /// </summary>
        internal void UnregisterDynamicElement(UIDynamicElement element)
        {
            if (element == null) return;
            mDynamicElements.Remove(element);
        }

        /// <summary>
        /// 注册静态元素
        /// </summary>
        internal void RegisterStaticElement(UIStaticElement element)
        {
            if (element == null || mStaticElements.Contains(element)) return;
            mStaticElements.Add(element);
            
            // 如果有静态 Canvas，移动元素
            if (mStaticCanvas != null && element.AutoMoveToCanvas)
            {
                element.transform.SetParent(mStaticCanvas.transform, true);
            }
        }

        /// <summary>
        /// 注销静态元素
        /// </summary>
        internal void UnregisterStaticElement(UIStaticElement element)
        {
            if (element == null) return;
            mStaticElements.Remove(element);
        }

        /// <summary>
        /// 获取所有动态元素
        /// </summary>
        public IReadOnlyList<UIDynamicElement> GetDynamicElements() => mDynamicElements;

        /// <summary>
        /// 获取所有静态元素
        /// </summary>
        public IReadOnlyList<UIStaticElement> GetStaticElements() => mStaticElements;

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

            if (mShowAnimation != null)
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
        }

        private void CompleteShow()
        {
            // 触发 OnShow
            SafeInvokeHook(OnShow, nameof(OnShow));
            
            // 触发 OnDidShow
            SafeInvokeHook(OnDidShow, nameof(OnDidShow));
            EventKit.Type.Send(new PanelDidShowEvent { Panel = this });
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

            if (mHideAnimation != null && gameObject.activeInHierarchy)
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
        }

        private void CompleteHide(bool deactivate)
        {
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
            foreach (var action in mOnClosed)
            {
                action?.Invoke();
            }
            mOnClosed.Clear();
            OnClose();
        }

        public void OnClosed(Action onClosed) => mOnClosed.Add(onClosed);

        #region Lifecycle Hooks - Existing

        protected virtual void OnInit(IUIData data = null) { }
        protected virtual void OnOpen(IUIData data = null) { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnClose() { }

        #endregion

        #region Lifecycle Hooks - New

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
        
        // Internal methods for UIStackManager to call
        internal void InvokeFocus() => OnFocus();
        internal void InvokeBlur() => OnBlur();
        internal void InvokeResume() => OnResume();

        #endregion

        #region Safe Hook Invocation

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

        protected virtual void OnBeforeDestroy()
        {
            StopAnimations();
            ClearUIComponents();
        }

        protected virtual void ClearUIComponents() { }

        protected void CloseSelf() => UIKit.ClosePanel(this);

        private void OnDestroy()
        {
            OnBeforeDestroy();
        }
    }
}