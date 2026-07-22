#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 表示可扩展的 UIKit 显示层级；排序值越大越靠近用户。
    /// <para><see cref="Common"/> 的排序值为 0，因此 <c>default(UILevel)</c> 与其相等。</para>
    /// </summary>
    [Serializable]
    public struct UILevel : IEquatable<UILevel>, IComparable<UILevel>
    {
        [SerializeField] private int mOrder;

        /// <summary>最底层。</summary>
        public static readonly UILevel AlwayBottom = new(-200);

        /// <summary>背景层。</summary>
        public static readonly UILevel Bg = new(-100);

        /// <summary>HUD 层，用于血条、名牌与世界跟踪 UI。</summary>
        public static readonly UILevel Hud = new(-50);

        /// <summary>默认内容层，等价于 <c>default(UILevel)</c>。</summary>
        public static readonly UILevel Common = new(0);

        /// <summary>轻提示层，用于 Toast、成就与系统通知。</summary>
        public static readonly UILevel Toast = new(50);

        /// <summary>弹窗层。</summary>
        public static readonly UILevel Pop = new(100);

        /// <summary>引导层，用于教程遮罩与高亮提示。</summary>
        public static readonly UILevel Guide = new(150);

        /// <summary>最顶层。</summary>
        public static readonly UILevel AlwayTop = new(200);

        /// <summary>使用独立 Canvas 的面板层。</summary>
        public static readonly UILevel CanvasPanel = new(300);

        private static readonly (UILevel Level, string Name)[] sPredefinedEntries =
        {
            (AlwayBottom, nameof(AlwayBottom)),
            (Bg, nameof(Bg)),
            (Hud, nameof(Hud)),
            (Common, nameof(Common)),
            (Toast, nameof(Toast)),
            (Pop, nameof(Pop)),
            (Guide, nameof(Guide)),
            (AlwayTop, nameof(AlwayTop)),
            (CanvasPanel, nameof(CanvasPanel))
        };

        private static readonly ReadOnlyCollection<UILevel> sPredefinedLevels =
            Array.AsReadOnly(BuildPredefinedLevels());
        private static readonly ReadOnlyCollection<string> sPredefinedLevelNames =
            Array.AsReadOnly(BuildPredefinedLevelNames());

        /// <summary>
        /// 创建一个自定义 UI 层级。
        /// </summary>
        /// <param name="order">层级排序值；数值越大越靠近用户。</param>
        public UILevel(int order)
        {
            mOrder = order;
        }

        /// <summary>获取层级排序值。</summary>
        public int Order => mOrder;

        /// <summary>获取按排序值升序排列的预定义层级只读集合。</summary>
        public static IReadOnlyList<UILevel> PredefinedLevels => sPredefinedLevels;

        /// <summary>获取与预定义层级对应的名称只读集合。</summary>
        public static IReadOnlyList<string> PredefinedLevelNames => sPredefinedLevelNames;

        /// <summary>
        /// 尝试按区分大小写的预定义名称解析 UI 层级。
        /// </summary>
        /// <param name="name">待解析的预定义层级名称。</param>
        /// <param name="level">解析成功时返回匹配层级；失败时返回 <see cref="Common"/>。</param>
        /// <returns>名称与某个预定义层级完全匹配时返回 true。</returns>
        public static bool TryParse(string name, out UILevel level)
        {
            for (int index = 0; index < sPredefinedEntries.Length; index++)
            {
                if (!string.Equals(sPredefinedEntries[index].Name, name, StringComparison.Ordinal)) continue;
                level = sPredefinedEntries[index].Level;
                return true;
            }

            level = default;
            return false;
        }

        /// <summary>
        /// 将 UI 层级隐式转换为排序值。
        /// </summary>
        /// <param name="level">待转换的 UI 层级。</param>
        /// <returns>层级排序值。</returns>
        public static implicit operator int(UILevel level) => level.mOrder;

        /// <summary>
        /// 将排序值隐式转换为自定义 UI 层级。
        /// </summary>
        /// <param name="order">层级排序值。</param>
        /// <returns>使用指定排序值的 UI 层级。</returns>
        public static implicit operator UILevel(int order) => new(order);

        /// <summary>判断两个 UI 层级的排序值是否相等。</summary>
        /// <param name="left">左侧 UI 层级。</param>
        /// <param name="right">右侧 UI 层级。</param>
        /// <returns>排序值相等时返回 true。</returns>
        public static bool operator ==(UILevel left, UILevel right) => left.mOrder == right.mOrder;

        /// <summary>判断两个 UI 层级的排序值是否不同。</summary>
        /// <param name="left">左侧 UI 层级。</param>
        /// <param name="right">右侧 UI 层级。</param>
        /// <returns>排序值不同时返回 true。</returns>
        public static bool operator !=(UILevel left, UILevel right) => left.mOrder != right.mOrder;

        /// <summary>判断左侧 UI 层级是否低于右侧。</summary>
        /// <param name="left">左侧 UI 层级。</param>
        /// <param name="right">右侧 UI 层级。</param>
        /// <returns>左侧排序值更小时返回 true。</returns>
        public static bool operator <(UILevel left, UILevel right) => left.mOrder < right.mOrder;

        /// <summary>判断左侧 UI 层级是否高于右侧。</summary>
        /// <param name="left">左侧 UI 层级。</param>
        /// <param name="right">右侧 UI 层级。</param>
        /// <returns>左侧排序值更大时返回 true。</returns>
        public static bool operator >(UILevel left, UILevel right) => left.mOrder > right.mOrder;

        /// <summary>判断左侧 UI 层级是否低于或等于右侧。</summary>
        /// <param name="left">左侧 UI 层级。</param>
        /// <param name="right">右侧 UI 层级。</param>
        /// <returns>左侧排序值不大于右侧时返回 true。</returns>
        public static bool operator <=(UILevel left, UILevel right) => left.mOrder <= right.mOrder;

        /// <summary>判断左侧 UI 层级是否高于或等于右侧。</summary>
        /// <param name="left">左侧 UI 层级。</param>
        /// <param name="right">右侧 UI 层级。</param>
        /// <returns>左侧排序值不小于右侧时返回 true。</returns>
        public static bool operator >=(UILevel left, UILevel right) => left.mOrder >= right.mOrder;

        /// <summary>
        /// 判断当前 UI 层级是否与指定层级相等。
        /// </summary>
        /// <param name="other">待比较的 UI 层级。</param>
        /// <returns>排序值相等时返回 true。</returns>
        public bool Equals(UILevel other) => mOrder == other.mOrder;

        /// <summary>
        /// 判断当前 UI 层级是否与指定对象表示相同层级。
        /// </summary>
        /// <param name="obj">待比较的对象。</param>
        /// <returns>对象是同排序值的 UILevel 时返回 true。</returns>
        public override bool Equals(object obj) => obj is UILevel other && Equals(other);

        /// <summary>获取基于排序值的哈希码。</summary>
        /// <returns>当前排序值。</returns>
        public override int GetHashCode() => mOrder;

        /// <summary>
        /// 按排序值比较当前层级与指定层级。
        /// </summary>
        /// <param name="other">待比较的 UI 层级。</param>
        /// <returns>符合 <see cref="IComparable{T}.CompareTo(T)"/> 约定的比较结果。</returns>
        public int CompareTo(UILevel other) => mOrder.CompareTo(other.mOrder);

        /// <summary>
        /// 获取预定义层级名称，或自定义层级的排序值描述。
        /// </summary>
        /// <returns>预定义名称或 <c>UILevel(order)</c>。</returns>
        public override string ToString()
        {
            for (int index = 0; index < sPredefinedEntries.Length; index++)
            {
                if (sPredefinedEntries[index].Level.mOrder == mOrder)
                {
                    return sPredefinedEntries[index].Name;
                }
            }

            return $"UILevel({mOrder})";
        }

        /// <summary>复制预定义层级，避免向调用方暴露内部映射数组。</summary>
        /// <returns>按排序值升序排列的新层级数组。</returns>
        private static UILevel[] BuildPredefinedLevels()
        {
            UILevel[] levels = new UILevel[sPredefinedEntries.Length];
            for (int index = 0; index < sPredefinedEntries.Length; index++)
            {
                levels[index] = sPredefinedEntries[index].Level;
            }

            return levels;
        }

        /// <summary>复制预定义名称，避免向调用方暴露内部映射数组。</summary>
        /// <returns>与预定义层级顺序一致的新名称数组。</returns>
        private static string[] BuildPredefinedLevelNames()
        {
            string[] names = new string[sPredefinedEntries.Length];
            for (int index = 0; index < sPredefinedEntries.Length; index++)
            {
                names[index] = sPredefinedEntries[index].Name;
            }

            return names;
        }
    }
}
#endif
