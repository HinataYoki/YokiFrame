using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 输入上下文栈管理
    /// </summary>
    public sealed class InputContextStack
    {
        private readonly List<InputContext> mStack = new(8);
        private readonly Dictionary<string, InputContext> mContextMap = new();
        
        /// <summary>上下文变更事件（oldContext, newContext）</summary>
        public event Action<InputContext, InputContext> OnContextChanged;

        /// <summary>当前活动上下文</summary>
        public InputContext Current => mStack.Count > 0 ? mStack[^1] : default;

        /// <summary>栈深度</summary>
        public int Depth => mStack.Count;

        #region 上下文注册

        /// <summary>
        /// 注册上下文（用于按名称查找）
        /// </summary>
        public void Register(InputContext context)
        {
            if (context == default || string.IsNullOrEmpty(context.ContextName)) return;
            mContextMap[context.ContextName] = context;
        }

        /// <summary>
        /// 注销上下文
        /// </summary>
        public void Unregister(string contextName)
        {
            mContextMap.Remove(contextName);
        }

        #endregion

        #region 栈操作

        /// <summary>
        /// 压入上下文
        /// </summary>
        public void Push(InputContext context)
        {
            if (context == default) return;
            
            var oldContext = Current;
            mStack.Add(context);
            
            ApplyContext(context);
            OnContextChanged?.Invoke(oldContext, context);
        }

        /// <summary>
        /// 压入上下文（按名称）
        /// </summary>
        public void Push(string contextName)
        {
            if (mContextMap.TryGetValue(contextName, out var context))
            {
                Push(context);
            }
            else
            {
                Debug.LogWarning($"[InputKit] 找不到上下文: {contextName}");
            }
        }

        /// <summary>
        /// 弹出当前上下文
        /// </summary>
        public InputContext Pop()
        {
            if (mStack.Count == 0) return default;
            
            var oldContext = mStack[^1];
            mStack.RemoveAt(mStack.Count - 1);
            
            var newContext = Current;
            if (newContext != default)
            {
                ApplyContext(newContext);
            }
            
            OnContextChanged?.Invoke(oldContext, newContext);
            return oldContext;
        }

        /// <summary>
        /// 弹出到指定上下文
        /// </summary>
        public void PopTo(string contextName)
        {
            var oldContext = Current;
            
            while (mStack.Count > 0)
            {
                var top = mStack[^1];
                if (top.ContextName == contextName) break;
                mStack.RemoveAt(mStack.Count - 1);
            }
            
            var newContext = Current;
            if (newContext != oldContext)
            {
                if (newContext != default)
                {
                    ApplyContext(newContext);
                }
                OnContextChanged?.Invoke(oldContext, newContext);
            }
        }

        /// <summary>
        /// 清空上下文栈
        /// </summary>
        public void Clear()
        {
            var oldContext = Current;
            mStack.Clear();
            OnContextChanged?.Invoke(oldContext, default);
        }

        #endregion

        #region 查询

        /// <summary>
        /// 检查 Action 是否被当前上下文阻断
        /// </summary>
        public bool IsActionBlocked(string actionName)
        {
            var context = Current;
            if (context == default) return false;
            
            if (context.BlockedActions == default) return false;
            
            for (int i = 0; i < context.BlockedActions.Length; i++)
            {
                if (context.BlockedActions[i] == actionName)
                    return true;
            }
            
            return false;
        }

        /// <summary>
        /// 检查是否包含指定上下文
        /// </summary>
        public bool Contains(string contextName)
        {
            for (int i = 0; i < mStack.Count; i++)
            {
                if (mStack[i].ContextName == contextName)
                    return true;
            }
            return false;
        }

        #endregion

        #region 内部方法

        private static void ApplyContext(InputContext context)
        {
#if ENABLE_INPUT_SYSTEM
            if (context.EnabledActionMaps != default && context.EnabledActionMaps.Length > 0)
            {
                InputKit.EnableActionMaps(context.EnabledActionMaps);
            }
#endif
        }

        #endregion
    }
}
