#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;

namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        /// <summary>
        /// 同步物化并提交一次 Open 请求；重复 Open 只重放 OnOpen，不重放 Show。
        /// </summary>
        internal UIPanel Open(
            Type panelType,
            UILevel level,
            IUIData data,
            string tag,
            PanelCachePolicy policy)
        {
            PanelEntry entry = GetOrCreate(panelType, level, policy, data);
            return OpenEntry(entry, level, data, tag, policy);
        }

        /// <summary>
        /// 异步加入物化 single-flight 后提交当前调用方自己的 Open 请求。
        /// </summary>
        internal async Task<UIPanel> OpenAsync(
            Type panelType,
            UILevel level,
            IUIData data,
            string tag,
            PanelCachePolicy policy,
            CancellationToken token)
        {
            PanelEntry entry = await GetOrCreateAsync(panelType, level, policy, data, token);
            token.ThrowIfCancellationRequested();
            return OpenEntry(entry, level, data, tag, policy);
        }

        /// <summary>
        /// 获取任意已物化面板，不创建 Root、不改变预加载或 LRU 状态。
        /// </summary>
        internal UIPanel GetPanel(Type panelType)
        {
            EnsureAvailable();
            ValidatePanelType(panelType);
            return TryGetLiveEntry(panelType, out PanelEntry entry) ? entry.Panel : null;
        }

        /// <summary>
        /// 提交 Open 数据和元信息，并在需要时完成一次显示转换。
        /// </summary>
        private UIPanel OpenEntry(
            PanelEntry entry,
            UILevel level,
            IUIData data,
            string tag,
            PanelCachePolicy policy)
        {
            EnsureAvailable();
            if (entry.State == PanelState.Closing || entry.State == PanelState.Close)
                throw new InvalidOperationException("Cannot open a panel while its close transition is running.");
            bool alreadyVisible = entry.State == PanelState.Open;
            RemoveReusableEntry(entry);
            entry.Panel.StopAnimations();
            int generation = ++entry.TransitionGeneration;
            ApplyOpenMetadata(entry, level, data, tag, policy);
            if (!alreadyVisible) entry.State = PanelState.Opening;
            entry.Panel.InvokeOpen(data);
            if (!IsTransitionCurrent(entry, generation)) return entry.Panel;
            if (alreadyVisible)
            {
                entry.State = PanelState.Open;
                SortLevel(entry.Level);
                OnStateChanged();
                return entry.Panel;
            }
            RegisterAtLevel(entry);
            return CommitShow(entry, generation);
        }

        /// <summary>
        /// 原子更新当前 Open 请求的只读公开元信息。
        /// </summary>
        private void ApplyOpenMetadata(
            PanelEntry entry,
            UILevel level,
            IUIData data,
            string tag,
            PanelCachePolicy policy)
        {
            entry.Data = data;
            entry.Tag = tag;
            entry.CachePolicy = policy;
            entry.HasOpened = true;
            entry.OpenSequence = ++mOpenSequence;
            if (entry.Level != level) ChangeEntryLevel(entry, level, entry.SubLevel);
        }

        /// <summary>
        /// 显示一个已打开但隐藏的面板；预加载和关闭保留项必须先 Open。
        /// </summary>
        internal bool Show(IPanel panel)
        {
            EnsureAvailable();
            if (!TryGetOwnedEntry(panel, out PanelEntry entry)
                || (entry.State != PanelState.Hide && entry.State != PanelState.Hiding)) return false;
            int generation = ++entry.TransitionGeneration;
            entry.Panel.StopAnimations();
            entry.State = PanelState.Opening;
            CommitShow(entry, generation);
            return IsTransitionCurrent(entry, generation);
        }

        /// <summary>
        /// 提交同步显示钩子和 active 状态，并拒绝重入产生的旧 generation。
        /// </summary>
        private UIPanel CommitShow(PanelEntry entry, int generation)
        {
            if (!IsTransitionCurrent(entry, generation)) return entry.Panel;
            entry.Panel.gameObject.SetActive(true);
            entry.Panel.InvokeWillShow();
            if (!IsTransitionCurrent(entry, generation)) return entry.Panel;
            entry.Panel.InvokeShow();
            if (!IsTransitionCurrent(entry, generation)) return entry.Panel;
            if (entry.Panel.TryPlayShowAnimation(() => CompleteShow(entry, generation))) return entry.Panel;
            CompleteShow(entry, generation);
            return entry.Panel;
        }

        /// <summary>提交显示动画完成后的 Open 终态。</summary>
        private void CompleteShow(PanelEntry entry, int generation)
        {
            if (!IsTransitionCurrent(entry, generation) || entry.State != PanelState.Opening) return;
            entry.State = PanelState.Open;
            EnsureModalBlocker(entry);
            SortLevel(entry.Level);
            entry.Panel.InvokeDidShow();
            mRoot.OnPanelShown(entry.Panel);
            OnStateChanged();
        }

        /// <summary>
        /// 隐藏一个当前可见面板，保留其打开轮次、层级和栈归属。
        /// </summary>
        internal bool Hide(IPanel panel)
        {
            EnsureAvailable();
            if (!TryGetOwnedEntry(panel, out PanelEntry entry)
                || (entry.State != PanelState.Open && entry.State != PanelState.Opening)) return false;
            int generation = ++entry.TransitionGeneration;
            entry.Panel.StopAnimations();
            entry.State = PanelState.Hiding;
            entry.Panel.InvokeWillHide();
            if (!IsTransitionCurrent(entry, generation)) return false;
            entry.Panel.InvokeHide();
            if (!IsTransitionCurrent(entry, generation)) return false;
            if (entry.Panel.TryPlayHideAnimation(() => CompleteHide(entry, generation))) return true;
            CompleteHide(entry, generation);
            return true;
        }

        /// <summary>提交隐藏动画完成后的 inactive 终态。</summary>
        private void CompleteHide(PanelEntry entry, int generation)
        {
            if (!IsTransitionCurrent(entry, generation) || entry.State != PanelState.Hiding) return;
            mRoot.OnPanelHidden(entry.Panel);
            entry.Panel.gameObject.SetActive(false);
            entry.State = PanelState.Hide;
            RemoveModalBlocker(entry);
            entry.Panel.InvokeDidHide();
            OnStateChanged();
        }

        /// <summary>
        /// 关闭一个面板打开轮次，并按显式策略销毁或保留实例。
        /// </summary>
        internal bool Close(IPanel panel)
        {
            EnsureAvailable();
            if (!TryGetOwnedEntry(panel, out PanelEntry entry)
                || !entry.IsLogicallyOpen
                || entry.State == PanelState.Closing) return false;
            bool hideLifecycleStarted = entry.State == PanelState.Hiding;
            ++entry.TransitionGeneration;
            entry.Panel.StopAnimations();
            entry.State = PanelState.Closing;
            try
            {
                mRoot.OnPanelClosed(entry.Panel);
                DetachFromStack(entry, true);
                RemoveModalBlocker(entry);
                UnregisterFromLevel(entry);
                InvokeCloseLifecycle(entry, hideLifecycleStarted);
            }
            finally
            {
                FinalizeClose(entry);
            }

            return true;
        }

        /// <summary>
        /// 在关闭阶段按当前 active 状态补齐隐藏钩子，再调用 OnClose。
        /// </summary>
        private static void InvokeCloseLifecycle(PanelEntry entry, bool hideLifecycleStarted)
        {
            UIPanel panel = entry.Panel;
            if (panel == default) return;
            if (panel.gameObject.activeSelf)
            {
                if (!hideLifecycleStarted)
                {
                    panel.InvokeWillHide();
                    panel.InvokeHide();
                }
                panel.gameObject.SetActive(false);
                panel.InvokeDidHide();
            }

            panel.InvokeClose();
        }

        /// <summary>
        /// 无论用户钩子结果如何都提交 Close 终态和资源策略。
        /// </summary>
        private void FinalizeClose(PanelEntry entry)
        {
            Action[] closedCallbacks = entry.Panel == default
                ? Array.Empty<Action>()
                : entry.Panel.TakeClosedCallbacks();
            entry.Data = null;
            entry.Tag = null;
            entry.IsModal = false;
            if (entry.CachePolicy == PanelCachePolicy.Transient)
            {
                DisposeEntry(entry);
                UIPanel.InvokeClosedCallbacks(closedCallbacks);
                return;
            }

            MoveToStorage(entry);
            entry.State = PanelState.Cached;
            if (entry.State == PanelState.Cached && entry.CachePolicy == PanelCachePolicy.Reusable)
                AddReusableEntry(entry);
            OnStateChanged();
            UIPanel.InvokeClosedCallbacks(closedCallbacks);
        }

        /// <summary>
        /// 判断 entry、Panel 与 generation 仍属于当前转换。
        /// </summary>
        private bool IsTransitionCurrent(PanelEntry entry, int generation)
        {
            return entry != null
                && entry.TransitionGeneration == generation
                && entry.Panel != default
                && mEntries.TryGetValue(entry.PanelType, out PanelEntry current)
                && ReferenceEquals(current, entry);
        }

        /// <summary>
        /// 验证公开 Panel 确实由当前控制器拥有。
        /// </summary>
        private bool TryGetOwnedEntry(IPanel panel, out PanelEntry entry)
        {
            entry = null;
            if (!(panel is UIPanel uiPanel) || uiPanel == default) return false;
            Type panelType = uiPanel.GetType();
            return mEntries.TryGetValue(panelType, out entry)
                && ReferenceEquals(entry.Panel, uiPanel);
        }
    }
}
#endif
