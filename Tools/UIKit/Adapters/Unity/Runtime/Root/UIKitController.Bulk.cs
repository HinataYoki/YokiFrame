#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        /// <summary>
        /// 隐藏当前全部可见面板，不影响预加载、缓存和栈 membership。
        /// </summary>
        internal void HideAll()
        {
            EnsureAvailable();
            PanelEntry[] entries = CopyEntries();
            for (var index = 0; index < entries.Length; index++)
            {
                if (entries[index].State == PanelState.Open) Hide(entries[index].Panel);
            }
        }

        /// <summary>
        /// 关闭全部逻辑打开轮次；纯预加载和已关闭保留项保持不变。
        /// </summary>
        internal void CloseAll()
        {
            EnsureAvailable();
            ClearAllStackMemberships();
            PanelEntry[] entries = CopyEntries();
            for (var index = 0; index < entries.Length; index++)
            {
                if (entries[index].IsLogicallyOpen && entries[index].State != PanelState.Closing)
                    Close(entries[index].Panel);
            }

            OnStateChanged();
        }

        /// <summary>
        /// 关闭全部匹配非空 Tag 的打开面板。
        /// </summary>
        internal int CloseByTag(string tag)
        {
            EnsureAvailable();
            if (string.IsNullOrEmpty(tag)) throw new ArgumentException("UIKit tag cannot be empty.", nameof(tag));
            PanelEntry[] entries = CopyEntries();
            var closed = 0;
            for (var index = 0; index < entries.Length; index++)
            {
                PanelEntry entry = entries[index];
                if (entry.IsLogicallyOpen && string.Equals(entry.Tag, tag, StringComparison.Ordinal)
                    && Close(entry.Panel)) closed++;
            }

            return closed;
        }

        /// <summary>
        /// 复制当前 entries，允许批量操作过程中安全删除字典项。
        /// </summary>
        private PanelEntry[] CopyEntries()
        {
            var result = new PanelEntry[mEntries.Count];
            mEntries.Values.CopyTo(result, 0);
            return result;
        }

        /// <summary>
        /// 批量摘除所有栈 membership，不在 CloseAll 中逐层恢复中间栈顶。
        /// </summary>
        private void ClearAllStackMemberships()
        {
            var stacks = new List<KeyValuePair<string, LinkedList<PanelEntry>>>(mStacks);
            for (var index = 0; index < stacks.Count; index++)
            {
                LinkedList<PanelEntry> stack = stacks[index].Value;
                if (stack.Last != null) InvokeBlur(stack.Last.Value);
                if (!mStacks.TryGetValue(stacks[index].Key, out stack)) continue;
                ClearMembership(stacks[index].Key, stack);
            }
        }
    }
}
#endif
