using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UI 堆栈管理器
    /// 支持多命名栈，用于管理不同导航上下文
    /// </summary>
    internal static class UIStackManager
    {
        private static readonly Dictionary<string, PooledLinkedList<IPanel>> sStacks = new();
        
        /// <summary>
        /// 默认栈名称
        /// </summary>
        public const string DEFAULT_STACK = "main";

        #region Stack Operations

        /// <summary>
        /// 压入面板到指定栈
        /// </summary>
        /// <param name="panel">要压入的面板</param>
        /// <param name="stackName">栈名称</param>
        /// <param name="hidePrevious">是否隐藏前一个面板</param>
        public static void Push(IPanel panel, string stackName = DEFAULT_STACK, bool hidePrevious = true)
        {
            if (panel?.Handler == null) return;

            var stack = GetOrCreateStack(stackName);

            // 如果面板已在栈中，先移除
            if (panel.Handler.OnStack != null)
            {
                var oldStack = GetStackContaining(panel);
                oldStack?.Remove(panel.Handler.OnStack);
            }

            // 触发前一个面板的 OnBlur
            if (hidePrevious && stack.Count > 0)
            {
                var previousPanel = stack.Last.Value;
                TriggerOnBlur(previousPanel);
                previousPanel.Hide();
            }

            // 压入新面板
            panel.Handler.OnStack = stack.AddLast(panel);
            panel.Handler.StackName = stackName;

            // 触发新面板的 OnFocus
            TriggerOnFocus(panel);
        }

        /// <summary>
        /// 从指定栈弹出面板
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <param name="showPrevious">是否显示前一个面板</param>
        /// <param name="autoClose">是否自动关闭弹出的面板</param>
        /// <returns>弹出的面板</returns>
        public static IPanel Pop(string stackName = DEFAULT_STACK, bool showPrevious = true, bool autoClose = true)
        {
            if (!sStacks.TryGetValue(stackName, out var stack) || stack.Count == 0)
            {
                KitLogger.Warning($"[UIKit] Cannot pop from empty stack: {stackName}");
                return null;
            }

            var panel = stack.Last.Value;
            stack.RemoveLast();
            panel.Handler.OnStack = null;

            // 触发弹出面板的 OnBlur
            TriggerOnBlur(panel);

            // 显示并触发前一个面板的 OnFocus 和 OnResume
            if (showPrevious && stack.Count > 0)
            {
                var previousPanel = stack.Last.Value;
                previousPanel.Show();
                TriggerOnResume(previousPanel);
                TriggerOnFocus(previousPanel);
            }

            if (autoClose)
            {
                panel.Close();
            }

            return panel;
        }

        /// <summary>
        /// 查看栈顶面板（不移除）
        /// </summary>
        public static IPanel Peek(string stackName = DEFAULT_STACK)
        {
            if (!sStacks.TryGetValue(stackName, out var stack) || stack.Count == 0)
            {
                return null;
            }
            return stack.Last.Value;
        }

        /// <summary>
        /// 获取指定栈的深度
        /// </summary>
        public static int GetDepth(string stackName = DEFAULT_STACK)
        {
            if (!sStacks.TryGetValue(stackName, out var stack))
            {
                return 0;
            }
            return stack.Count;
        }

        /// <summary>
        /// 清空指定栈
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <param name="closeAll">是否关闭所有面板</param>
        public static void Clear(string stackName = DEFAULT_STACK, bool closeAll = true)
        {
            if (!sStacks.TryGetValue(stackName, out var stack))
            {
                return;
            }

            if (closeAll)
            {
                while (stack.Count > 0)
                {
                    var panel = stack.Last.Value;
                    stack.RemoveLast();
                    panel.Handler.OnStack = null;
                    panel.Close();
                }
            }
            else
            {
                var node = stack.First;
                while (node != null)
                {
                    node.Value.Handler.OnStack = null;
                    node = node.Next;
                }
                stack.Clear();
            }
        }

        /// <summary>
        /// 从栈中移除指定面板（不关闭）
        /// </summary>
        public static void RemoveFromStack(IPanel panel)
        {
            if (panel?.Handler?.OnStack == null) return;

            var stack = GetStackContaining(panel);
            if (stack != null)
            {
                // 检查是否是栈顶
                bool wasTop = stack.Last?.Value == panel;
                
                stack.Remove(panel.Handler.OnStack);
                panel.Handler.OnStack = null;

                // 如果移除的是栈顶，触发新栈顶的 OnFocus
                if (wasTop && stack.Count > 0)
                {
                    var newTop = stack.Last.Value;
                    TriggerOnFocus(newTop);
                }
            }
        }

        /// <summary>
        /// 获取所有栈名称
        /// </summary>
        public static IReadOnlyCollection<string> GetStackNames()
        {
            return sStacks.Keys;
        }

        /// <summary>
        /// 检查面板是否在任意栈中
        /// </summary>
        public static bool IsInStack(IPanel panel)
        {
            return panel?.Handler?.OnStack != null;
        }

        /// <summary>
        /// 获取面板所在的栈名称
        /// </summary>
        public static string GetStackName(IPanel panel)
        {
            return panel?.Handler?.StackName;
        }

        #endregion

        #region Internal Methods

        private static PooledLinkedList<IPanel> GetOrCreateStack(string stackName)
        {
            if (!sStacks.TryGetValue(stackName, out var stack))
            {
                stack = new PooledLinkedList<IPanel>();
                sStacks[stackName] = stack;
            }
            return stack;
        }

        private static PooledLinkedList<IPanel> GetStackContaining(IPanel panel)
        {
            if (panel?.Handler?.StackName == null) return null;
            
            sStacks.TryGetValue(panel.Handler.StackName, out var stack);
            return stack;
        }

        private static void TriggerOnFocus(IPanel panel)
        {
            if (panel is UIPanel uiPanel)
            {
                uiPanel.InvokeFocus();
            }
        }

        private static void TriggerOnBlur(IPanel panel)
        {
            if (panel is UIPanel uiPanel)
            {
                uiPanel.InvokeBlur();
            }
        }

        private static void TriggerOnResume(IPanel panel)
        {
            if (panel is UIPanel uiPanel)
            {
                uiPanel.InvokeResume();
            }
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        #region UniTask Async Methods

        /// <summary>
        /// [UniTask] 异步弹出面板（等待动画完成）
        /// </summary>
        public static async UniTask<IPanel> PopUniTaskAsync(
            string stackName = DEFAULT_STACK, 
            bool showPrevious = true, 
            bool autoClose = true, 
            CancellationToken ct = default)
        {
            if (!sStacks.TryGetValue(stackName, out var stack) || stack.Count == 0)
            {
                KitLogger.Warning($"[UIKit] Cannot pop from empty stack: {stackName}");
                return null;
            }

            var panel = stack.Last.Value;
            stack.RemoveLast();
            panel.Handler.OnStack = null;

            // 触发弹出面板的 OnBlur
            TriggerOnBlur(panel);

            // 显示并触发前一个面板的 OnFocus 和 OnResume
            if (showPrevious && stack.Count > 0)
            {
                var previousPanel = stack.Last.Value;
                
                if (previousPanel is UIPanel uiPreviousPanel)
                {
                    await uiPreviousPanel.ShowUniTaskAsync(ct);
                }
                else
                {
                    previousPanel.Show();
                }
                
                TriggerOnResume(previousPanel);
                TriggerOnFocus(previousPanel);
            }

            if (autoClose)
            {
                if (panel is UIPanel uiPanel)
                {
                    await uiPanel.HideUniTaskAsync(ct);
                }
                panel.Close();
            }

            return panel;
        }

        #endregion
#endif

        #region Legacy Compatibility

        /// <summary>
        /// 获取默认栈的引用（用于向后兼容）
        /// </summary>
        internal static PooledLinkedList<IPanel> GetDefaultStack()
        {
            return GetOrCreateStack(DEFAULT_STACK);
        }

        /// <summary>
        /// 清理所有栈
        /// </summary>
        internal static void ClearAll()
        {
            foreach (var stack in sStacks.Values)
            {
                var node = stack.First;
                while (node != null)
                {
                    if (node.Value?.Handler != null)
                    {
                        node.Value.Handler.OnStack = null;
                    }
                    node = node.Next;
                }
                stack.Clear();
            }
            sStacks.Clear();
        }

        #endregion
    }
}
