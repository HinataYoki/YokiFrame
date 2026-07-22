#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>保存一次 UIKit 只读查询得到的稳定事实快照。</summary>
    internal sealed class UIKitInteractionSnapshot
    {
        internal UIKitRootSnapshot Root = new();
        internal UIKitStatsSnapshot Stats = new();
        internal UIKitCacheSnapshot Cache = new();
        internal UIKitModalSnapshot Modal = new();
        internal List<UIKitPanelSnapshot> Panels = new();
        internal List<UIKitStackSnapshot> Stacks = new();
    }

    /// <summary>描述现有 UIRoot 的非标识性状态。</summary>
    internal sealed class UIKitRootSnapshot
    {
        internal bool Exists;
    }

    /// <summary>汇总 UIKit 面板、栈和生命周期状态数量。</summary>
    internal sealed class UIKitStatsSnapshot
    {
        internal int PanelCount;
        internal int StackCount;
        internal int StackMembershipCount;
        internal int PreloadedCount;
        internal int OpeningCount;
        internal int OpenCount;
        internal int HidingCount;
        internal int HiddenCount;
        internal int ClosingCount;
        internal int CachedCount;
        internal int ClosedCount;
    }

    /// <summary>汇总当前加载实例采用的缓存策略。</summary>
    internal sealed class UIKitCacheSnapshot
    {
        internal int Capacity;
        internal int TransientCount;
        internal int ReusableCount;
        internal int ReusableCachedCount;
        internal int PersistentCount;
    }

    /// <summary>汇总当前模态面板与 blocker 状态。</summary>
    internal sealed class UIKitModalSnapshot
    {
        internal bool BlockerActive;
        internal int PanelCount;
    }

    /// <summary>描述一个面板的公开只读状态，不包含业务 Data 或 Unity 标识。</summary>
    internal sealed class UIKitPanelSnapshot
    {
        internal string Type;
        internal string Name;
        internal string State;
        internal string Level;
        internal int LevelOrder;
        internal int SubLevel;
        internal string CachePolicy;
        internal bool IsModal;
        internal string StackName;
    }

    /// <summary>描述一个命名栈的深度和顶部面板。</summary>
    internal sealed class UIKitStackSnapshot
    {
        internal string Name;
        internal int Depth;
        internal string TopPanelType;
        internal string TopPanelName;
    }
}
#endif
