using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 层级 — 可扩展的值类型
    /// <para>框架预定义 7 个层级，用户可通过 <c>new UILevel(order)</c> 自定义层级。</para>
    /// <para>Common = 0，因此 <c>default(UILevel)</c> 等价于 <c>UILevel.Common</c>。</para>
    /// </summary>
    [Serializable]
    public readonly struct UILevel : IEquatable<UILevel>, IComparable<UILevel>
    {
        #region 序列化字段

        [SerializeField] private readonly int mOrder;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建自定义层级
        /// </summary>
        /// <param name="order">排序值，越小越靠底层</param>
        public UILevel(int order) => mOrder = order;

        #endregion

        #region 预定义层级

        /// <summary>最底层</summary>
        public static readonly UILevel AlwayBottom = new(-200);

        /// <summary>背景层</summary>
        public static readonly UILevel Bg = new(-100);

        /// <summary>HUD 层 — 血条、名牌、伤害飘字等世界跟踪 UI</summary>
        public static readonly UILevel Hud = new(-50);

        /// <summary>默认层（等于 default(UILevel)）</summary>
        public static readonly UILevel Common = new(0);

        /// <summary>轻提示层 — Toast 消息、成就通知、系统广播</summary>
        public static readonly UILevel Toast = new(50);

        /// <summary>弹窗层</summary>
        public static readonly UILevel Pop = new(100);

        /// <summary>引导层 — 新手引导、教程遮罩、高亮提示</summary>
        public static readonly UILevel Guide = new(150);

        /// <summary>最顶层</summary>
        public static readonly UILevel AlwayTop = new(200);

        /// <summary>独立 Canvas 面板层</summary>
        public static readonly UILevel CanvasPanel = new(300);

        #endregion

        #region PredefinedLevels

        /// <summary>
        /// 预定义层级与名称的映射（按 Order 升序）
        /// </summary>
        private static readonly (UILevel level, string name)[] sPredefinedEntries =
        {
            (AlwayBottom, nameof(AlwayBottom)),
            (Bg, nameof(Bg)),
            (Hud, nameof(Hud)),
            (Common, nameof(Common)),
            (Toast, nameof(Toast)),
            (Pop, nameof(Pop)),
            (Guide, nameof(Guide)),
            (AlwayTop, nameof(AlwayTop)),
            (CanvasPanel, nameof(CanvasPanel)),
        };

        /// <summary>
        /// 预定义层级集合（只读，按 Order 升序排列）
        /// </summary>
        private static readonly UILevel[] sPredefinedLevels = BuildPredefinedLevels();

        /// <summary>
        /// 所有预定义层级，按 Order 升序排列
        /// </summary>
        public static IReadOnlyList<UILevel> PredefinedLevels => sPredefinedLevels;

        private static UILevel[] BuildPredefinedLevels()
        {
            var levels = new UILevel[sPredefinedEntries.Length];
            for (int i = 0; i < sPredefinedEntries.Length; i++)
            {
                levels[i] = sPredefinedEntries[i].level;
            }
            return levels;
        }

        /// <summary>
        /// 所有预定义层级名称，按 Order 升序排列
        /// </summary>
        public static IReadOnlyList<string> PredefinedLevelNames => sPredefinedLevelNames;

        private static readonly string[] sPredefinedLevelNames = BuildPredefinedLevelNames();

        private static string[] BuildPredefinedLevelNames()
        {
            var names = new string[sPredefinedEntries.Length];
            for (int i = 0; i < sPredefinedEntries.Length; i++)
            {
                names[i] = sPredefinedEntries[i].name;
            }
            return names;
        }

        /// <summary>
        /// 尝试通过名称获取预定义层级
        /// </summary>
        public static bool TryParse(string name, out UILevel level)
        {
            for (int i = 0; i < sPredefinedEntries.Length; i++)
            {
                if (string.Equals(sPredefinedEntries[i].name, name, StringComparison.Ordinal))
                {
                    level = sPredefinedEntries[i].level;
                    return true;
                }
            }
            level = default;
            return false;
        }

        #endregion

        #region 属性

        /// <summary>
        /// 排序值，越小越靠底层
        /// </summary>
        public int Order => mOrder;

        #endregion

        #region 隐式转换

        public static implicit operator int(UILevel level) => level.mOrder;
        public static implicit operator UILevel(int order) => new(order);

        #endregion

        #region 比较运算符

        public static bool operator ==(UILevel left, UILevel right) => left.mOrder == right.mOrder;
        public static bool operator !=(UILevel left, UILevel right) => left.mOrder != right.mOrder;
        public static bool operator <(UILevel left, UILevel right) => left.mOrder < right.mOrder;
        public static bool operator >(UILevel left, UILevel right) => left.mOrder > right.mOrder;
        public static bool operator <=(UILevel left, UILevel right) => left.mOrder <= right.mOrder;
        public static bool operator >=(UILevel left, UILevel right) => left.mOrder >= right.mOrder;

        #endregion

        #region IEquatable / IComparable

        public bool Equals(UILevel other) => mOrder == other.mOrder;

        public override bool Equals(object obj) => obj is UILevel other && Equals(other);

        public override int GetHashCode() => mOrder;

        public int CompareTo(UILevel other) => mOrder.CompareTo(other.mOrder);

        #endregion

        #region ToString

        /// <summary>
        /// 预定义层级返回名称（如 "Common"），自定义层级返回 "UILevel(order)"
        /// </summary>
        public override string ToString()
        {
            for (int i = 0; i < sPredefinedEntries.Length; i++)
            {
                if (sPredefinedEntries[i].level.mOrder == mOrder)
                    return sPredefinedEntries[i].name;
            }
            return $"UILevel({mOrder})";
        }

        #endregion
    }
}
