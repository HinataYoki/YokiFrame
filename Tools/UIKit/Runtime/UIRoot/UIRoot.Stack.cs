using System;
using System.Collections.Generic;
#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// UIRoot - 堆栈子系统
    /// </summary>
    public partial class UIRoot
    {
        #region 常量

        /// <summary>
        /// 默认栈名称
        /// </summary>
        public const string DEFAULT_STACK = "main";

        #endregion

        #region 堆栈数据

        private readonly Dictionary<string, PooledLinkedList<IPanel>> mStacks = new();

        #endregion

        #region 堆栈操作

        /// <summary>
        /// 压入面板到栈
        /// </summary>
        /// <param name="panel">要压入的面板</param>
        /// <param name="stackName">栈名称</param>
        /// <param name="hidePrevious">是否隐藏前一个面板</param>
        public void PushToStack(IPanel panel, string stackName = DEFAULT_STACK, bool hidePrevious = true)
        {
            if (panel == default || panel.Handler == default) return;

            var stack = GetOrCreateStack(stackName);

            // 如果面板已在栈中，先移除
            if (panel.Handler.OnStack != default)
            {
                var oldStack = GetStackContaining(panel);
                if (oldStack != default) oldStack.Remove(panel.Handler.OnStack);
            }

            // 隐藏前一个面板
            if (hidePrevious && stack.Count > 0)
            {
                var previousPanel = stack.Last.Value;
                TriggerOnBlur(previousPanel);
                previousPanel.Hide();
            }

            // 压入新面板
            panel.Handler.OnStack = stack.AddLast(panel);
            panel.Handler.StackName = stackName;
            TriggerOnFocus(panel);
        }

        /// <summary>
        /// 从栈弹出面板
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <param name="showPrevious">是否显示前一个面板</param>
        /// <param name="autoClose">是否自动关闭弹出的面板</param>
        /// <returns>弹出的面板</returns>
        public IPanel PopFromStack(string stackName = DEFAULT_STACK, bool showPrevious = true, bool autoClose = true)
        {
            if (!mStacks.TryGetValue(stackName, out var stack) || stack.Count == 0)
            {
#if YOKIFRAME_ZSTRING_SUPPORT
                using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
                {
                    sb.Append("[UIRoot] Cannot pop from empty stack: ");
                    sb.Append(stackName);
                    KitLogger.Warning(sb.ToString());
                }
#else
                KitLogger.Warning("[UIRoot] Cannot pop from empty stack: " + stackName);
#endif
                return null;
            }

            var panel = stack.Last.Value;
            stack.RemoveLast();
            panel.Handler.OnStack = null;

            TriggerOnBlur(panel);

            if (showPrevious && stack.Count > 0)
            {
                var previousPanel = stack.Last.Value;
                previousPanel.Show();
                TriggerOnResume(previousPanel);
                TriggerOnFocus(previousPanel);
            }

            if (autoClose) panel.Close();
            return panel;
        }

        /// <summary>
        /// 查看栈顶面板
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <returns>栈顶面板，栈为空时返回 null</returns>
        public IPanel PeekStack(string stackName = DEFAULT_STACK)
        {
            if (!mStacks.TryGetValue(stackName, out var stack) || stack.Count == 0)
            {
                return null;
            }
            return stack.Last.Value;
        }

        /// <summary>
        /// 获取栈深度
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <returns>栈中面板数量</returns>
        public int GetStackDepth(string stackName = DEFAULT_STACK)
        {
            if (!mStacks.TryGetValue(stackName, out var stack)) return 0;
            return stack.Count;
        }

        /// <summary>
        /// /// 获取所有栈名称
        /// </summary>
        public IReadOnlyCollection<string> GetAllStackNames() => mStacks.Keys;

        /// <summary>
        /// 清空指定栈
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <param name="closeAll">是否关闭所有面板</param>
        public void ClearStack(string stackName = DEFAULT_STACK, bool closeAll = true)
        {
            if (!mStacks.TryGetValue(stackName, out var stack)) return;

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
                while (node != default)
                {
                    node.Value.Handler.OnStack = null;
                    node = node.Next;
                }
                stack.Clear();
            }
        }

        /// <summary>
        /// 从栈中移除指定面板
        /// </summary>
        /// <param name="panel">要移除的面板</param>
        public void RemoveFromStack(IPanel panel)
        {
            if (panel == default || panel.Handler == default || panel.Handler.OnStack == default) return;

            var stack = GetStackContaining(panel);
            if (stack == default) return;

            bool wasTop = stack.Last != default && stack.Last.Value == panel;
            stack.Remove(panel.Handler.OnStack);
            panel.Handler.OnStack = null;

            if (wasTop && stack.Count > 0)
            {
                TriggerOnFocus(stack.Last.Value);
            }
        }

        /// <summary>
        /// 检查面板是否在栈中
        /// </summary>
        /// <param name="panel">要检查的面板</param>
        /// <returns>是否在栈中</returns>
        public bool IsInStack(IPanel panel) => 
            panel != default && panel.Handler != default && panel.Handler.OnStack != default;

        /// <summary>
        /// 获取面板所在栈名称
        /// </summary>
        /// <param name="panel">面板</param>
        /// <returns>栈名称，面板不在栈中时返回 null</returns>
        public string GetPanelStackName(IPanel panel) => 
            panel != default && panel.Handler != default ? panel.Handler.StackName : null;

        /// <summary>
        /// 清空所有栈
        /// </summary>
        internal void ClearAllStacks()
        {
            foreach (var stack in mStacks.Values)
            {
                var node = stack.First;
                while (node != default)
                {
                    if (node.Value != default && node.Value.Handler != default)
                    {
                        node.Value.Handler.OnStack = null;
                    }
                    node = node.Next;
                }
                stack.Clear();
            }
            mStacks.Clear();
        }

        #endregion

        #region 内部方法

        private PooledLinkedList<IPanel> GetOrCreateStack(string stackName)
        {
            if (!mStacks.TryGetValue(stackName, out var stack))
            {
                stack = new PooledLinkedList<IPanel>();
                mStacks[stackName] = stack;
            }
            return stack;
        }

        private PooledLinkedList<IPanel> GetStackContaining(IPanel panel)
        {
            if (panel == default || panel.Handler == default || panel.Handler.StackName == default) return null;
            mStacks.TryGetValue(panel.Handler.StackName, out var stack);
            return stack;
        }

        private static void TriggerOnFocus(IPanel panel)
        {
            if (panel is UIPanel uiPanel) uiPanel.InvokeFocus();
        }

        private static void TriggerOnBlur(IPanel panel)
        {
            if (panel is UIPanel uiPanel) uiPanel.InvokeBlur();
        }

        private static void TriggerOnResume(IPanel panel)
        {
            if (panel is UIPanel uiPanel) uiPanel.InvokeResume();
        }

        #endregion

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// [UniTask] 异步弹出面板
        /// </summary>
        /// <param name="stackName">栈名称</param>
        /// <param name="showPrevious">是否显示前一个面板</param>
        /// <param name="autoClose">是否自动关闭弹出的面板</param>
        /// <param name="ct">取消令牌</param>
        /// <returns>弹出的面板</returns>
        public async UniTask<IPanel> PopFromStackUniTaskAsync(string stackName = DEFAULT_STACK,
            bool showPrevious = true, bool autoClose = true, CancellationToken ct = default)
        {
            if (!mStacks.TryGetValue(stackName, out var stack) || stack.Count == 0)
            {
#if YOKIFRAME_ZSTRING_SUPPORT
                using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
                {
                    sb.Append("[UIRoot] Cannot pop from empty stack: ");
                    sb.Append(stackName);
                    KitLogger.Warning(sb.ToString());
                }
#else
                KitLogger.Warning("[UIRoot] Cannot pop from empty stack: " + stackName);
#endif
                return null;
            }

            var panel = stack.Last.Value;
            stack.RemoveLast();
            panel.Handler.OnStack = null;

            TriggerOnBlur(panel);

            if (showPrevious && stack.Count > 0)
            {
                var previousPanel = stack.Last.Value;
                if (previousPanel is UIPanel uiPrevious)
                {
                    await uiPrevious.ShowUniTaskAsync(ct);
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
#endif
    }
}
