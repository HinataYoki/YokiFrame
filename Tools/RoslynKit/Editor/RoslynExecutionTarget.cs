#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;

namespace YokiFrame
{
    /// <summary>
    /// 描述执行目标：命令在哪一种运行形态下执行。引擎差异只体现在"谁能承载该 target"。
    /// </summary>
    [Flags]
    public enum RoslynExecutionTarget
    {
        /// <summary>未指定。</summary>
        None = 0,

        /// <summary>编辑器域、非播放态。</summary>
        Editor = 1,

        /// <summary>运行态：Unity 为编辑器进程内的 Play Mode；Godot 由 godot-runtime 宿主承载。</summary>
        Play = 2,

        /// <summary>独立进程中的游戏（本期仅 Godot 支持）。</summary>
        Runtime = 4
    }

    /// <summary>
    /// 执行目标的 wire 名称解析与格式化；名称是对外契约，不得随意改动。
    /// </summary>
    public static class RoslynExecutionTargets
    {
        /// <summary>编辑器目标名称。</summary>
        public const string EDITOR = "editor";

        /// <summary>运行态目标名称。</summary>
        public const string PLAY = "play";

        /// <summary>独立进程目标名称。</summary>
        public const string RUNTIME = "runtime";

        /// <summary>
        /// 判断目标集合是否恰好是单一目标；请求只允许单一目标，组合值只用于能力声明。
        /// </summary>
        /// <param name="target">待判断目标。</param>
        /// <returns>恰好一个目标位时返回 true。</returns>
        public static bool IsSingleTarget(RoslynExecutionTarget target)
        {
            return target != RoslynExecutionTarget.None
                && (target & (target - 1)) == 0;
        }

        /// <summary>全部目标。</summary>
        public const RoslynExecutionTarget ALL =
            RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play | RoslynExecutionTarget.Runtime;

        /// <summary>
        /// 解析 target 字段；支持 <c>play</c> 或 <c>editor|play</c> 形式，名称不区分大小写。
        /// </summary>
        /// <param name="value">target 文本。</param>
        /// <param name="target">解析结果。</param>
        /// <returns>存在可识别目标时返回 true。</returns>
        public static bool TryParse(string value, out RoslynExecutionTarget target)
        {
            target = RoslynExecutionTarget.None;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            string[] parts = value.Split(new[] { '|', ',' });
            for (var index = 0; index < parts.Length; index++)
            {
                string part = parts[index].Trim();
                if (part.Length == 0)
                {
                    continue;
                }

                if (string.Equals(part, EDITOR, StringComparison.OrdinalIgnoreCase))
                {
                    target |= RoslynExecutionTarget.Editor;
                }
                else if (string.Equals(part, PLAY, StringComparison.OrdinalIgnoreCase))
                {
                    target |= RoslynExecutionTarget.Play;
                }
                else if (string.Equals(part, RUNTIME, StringComparison.OrdinalIgnoreCase))
                {
                    target |= RoslynExecutionTarget.Runtime;
                }
                else
                {
                    target = RoslynExecutionTarget.None;
                    return false;
                }
            }

            return target != RoslynExecutionTarget.None;
        }

        /// <summary>
        /// 按固定顺序展开单个目标，供能力矩阵逐项报告。
        /// </summary>
        /// <param name="target">目标集合。</param>
        /// <returns>单个目标列表。</returns>
        public static System.Collections.Generic.IReadOnlyList<RoslynExecutionTarget> Enumerate(
            RoslynExecutionTarget target)
        {
            var targets = new System.Collections.Generic.List<RoslynExecutionTarget>(3);
            if ((target & RoslynExecutionTarget.Editor) != 0)
            {
                targets.Add(RoslynExecutionTarget.Editor);
            }

            if ((target & RoslynExecutionTarget.Play) != 0)
            {
                targets.Add(RoslynExecutionTarget.Play);
            }

            if ((target & RoslynExecutionTarget.Runtime) != 0)
            {
                targets.Add(RoslynExecutionTarget.Runtime);
            }

            return targets;
        }

        /// <summary>
        /// 把目标集合格式化为 wire 文本，例如 <c>editor|play</c>。
        /// </summary>
        /// <param name="target">目标集合。</param>
        /// <returns>wire 文本；空集合返回空字符串。</returns>
        public static string Format(RoslynExecutionTarget target)
        {
            var parts = new System.Collections.Generic.List<string>(3);
            if ((target & RoslynExecutionTarget.Editor) != 0)
            {
                parts.Add(EDITOR);
            }

            if ((target & RoslynExecutionTarget.Play) != 0)
            {
                parts.Add(PLAY);
            }

            if ((target & RoslynExecutionTarget.Runtime) != 0)
            {
                parts.Add(RUNTIME);
            }

            return string.Join("|", parts.ToArray());
        }
    }
}
#endif