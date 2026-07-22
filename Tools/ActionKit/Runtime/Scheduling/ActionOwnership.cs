using System;
using System.Runtime.CompilerServices;

namespace YokiFrame
{
    /// <summary>
    /// 维护动作树单父级和单活动根约束；内置 Action 把状态保存在实例上，避免静态表强引用废弃树。
    /// </summary>
    internal static class ActionOwnership
    {
        private static readonly object sSyncRoot = new();
        private static readonly ConditionalWeakTable<IAction, ExternalOwnership> sExternalOwnership = new();

        /// <summary>
        /// 把子 Action 交给唯一父容器，并拒绝运行中修改、重复所有权和直接或间接环。
        /// </summary>
        /// <param name="parent">目标父容器。</param>
        /// <param name="child">待追加子 Action。</param>
        internal static void ClaimChild(IAction parent, IAction child)
        {
            if (parent == null) throw new ArgumentNullException(nameof(parent));
            if (child == null) throw new ArgumentNullException(nameof(child));
            if (parent.Deinited || parent.ActionState != ActionStatus.NotStart)
                throw new InvalidOperationException("An Action container can only be configured before it starts.");
            if (child.Deinited || child.ActionState != ActionStatus.NotStart)
                throw new InvalidOperationException("Only a fresh inactive Action can be appended.");
            if (ReferenceEquals(parent, child))
                throw new InvalidOperationException("An Action container cannot append itself.");

            lock (sSyncRoot)
            {
                if (IsTreeActive(parent))
                    throw new InvalidOperationException("An active Action tree cannot be modified.");
                if (HasOwnership(child))
                    throw new InvalidOperationException("An Action can have only one parent or active controller.");

                ValidateAppendDepth(parent, child);

                SetParent(child, parent);
            }
        }

        /// <summary>把无父级且未释放的 Action 标记为活动根；重复 Start 会被明确拒绝。</summary>
        /// <param name="action">待启动根 Action。</param>
        internal static void ClaimRoot(IAction action)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            if (action.Deinited) throw new InvalidOperationException("A deinited Action lease cannot be started again.");
            lock (sSyncRoot)
            {
                if (HasOwnership(action))
                    throw new InvalidOperationException("Only an unowned inactive Action can be started.");
                SetActiveRoot(action);
            }
        }

        /// <summary>拒绝外部手动推进已经属于父容器或活动 controller 的 Action。</summary>
        /// <param name="action">待手动推进 Action。</param>
        internal static void EnsureCanManuallyUpdate(IAction action)
        {
            lock (sSyncRoot)
            {
                if (HasOwnership(action))
                    throw new InvalidOperationException("An owned Action can only be advanced by its scheduler or parent container.");
            }
        }

        /// <summary>获取指定 Action 当前是否由活动 controller 持有。</summary>
        /// <param name="action">待检查 Action。</param>
        /// <returns>Action 是活动根时返回 true。</returns>
        internal static bool IsActiveRoot(IAction action)
        {
            lock (sSyncRoot) return GetIsActiveRoot(action);
        }

        /// <summary>释放一个 Action 的父级或活动根身份，使池化实例可以进入新租约。</summary>
        /// <param name="action">已经完成 OnDeinit 的 Action。</param>
        internal static void Release(IAction action)
        {
            if (action == null) return;
            lock (sSyncRoot)
            {
                if (action is ActionBase actionBase) actionBase.ReleaseOwnership();
                else sExternalOwnership.Remove(action);
            }
        }

        /// <summary>沿父链检查任一祖先是否已经是活动根。</summary>
        private static bool IsTreeActive(IAction action)
        {
            IAction current = action;
            for (var depth = 0; current != null; depth++)
            {
                if (depth >= ActionTreeLimits.MAX_DEPTH)
                    throw new InvalidOperationException("Action ownership chain exceeds the supported depth.");
                if (GetIsActiveRoot(current)) return true;
                current = GetParent(current);
            }

            return false;
        }

        /// <summary>校验追加后的绝对深度，并在遍历候选子树时拒绝容器环。</summary>
        private static void ValidateAppendDepth(IAction parent, IAction child)
        {
            int parentDepth = GetAncestorDepth(parent);
            int childDepth = GetCandidateDepth(child, parent, 0);
            if (parentDepth + 1 + childDepth >= ActionTreeLimits.MAX_DEPTH)
                throw new InvalidOperationException("Appending this Action exceeds the supported tree depth.");
        }

        /// <summary>计算父容器距当前未启动树根的边数。</summary>
        private static int GetAncestorDepth(IAction action)
        {
            var depth = 0;
            IAction current = GetParent(action);
            while (current != null)
            {
                depth++;
                if (depth >= ActionTreeLimits.MAX_DEPTH)
                    throw new InvalidOperationException("Action ownership chain exceeds the supported depth.");
                current = GetParent(current);
            }

            return depth;
        }

        /// <summary>计算候选子树最大相对深度，并拒绝间接包含目标父容器。</summary>
        private static int GetCandidateDepth(IAction candidate, IAction target, int depth)
        {
            if (ReferenceEquals(candidate, target))
                throw new InvalidOperationException("Appending this Action would create a container cycle.");
            if (depth >= ActionTreeLimits.MAX_DEPTH)
                throw new InvalidOperationException("Action tree exceeds the supported ownership depth.");
            if (!(candidate is IActionContainerInternal container)) return depth;

            int maxDepth = depth;
            for (var index = 0; index < container.ChildCount; index++)
                maxDepth = Math.Max(
                    maxDepth,
                    GetCandidateDepth(container.GetChild(index), target, depth + 1));
            return maxDepth;
        }

        /// <summary>读取 Action 是否已经拥有父级或活动根身份。</summary>
        private static bool HasOwnership(IAction action)
        {
            if (action is ActionBase actionBase) return actionBase.HasOwnership;
            return sExternalOwnership.TryGetValue(action, out _);
        }

        /// <summary>读取 Action 的直接父级。</summary>
        private static IAction GetParent(IAction action)
        {
            if (action is ActionBase actionBase) return actionBase.ParentAction;
            return sExternalOwnership.TryGetValue(action, out ExternalOwnership state) ? state.Parent : null;
        }

        /// <summary>读取 Action 是否是活动根。</summary>
        private static bool GetIsActiveRoot(IAction action)
        {
            if (action is ActionBase actionBase) return actionBase.IsActiveRoot;
            return sExternalOwnership.TryGetValue(action, out ExternalOwnership state) && state.ActiveRoot;
        }

        /// <summary>为 Action 写入直接父级；非 ActionBase 使用弱键状态避免静态强引用泄漏。</summary>
        private static void SetParent(IAction action, IAction parent)
        {
            if (action is ActionBase actionBase) actionBase.ClaimParent(parent);
            else sExternalOwnership.Add(action, new ExternalOwnership { Parent = parent });
        }

        /// <summary>为 Action 写入活动根身份。</summary>
        private static void SetActiveRoot(IAction action)
        {
            if (action is ActionBase actionBase) actionBase.ClaimActiveRoot();
            else sExternalOwnership.Add(action, new ExternalOwnership { ActiveRoot = true });
        }

        /// <summary>保存未继承 ActionBase 的自定义 Action 所有权状态。</summary>
        private sealed class ExternalOwnership
        {
            internal IAction Parent;
            internal bool ActiveRoot;
        }
    }
}
