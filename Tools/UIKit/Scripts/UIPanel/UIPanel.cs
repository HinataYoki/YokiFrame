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

            // 通知焦点系统
            UIFocusSystem.Instance?.OnPanelShow(this);
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
            // 通知焦点系统
            UIFocusSystem.Instance?.OnPanelHide(this);

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
            UIFocusSystem.Instance?.OnPanelClose(this);

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