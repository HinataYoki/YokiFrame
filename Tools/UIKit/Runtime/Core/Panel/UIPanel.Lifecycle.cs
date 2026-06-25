#if !GODOT
using System;

namespace YokiFrame
{
    /// <summary>
    /// UI 面板生命周期钩子与销毁清理逻辑。
    /// </summary>
    public abstract partial class UIPanel
    {
        /// <summary>
        /// 标记是否已清理，防止重复清理。
        /// </summary>
        private bool mIsCleanedUp;

        #region 生命周期钩子 - 基础

        protected virtual void OnInit(IUIData data = null) { }
        protected virtual void OnOpen(IUIData data = null) { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnClose() { }

        #endregion

        #region 生命周期钩子 - 扩展

        /// <summary>
        /// 在显示动画开始前调用。
        /// </summary>
        protected virtual void OnWillShow() { }

        /// <summary>
        /// 在显示动画完成后调用。
        /// </summary>
        protected virtual void OnDidShow() { }

        /// <summary>
        /// 在隐藏动画开始前调用。
        /// </summary>
        protected virtual void OnWillHide() { }

        /// <summary>
        /// 在隐藏动画完成后调用。
        /// </summary>
        protected virtual void OnDidHide() { }

        /// <summary>
        /// 当面板成为栈顶面板时调用。
        /// </summary>
        protected virtual void OnFocus()
        {
            EventKit.Type.Send(new PanelFocusEvent { Panel = this });
        }

        /// <summary>
        /// 当面板失去栈顶位置时调用。
        /// </summary>
        protected virtual void OnBlur()
        {
            EventKit.Type.Send(new PanelBlurEvent { Panel = this });
        }

        /// <summary>
        /// 当面板从栈中恢复时调用。
        /// </summary>
        protected virtual void OnResume()
        {
            EventKit.Type.Send(new PanelResumeEvent { Panel = this });
        }

        // 供 UIStackManager 调用的内部方法。
        internal void InvokeFocus() => OnFocus();
        internal void InvokeBlur() => OnBlur();
        internal void InvokeResume() => OnResume();

        #endregion

        #region 安全钩子调用

        /// <summary>
        /// 安全调用生命周期钩子，捕获异常并记录日志。
        /// </summary>
        /// <param name="hook">钩子方法。</param>
        /// <param name="hookName">钩子名称。</param>
        private void SafeInvokeHook(Action hook, string hookName)
        {
            try
            {
                if (hook != default) hook();
            }
            catch (Exception e)
            {
#if YOKIFRAME_ZSTRING_SUPPORT
                using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
                {
                    sb.Append("[UIKit] ");
                    sb.Append(GetType().Name);
                    sb.Append(".");
                    sb.Append(hookName);
                    sb.Append(" threw exception: ");
                    sb.Append(e.Message);
                    sb.Append("\n");
                    sb.Append(e.StackTrace);
                    KitLogger.Error(sb.ToString());
                }
#else
                KitLogger.Error("[UIKit] " + GetType().Name + "." + hookName + " threw exception: " + e.Message + "\n" + e.StackTrace);
#endif
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
        /// 销毁前清理资源（由 UIKit 在 DestroyPanel 前调用）。
        /// </summary>
        void IPanel.Cleanup()
        {
            if (mIsCleanedUp) return;
            mIsCleanedUp = true;
            OnBeforeDestroy();
        }

        /// <summary>
        /// 归还动画到对象池。
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
            // 标记正在销毁，防止访问单例。
            mIsDestroying = true;

            // 如果已通过 Cleanup 清理，跳过。
            if (mIsCleanedUp) return;
            mIsCleanedUp = true;
            OnBeforeDestroy();
        }
    }
}
#endif
