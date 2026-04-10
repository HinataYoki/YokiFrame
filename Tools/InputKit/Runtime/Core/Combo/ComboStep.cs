#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// 连招步骤定义
    /// </summary>
    [Serializable]
    public struct ComboStep
    {
        /// <summary>Action 名称（备用，用于字符串匹配）</summary>
        public string ActionName;

        /// <summary>InputAction 引用（类型安全，优先使用）</summary>
        [NonSerialized]
        public InputAction Action;

        /// <summary>输入类型</summary>
        public ComboInputType InputType;

        /// <summary>Hold 类型的最小时长（秒）</summary>
        public float HoldDuration;

        /// <summary>方向输入范围（X=最小角度, Y=最大角度，0-360）</summary>
        public Vector2 DirectionRange;

        /// <summary>
        /// 获取有效的 Action 标识符（优先使用 Action.id）
        /// </summary>
        public string EffectiveActionId => Action != default
            ? Action.id.ToString()
            : ActionName;

        /// <summary>
        /// 创建 Tap 类型步骤（类型安全）
        /// </summary>
        /// <param name="action">InputAction 引用</param>
        public static ComboStep Tap(InputAction action) => new()
        {
            Action = action,
            ActionName = action != default ? action.name : string.Empty,
            InputType = ComboInputType.Tap,
            HoldDuration = 0f,
            DirectionRange = Vector2.zero
        };

        /// <summary>
        /// 创建 Tap 类型步骤（字符串，向后兼容）
        /// </summary>
        /// <param name="actionName">Action 名称</param>
        public static ComboStep Tap(string actionName) => new()
        {
            ActionName = actionName,
            InputType = ComboInputType.Tap,
            HoldDuration = 0f,
            DirectionRange = Vector2.zero
        };

        /// <summary>
        /// 创建 Hold 类型步骤（类型安全）
        /// </summary>
        /// <param name="action">InputAction 引用</param>
        /// <param name="duration">按住时长（秒）</param>
        public static ComboStep Hold(InputAction action, float duration) => new()
        {
            Action = action,
            ActionName = action != default ? action.name : string.Empty,
            InputType = ComboInputType.Hold,
            HoldDuration = duration,
            DirectionRange = Vector2.zero
        };

        /// <summary>
        /// 创建 Hold 类型步骤（字符串，向后兼容）
        /// </summary>
        /// <param name="actionName">Action 名称</param>
        /// <param name="duration">按住时长（秒）</param>
        public static ComboStep Hold(string actionName, float duration) => new()
        {
            ActionName = actionName,
            InputType = ComboInputType.Hold,
            HoldDuration = duration,
            DirectionRange = Vector2.zero
        };

        /// <summary>
        /// 创建 Release 类型步骤（类型安全）
        /// </summary>
        /// <param name="action">InputAction 引用</param>
        public static ComboStep Release(InputAction action) => new()
        {
            Action = action,
            ActionName = action != default ? action.name : string.Empty,
            InputType = ComboInputType.Release,
            HoldDuration = 0f,
            DirectionRange = Vector2.zero
        };

        /// <summary>
        /// 创建 Release 类型步骤（字符串，向后兼容）
        /// </summary>
        /// <param name="actionName">Action 名称</param>
        public static ComboStep Release(string actionName) => new()
        {
            ActionName = actionName,
            InputType = ComboInputType.Release,
            HoldDuration = 0f,
            DirectionRange = Vector2.zero
        };

        /// <summary>
        /// 创建方向输入步骤（类型安全）
        /// </summary>
        /// <param name="action">InputAction 引用</param>
        /// <param name="minAngle">最小角度</param>
        /// <param name="maxAngle">最大角度</param>
        public static ComboStep Direction(InputAction action, float minAngle, float maxAngle) => new()
        {
            Action = action,
            ActionName = action != default ? action.name : string.Empty,
            InputType = ComboInputType.Direction,
            HoldDuration = 0f,
            DirectionRange = new Vector2(minAngle, maxAngle)
        };

        /// <summary>
        /// 创建方向输入步骤（字符串，向后兼容）
        /// </summary>
        /// <param name="actionName">Action 名称</param>
        /// <param name="minAngle">最小角度</param>
        /// <param name="maxAngle">最大角度</param>
        public static ComboStep Direction(string actionName, float minAngle, float maxAngle) => new()
        {
            ActionName = actionName,
            InputType = ComboInputType.Direction,
            HoldDuration = 0f,
            DirectionRange = new Vector2(minAngle, maxAngle)
        };
    }
}

#endif