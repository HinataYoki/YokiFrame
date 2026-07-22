#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        private const int MAX_STACK_NAME_LENGTH = 128;
        private readonly Dictionary<string, LinkedList<PanelEntry>> mStacks =
            new(StringComparer.Ordinal);

        /// <summary>
        /// 把已打开或隐藏的受管面板压入命名栈，并按需隐藏旧栈顶。
        /// </summary>
        internal bool Push(IPanel panel, string stackName, bool hidePrevious)
        {
            EnsureAvailable();
            ValidateStackName(stackName);
            if (!TryGetOwnedEntry(panel, out PanelEntry entry) || !CanPush(entry)) return false;
            if (string.Equals(entry.StackName, stackName, StringComparison.Ordinal)
                && entry.StackNode != null
                && entry.StackNode.List != null
                && ReferenceEquals(entry.StackNode.List.Last, entry.StackNode)) return true;
            if (entry.StackNode != null) DetachFromStack(entry, true);
            LinkedList<PanelEntry> stack = GetOrCreateStack(stackName);
            BlurPreviousTop(stackName, stack, hidePrevious);
            if (!TryGetOwnedEntry(panel, out entry) || !CanPush(entry)) return false;
            if (entry.StackNode != null)
            {
                if (string.Equals(entry.StackName, stackName, StringComparison.Ordinal)
                    && entry.StackNode.List != null
                    && ReferenceEquals(entry.StackNode.List.Last, entry.StackNode)) return true;
                DetachFromStack(entry, true);
                if (!TryGetOwnedEntry(panel, out entry) || !CanPush(entry)) return false;
            }
            stack = GetOrCreateStack(stackName);
            entry.StackNode = stack.AddLast(entry);
            entry.StackName = stackName;
            if (entry.State == PanelState.Hide) Show(entry.Panel);
            entry.Panel.InvokeFocus();
            OnStateChanged();
            return true;
        }

        /// <summary>
        /// 弹出指定命名栈顶部，并按参数恢复旧栈顶和关闭弹出项。
        /// </summary>
        internal IPanel Pop(string stackName, bool showPrevious, bool autoClose)
        {
            EnsureAvailable();
            ValidateStackName(stackName);
            if (!mStacks.TryGetValue(stackName, out LinkedList<PanelEntry> stack) || stack.Count == 0)
                return null;
            PanelEntry entry = stack.Last.Value;
            IPanel panel = entry.Panel;
            DetachFromStack(entry, showPrevious);
            if (autoClose) Close(panel);
            OnStateChanged();
            return panel;
        }

        /// <summary>
        /// 以 Task 形态返回同步栈操作，保留 Task/UniTask 公共入口的一致取消语义。
        /// </summary>
        internal Task<IPanel> PopAsync(
            string stackName,
            bool showPrevious,
            bool autoClose,
            CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(Pop(stackName, showPrevious, autoClose));
        }

        /// <summary>
        /// 读取命名栈顶部，不改变焦点或可见性。
        /// </summary>
        internal IPanel Peek(string stackName)
        {
            EnsureAvailable();
            ValidateStackName(stackName);
            return mStacks.TryGetValue(stackName, out LinkedList<PanelEntry> stack) && stack.Count > 0
                ? stack.Last.Value.Panel
                : null;
        }

        /// <summary>
        /// 获取命名栈深度。
        /// </summary>
        internal int GetStackDepth(string stackName)
        {
            EnsureAvailable();
            ValidateStackName(stackName);
            return mStacks.TryGetValue(stackName, out LinkedList<PanelEntry> stack) ? stack.Count : 0;
        }

        /// <summary>
        /// 复制当前非空栈名称并按 ordinal 稳定排序。
        /// </summary>
        internal IReadOnlyCollection<string> GetStackNames()
        {
            EnsureAvailable();
            var result = new List<string>(mStacks.Keys);
            result.Sort(StringComparer.Ordinal);
            return result;
        }

        /// <summary>
        /// 判断受管面板是否属于任意命名栈。
        /// </summary>
        internal bool IsInStack(IPanel panel)
        {
            EnsureAvailable();
            return TryGetOwnedEntry(panel, out PanelEntry entry) && entry.StackNode != null;
        }

        /// <summary>
        /// 获取面板栈名；未入栈时必须返回 null。
        /// </summary>
        internal string GetStackName(IPanel panel)
        {
            EnsureAvailable();
            return TryGetOwnedEntry(panel, out PanelEntry entry) && entry.StackNode != null
                ? entry.StackName
                : null;
        }

        /// <summary>
        /// 原子清空命名栈；批量关闭前先摘除全部 membership，避免逐项恢复中间栈顶。
        /// </summary>
        internal void ClearStack(string stackName, bool closeAll)
        {
            EnsureAvailable();
            ValidateStackName(stackName);
            if (!mStacks.TryGetValue(stackName, out LinkedList<PanelEntry> stack)) return;
            if (stack.Last != null) InvokeBlur(stack.Last.Value);
            if (!mStacks.TryGetValue(stackName, out stack)) return;
            PanelEntry[] entries = CopyStack(stack);
            ClearMembership(stackName, stack);
            if (closeAll)
            {
                for (var index = entries.Length - 1; index >= 0; index--) Close(entries[index].Panel);
            }

            OnStateChanged();
        }

        /// <summary>
        /// 从当前栈移除 entry；若它原为栈顶则按 Show、Resume、Focus 恢复新顶。
        /// </summary>
        private void DetachFromStack(PanelEntry entry, bool restorePrevious)
        {
            if (entry.StackNode == null || entry.StackName == null) return;
            string stackName = entry.StackName;
            if (!mStacks.TryGetValue(stackName, out LinkedList<PanelEntry> stack))
            {
                ClearEntryMembership(entry);
                return;
            }

            bool wasTop = ReferenceEquals(stack.Last, entry.StackNode);
            stack.Remove(entry.StackNode);
            PanelEntry previousTop = wasTop && stack.Count > 0 ? stack.Last.Value : null;
            ClearEntryMembership(entry);
            if (wasTop) InvokeBlur(entry);
            if (!mStacks.TryGetValue(stackName, out stack)) return;
            if (wasTop && restorePrevious && previousTop != null
                && stack.Last != null
                && ReferenceEquals(stack.Last.Value, previousTop)) RestoreStackTop(previousTop);
            if (stack.Count == 0) mStacks.Remove(stackName);
        }

        /// <summary>
        /// 恢复栈顶的可见性和焦点生命周期。
        /// </summary>
        private void RestoreStackTop(PanelEntry entry)
        {
            if (entry.State == PanelState.Hide) Show(entry.Panel);
            entry.Panel.InvokeResume();
            entry.Panel.InvokeFocus();
        }

        /// <summary>
        /// 处理压栈前的旧栈顶 Blur 和可选 Hide。
        /// </summary>
        private void BlurPreviousTop(
            string stackName,
            LinkedList<PanelEntry> stack,
            bool hidePrevious)
        {
            if (stack.Count == 0) return;
            PanelEntry previous = stack.Last.Value;
            InvokeBlur(previous);
            if (!mStacks.TryGetValue(stackName, out LinkedList<PanelEntry> current)
                || current.Count == 0
                || !ReferenceEquals(current.Last.Value, previous)) return;
            if (hidePrevious && previous.State == PanelState.Open) Hide(previous.Panel);
        }

        /// <summary>
        /// 调用一次 Blur 并屏蔽同一 entry 在生命周期重入中的重复 Blur。
        /// </summary>
        private static void InvokeBlur(PanelEntry entry)
        {
            if (entry == null || entry.IsBlurInProgress || entry.Panel == default) return;
            entry.IsBlurInProgress = true;
            try
            {
                entry.Panel.InvokeBlur();
            }
            finally
            {
                entry.IsBlurInProgress = false;
            }
        }

        /// <summary>
        /// 创建或读取指定命名栈。
        /// </summary>
        private LinkedList<PanelEntry> GetOrCreateStack(string stackName)
        {
            if (mStacks.TryGetValue(stackName, out LinkedList<PanelEntry> stack)) return stack;
            stack = new LinkedList<PanelEntry>();
            mStacks.Add(stackName, stack);
            return stack;
        }

        /// <summary>
        /// 判断 entry 状态是否允许加入导航栈。
        /// </summary>
        private static bool CanPush(PanelEntry entry)
        {
            return entry.State == PanelState.Open || entry.State == PanelState.Hide;
        }

        /// <summary>
        /// 校验命名栈标识，拒绝空白和超长名称。
        /// </summary>
        private static void ValidateStackName(string stackName)
        {
            if (string.IsNullOrWhiteSpace(stackName) || stackName.Length > MAX_STACK_NAME_LENGTH)
                throw new ArgumentException("UIKit stack name must contain 1-128 characters.", nameof(stackName));
        }

        /// <summary>
        /// 复制栈内容，供清空后继续执行批量操作。
        /// </summary>
        private static PanelEntry[] CopyStack(LinkedList<PanelEntry> stack)
        {
            var result = new PanelEntry[stack.Count];
            stack.CopyTo(result, 0);
            return result;
        }

        /// <summary>
        /// 清空一个栈的全部 membership，并移除空栈字典项。
        /// </summary>
        private void ClearMembership(string stackName, LinkedList<PanelEntry> stack)
        {
            LinkedListNode<PanelEntry> node = stack.First;
            while (node != null)
            {
                ClearEntryMembership(node.Value);
                node = node.Next;
            }

            stack.Clear();
            mStacks.Remove(stackName);
        }

        /// <summary>
        /// 清除 entry 的节点和栈名，使未入栈查询稳定返回 null。
        /// </summary>
        private static void ClearEntryMembership(PanelEntry entry)
        {
            entry.StackNode = null;
            entry.StackName = null;
        }
    }
}
#endif
