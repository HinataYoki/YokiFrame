#if UNITY_2022_3_OR_NEWER
using System;

namespace YokiFrame
{
    public abstract partial class UIPanel
    {
        /// <summary>
        /// 面板实例初始化时调用一次；预加载时数据可能为空。
        /// </summary>
        /// <param name="data">首次物化请求携带的数据。</param>
        protected virtual void OnInit(IUIData data = null) { }

        /// <summary>
        /// 每次 Open 请求调用，用于提交本轮业务数据。
        /// </summary>
        /// <param name="data">本轮打开数据。</param>
        protected virtual void OnOpen(IUIData data = null) { }

        /// <summary>
        /// 面板即将从非可见状态进入可见状态时调用。
        /// </summary>
        protected virtual void OnWillShow() { }

        /// <summary>
        /// 面板进入可见状态时调用。
        /// </summary>
        protected virtual void OnShow() { }

        /// <summary>
        /// 面板完成可见状态提交后调用。
        /// </summary>
        protected virtual void OnDidShow() { }

        /// <summary>
        /// 面板即将离开可见状态时调用。
        /// </summary>
        protected virtual void OnWillHide() { }

        /// <summary>
        /// 面板进入隐藏状态时调用。
        /// </summary>
        protected virtual void OnHide() { }

        /// <summary>
        /// 面板完成隐藏状态提交后调用。
        /// </summary>
        protected virtual void OnDidHide() { }

        /// <summary>
        /// 当前打开轮次关闭时调用；是否销毁实例由缓存策略决定。
        /// </summary>
        protected virtual void OnClose() { }

        /// <summary>
        /// 面板成为命名栈顶部时调用。
        /// </summary>
        protected virtual void OnFocus() { }

        /// <summary>
        /// 面板失去命名栈顶部位置时调用。
        /// </summary>
        protected virtual void OnBlur() { }

        /// <summary>
        /// 上层面板离栈后当前面板恢复时调用。
        /// </summary>
        protected virtual void OnResume() { }

        /// <summary>
        /// 实例即将被 UIKit 或外部 Unity 生命周期销毁时调用一次。
        /// </summary>
        protected virtual void OnBeforeDestroy()
        {
            ReleaseAnimations();
            ClearUIComponents();
        }

        /// <summary>
        /// 清理由 Bind 代码生成器注入的面板引用；旧版 Designer 文件通过该钩子释放字段。
        /// </summary>
        protected virtual void ClearUIComponents() { }

        /// <summary>
        /// 供派生面板关闭自身，仍由 UIKit owner 统一完成清理。
        /// </summary>
        protected void CloseSelf()
        {
            UIKit.ClosePanel(this);
        }

        /// <summary>
        /// 安全调用一次初始化钩子，用户异常只记录而不破坏 owner 状态。
        /// </summary>
        internal void InvokeInit(IUIData data)
        {
            InvokeHook(() => OnInit(data));
        }

        /// <summary>
        /// 安全调用一次打开钩子。
        /// </summary>
        internal void InvokeOpen(IUIData data)
        {
            InvokeHook(() => OnOpen(data));
        }

        /// <summary>
        /// 安全调用显示前钩子。
        /// </summary>
        internal void InvokeWillShow()
        {
            InvokeHook(OnWillShow);
        }

        /// <summary>
        /// 安全调用显示钩子。
        /// </summary>
        internal void InvokeShow()
        {
            InvokeHook(OnShow);
        }

        /// <summary>
        /// 安全调用显示完成钩子。
        /// </summary>
        internal void InvokeDidShow()
        {
            InvokeHook(OnDidShow);
        }

        /// <summary>
        /// 安全调用隐藏前钩子。
        /// </summary>
        internal void InvokeWillHide()
        {
            InvokeHook(OnWillHide);
        }

        /// <summary>
        /// 安全调用隐藏钩子。
        /// </summary>
        internal void InvokeHide()
        {
            InvokeHook(OnHide);
        }

        /// <summary>
        /// 安全调用隐藏完成钩子。
        /// </summary>
        internal void InvokeDidHide()
        {
            InvokeHook(OnDidHide);
        }

        /// <summary>
        /// 安全调用关闭钩子。
        /// </summary>
        internal void InvokeClose()
        {
            InvokeHook(OnClose);
        }

        /// <summary>
        /// 安全调用获得焦点钩子。
        /// </summary>
        internal void InvokeFocus()
        {
            InvokeHook(OnFocus);
        }

        /// <summary>
        /// 安全调用失去焦点钩子。
        /// </summary>
        internal void InvokeBlur()
        {
            InvokeHook(OnBlur);
        }

        /// <summary>
        /// 安全调用恢复钩子。
        /// </summary>
        internal void InvokeResume()
        {
            InvokeHook(OnResume);
        }

        /// <summary>
        /// 安全调用销毁前钩子。
        /// </summary>
        internal void InvokeBeforeDestroy()
        {
            if (mBeforeDestroyInvoked) return;
            mBeforeDestroyInvoked = true;
            InvokeHook(OnBeforeDestroy);
        }

        /// <summary>
        /// 提取并清空本轮关闭回调，使 Controller 可以先提交完整关闭终态再通知业务。
        /// </summary>
        internal Action[] TakeClosedCallbacks()
        {
            Action[] callbacks = mClosedCallbacks.ToArray();
            mClosedCallbacks.Clear();
            return callbacks;
        }

        /// <summary>
        /// 在实例完成缓存或销毁后安全执行关闭回调，单个异常不会阻止后续回调。
        /// </summary>
        /// <param name="callbacks">已从面板提取的一次性回调快照。</param>
        internal static void InvokeClosedCallbacks(Action[] callbacks)
        {
            if (callbacks == null) return;
            for (var index = 0; index < callbacks.Length; index++)
            {
                try
                {
                    callbacks[index]();
                }
                catch (Exception exception)
                {
                    LogKit.Exception(exception);
                }
            }
        }

        /// <summary>
        /// 隔离用户钩子异常，确保 UIKit 状态机始终能进入终态。
        /// </summary>
        private void InvokeHook(Action hook)
        {
            try
            {
                hook();
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception, this);
            }
        }
    }
}
#endif
