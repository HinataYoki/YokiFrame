using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 输入上下文栈。
    /// </summary>
    public sealed class InputContextStack
    {
        private readonly List<InputContext> mStack = new(8);
        private readonly Dictionary<string, InputContext> mContextMap = new();

        /// <summary>
        /// 栈顶上下文变化事件。
        /// </summary>
        public event Action<InputContext, InputContext> OnContextChanged;

        /// <summary>
        /// 当前栈顶上下文。
        /// </summary>
        public InputContext Current => mStack.Count > 0 ? mStack[mStack.Count - 1] : null;

        /// <summary>
        /// 当前上下文栈深度。
        /// </summary>
        public int Depth => mStack.Count;

        /// <summary>
        /// 注册可通过名称压栈的上下文。
        /// </summary>
        public void Register(InputContext context)
        {
            if (context == null || string.IsNullOrEmpty(context.ContextName))
                return;

            mContextMap[context.ContextName] = context;
        }

        /// <summary>
        /// 注销指定名称的上下文。
        /// </summary>
        public void Unregister(string contextName)
        {
            if (string.IsNullOrEmpty(contextName))
                return;

            mContextMap.Remove(contextName);
        }

        /// <summary>
        /// 将上下文压入栈顶。
        /// </summary>
        public void Push(InputContext context)
        {
            if (context == null)
                return;

            var oldContext = Current;
            mStack.Add(context);
            OnContextChanged?.Invoke(oldContext, context);
        }

        /// <summary>
        /// 将已注册上下文压入栈顶。
        /// </summary>
        public void Push(string contextName)
        {
            InputContext context;
            if (mContextMap.TryGetValue(contextName, out context))
                Push(context);
        }

        /// <summary>
        /// 弹出当前栈顶上下文。
        /// </summary>
        public InputContext Pop()
        {
            if (mStack.Count == 0)
                return null;

            var oldContext = mStack[mStack.Count - 1];
            mStack.RemoveAt(mStack.Count - 1);
            OnContextChanged?.Invoke(oldContext, Current);
            return oldContext;
        }

        /// <summary>
        /// 弹出上下文直到指定上下文位于栈顶。
        /// </summary>
        public void PopTo(string contextName)
        {
            var oldContext = Current;
            while (mStack.Count > 0)
            {
                var top = mStack[mStack.Count - 1];
                if (top.ContextName == contextName)
                    break;

                mStack.RemoveAt(mStack.Count - 1);
            }

            if (!ReferenceEquals(oldContext, Current))
                OnContextChanged?.Invoke(oldContext, Current);
        }

        /// <summary>
        /// 清空上下文栈。
        /// </summary>
        public void Clear()
        {
            var oldContext = Current;
            mStack.Clear();
            if (oldContext != null)
                OnContextChanged?.Invoke(oldContext, null);
        }

        /// <summary>
        /// 判断当前上下文是否阻挡指定动作。
        /// </summary>
        public bool IsActionBlocked(string actionName)
        {
            var context = Current;
            return context != null && context.BlocksAction(actionName);
        }

        /// <summary>
        /// 判断上下文栈内是否包含指定名称的上下文。
        /// </summary>
        public bool Contains(string contextName)
        {
            for (var i = 0; i < mStack.Count; i++)
            {
                if (mStack[i].ContextName == contextName)
                    return true;
            }

            return false;
        }

        internal void CopyActiveContextDiagnostics(List<InputContextDiagnosticsSnapshot> output)
        {
            if (output == null)
                return;

            for (var i = 0; i < mStack.Count; i++)
            {
                output.Add(new InputContextDiagnosticsSnapshot(mStack[i], i));
            }
        }

        internal void CopyRegisteredContextDiagnostics(List<InputContextDiagnosticsSnapshot> output)
        {
            if (output == null)
                return;

            foreach (var pair in mContextMap)
            {
                output.Add(new InputContextDiagnosticsSnapshot(pair.Value, -1));
            }

            output.Sort(CompareContextSnapshots);
        }

        private static int CompareContextSnapshots(InputContextDiagnosticsSnapshot left, InputContextDiagnosticsSnapshot right)
        {
            return string.Compare(left.ContextName, right.ContextName, StringComparison.Ordinal);
        }
    }
}
