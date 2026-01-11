#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using System;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// InputKit - 输入上下文系统
    /// </summary>
    public static partial class InputKit
    {
        private static InputContextStack sContextStack;

        #region 属性

        /// <summary>当前活动上下文</summary>
        public static InputContext CurrentContext => sContextStack != default 
            ? sContextStack.Current 
            : default;

        /// <summary>上下文栈深度</summary>
        public static int ContextDepth => sContextStack != default 
            ? sContextStack.Depth 
            : 0;

        #endregion

        #region 事件

        /// <summary>上下文变更事件（oldContext, newContext）</summary>
        public static event Action<InputContext, InputContext> OnContextChanged;

        #endregion

        #region 上下文注册

        /// <summary>
        /// 注册上下文（用于按名称查找）
        /// </summary>
        public static void RegisterContext(InputContext context)
        {
            EnsureContextStackInitialized();
            sContextStack.Register(context);
        }

        /// <summary>
        /// 注销上下文
        /// </summary>
        public static void UnregisterContext(string contextName)
        {
            sContextStack?.Unregister(contextName);
        }

        #endregion

        #region 栈操作

        /// <summary>
        /// 压入新上下文
        /// </summary>
        public static void PushContext(InputContext context)
        {
            EnsureContextStackInitialized();
            sContextStack.Push(context);
        }

        /// <summary>
        /// 压入新上下文（按名称）
        /// </summary>
        public static void PushContext(string contextName)
        {
            EnsureContextStackInitialized();
            sContextStack.Push(contextName);
        }

        /// <summary>
        /// 弹出当前上下文
        /// </summary>
        public static InputContext PopContext()
        {
            return sContextStack != default ? sContextStack.Pop() : default;
        }

        /// <summary>
        /// 弹出到指定上下文
        /// </summary>
        public static void PopToContext(string contextName)
        {
            sContextStack?.PopTo(contextName);
        }

        /// <summary>
        /// 清空上下文栈（恢复默认）
        /// </summary>
        public static void ClearContextStack()
        {
            sContextStack?.Clear();
        }

        #endregion

        #region 查询

        /// <summary>
        /// 检查 Action 是否被当前上下文阻断（类型安全）
        /// </summary>
        /// <param name="action">InputAction 引用</param>
        /// <returns>是否被阻断</returns>
        public static bool IsActionBlocked(InputAction action)
        {
            if (action == default) return false;
            return sContextStack != default && sContextStack.IsActionBlocked(action.name);
        }

        /// <summary>
        /// 检查 Action 是否被当前上下文阻断（字符串，向后兼容）
        /// </summary>
        /// <param name="actionName">Action 名称</param>
        /// <returns>是否被阻断</returns>
        public static bool IsActionBlocked(string actionName)
        {
            return sContextStack != default && sContextStack.IsActionBlocked(actionName);
        }

        /// <summary>
        /// 检查是否包含指定上下文
        /// </summary>
        /// <param name="contextName">上下文名称</param>
        /// <returns>是否包含</returns>
        public static bool HasContext(string contextName)
        {
            return sContextStack != default && sContextStack.Contains(contextName);
        }

        #endregion

        #region 内部方法

        private static void EnsureContextStackInitialized()
        {
            if (sContextStack != default) return;
            
            sContextStack = new InputContextStack();
            sContextStack.OnContextChanged += HandleContextChanged;
        }

        private static void HandleContextChanged(InputContext oldContext, InputContext newContext)
        {
            OnContextChanged?.Invoke(oldContext, newContext);
        }

        /// <summary>
        /// 重置上下文系统（内部调用）
        /// </summary>
        internal static void ResetContext()
        {
            if (sContextStack != default)
            {
                sContextStack.OnContextChanged -= HandleContextChanged;
                sContextStack.Clear();
                sContextStack = default;
            }
            
            OnContextChanged = null;
        }

        #endregion
    }
}

#endif