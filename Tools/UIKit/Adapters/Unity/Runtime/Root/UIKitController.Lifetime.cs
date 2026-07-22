#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        /// <summary>
        /// 接收受管 UIPanel 的外部 Unity 销毁通知，并释放残留 lease 和索引。
        /// </summary>
        internal void NotifyPanelDestroyed(PanelEntry entry)
        {
            if (entry == null || entry.IsDisposing) return;
            if (entry.Panel != default) entry.Panel.InvokeBeforeDestroy();
            ReleaseDestroyedEntry(entry);
        }

        /// <summary>
        /// 每帧无分配扫描被外部单独销毁的 Panel 组件，补齐 sentinel 无法观察的清理路径。
        /// </summary>
        internal void SweepDestroyedEntries()
        {
            if (mDisposed) return;
            while (TryFindDestroyedEntry(out PanelEntry destroyed)) ReleaseDestroyedEntry(destroyed);
        }

        /// <summary>
        /// 查找一个 Unity fake-null Panel；单次只返回一项以便在枚举结束后安全修改字典。
        /// </summary>
        private bool TryFindDestroyedEntry(out PanelEntry destroyed)
        {
            foreach (PanelEntry entry in mEntries.Values)
            {
                if (entry.Panel == default)
                {
                    destroyed = entry;
                    return true;
                }
            }

            destroyed = null;
            return false;
        }

        /// <summary>
        /// 主动销毁 entry、Panel 和 Prefab lease；方法可重复调用。
        /// </summary>
        private void DisposeEntry(PanelEntry entry)
        {
            if (entry == null || entry.IsDisposing) return;
            entry.IsDisposing = true;
            RemoveEntryIndexes(entry, !mDisposed);
            UIPanel panel = entry.Panel;
            entry.State = PanelState.Close;
            if (panel != default)
            {
                mRoot.OnPanelClosed(panel);
                if (entry.LifetimeSentinel != default) entry.LifetimeSentinel.Detach();
                panel.InvokeBeforeDestroy();
                panel.DetachOwner();
                DestroyObject(panel.gameObject);
            }

            entry.Panel = null;
            entry.LifetimeSentinel = null;
            ReleaseLease(entry);
            OnStateChanged();
        }

        /// <summary>
        /// 清理由 Unity 外部销毁造成的无效 Panel 引用，并恢复原栈新顶。
        /// </summary>
        private void ReleaseDestroyedEntry(PanelEntry entry)
        {
            if (entry == null || entry.IsDisposing) return;
            entry.IsDisposing = true;
            UIPanel panel = entry.Panel;
            // 组件被外部单独 Destroy 后仅 Unity 比较为 null，托管对象仍可用于幂等提交销毁前钩子。
            if (!ReferenceEquals(panel, null))
            {
                panel.InvokeBeforeDestroy();
                panel.DetachOwner();
            }
            GameObject orphanedInstance = null;
            if (entry.Panel == default && entry.LifetimeSentinel != default)
            {
                entry.LifetimeSentinel.Detach();
                orphanedInstance = entry.LifetimeSentinel.gameObject;
            }
            RemoveDestroyedStackEntry(entry);
            RemoveEntryIndexes(entry, false);
            entry.Panel = null;
            entry.LifetimeSentinel = null;
            entry.State = PanelState.Close;
            ReleaseLease(entry);
            if (orphanedInstance != default) DestroyObject(orphanedInstance);
            OnStateChanged();
        }

        /// <summary>
        /// 从所有 Controller 索引移除 entry，并按需恢复它所在栈的新顶。
        /// </summary>
        private void RemoveEntryIndexes(PanelEntry entry, bool restoreStack)
        {
            if (entry.StackNode != null) DetachFromStack(entry, restoreStack);
            RemoveReusableEntry(entry);
            RemoveModalBlocker(entry);
            UnregisterFromLevel(entry);
            if (mEntries.TryGetValue(entry.PanelType, out PanelEntry current)
                && ReferenceEquals(current, entry)) mEntries.Remove(entry.PanelType);
        }

        /// <summary>
        /// 在 Panel 已进入 fake-null 时移除栈节点，避免再次调用其生命周期。
        /// </summary>
        private void RemoveDestroyedStackEntry(PanelEntry entry)
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
            ClearEntryMembership(entry);
            if (wasTop && !mDisposed && stack.Count > 0) RestoreStackTop(stack.Last.Value);
            if (stack.Count == 0) mStacks.Remove(stackName);
        }

        /// <summary>
        /// 幂等释放 entry 独占的 Prefab lease，并记录自定义 loader 释放异常。
        /// </summary>
        private static void ReleaseLease(PanelEntry entry)
        {
            IPanelPrefabLease lease = entry.PrefabLease;
            entry.PrefabLease = null;
            if (lease == null) return;
            try
            {
                lease.Dispose();
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
            }
        }
    }
}
#endif
