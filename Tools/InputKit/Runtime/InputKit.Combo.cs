using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace YokiFrame
{
    /// <summary>
    /// InputKit - 组合键/连招系统
    /// </summary>
    public static partial class InputKit
    {
        private static ComboMatcher sComboMatcher;

        #region 事件

        /// <summary>连招匹配成功事件</summary>
        public static event Action<string> OnComboTriggered;

        /// <summary>连招进度事件（comboId, currentStep, totalSteps）</summary>
        public static event Action<string, int, int> OnComboProgress;

        /// <summary>连招失败/超时事件</summary>
        public static event Action<string> OnComboFailed;

        #endregion

        #region 注册/注销

        /// <summary>
        /// 注册连招定义
        /// </summary>
        public static void RegisterCombo(ComboDefinition combo)
        {
            EnsureComboMatcherInitialized();
            sComboMatcher.Register(combo);
        }

        /// <summary>
        /// 注册连招（代码方式）
        /// </summary>
        public static void RegisterCombo(string comboId, params ComboStep[] steps)
        {
            RegisterCombo(comboId, 0.3f, steps);
        }

        /// <summary>
        /// 注册连招（代码方式，指定窗口时长）
        /// </summary>
        public static void RegisterCombo(string comboId, float windowBetweenSteps, params ComboStep[] steps)
        {
            EnsureComboMatcherInitialized();
            sComboMatcher.Register(comboId, windowBetweenSteps, steps);
        }

        /// <summary>
        /// 注销连招
        /// </summary>
        public static void UnregisterCombo(string comboId)
        {
            sComboMatcher?.Unregister(comboId);
        }

        /// <summary>
        /// 清空所有连招
        /// </summary>
        public static void ClearAllCombos()
        {
            sComboMatcher?.Clear();
        }

        #endregion

        #region 输入处理

        /// <summary>
        /// 处理 Tap 输入（类型安全）
        /// </summary>
        /// <param name="action">InputAction 引用</param>
        public static void ProcessComboTap(InputAction action)
        {
            if (action == default) return;
            sComboMatcher?.ProcessInput(action.id.ToString(), ComboInputType.Tap);
        }

        /// <summary>
        /// 处理 Tap 输入（字符串，向后兼容）
        /// </summary>
        /// <param name="actionName">Action 名称</param>
        public static void ProcessComboTap(string actionName)
        {
            sComboMatcher?.ProcessInput(actionName, ComboInputType.Tap);
        }

        /// <summary>
        /// 处理 Release 输入（类型安全）
        /// </summary>
        /// <param name="action">InputAction 引用</param>
        public static void ProcessComboRelease(InputAction action)
        {
            if (action == default) return;
            sComboMatcher?.ProcessInput(action.id.ToString(), ComboInputType.Release);
        }

        /// <summary>
        /// 处理 Release 输入（字符串，向后兼容）
        /// </summary>
        /// <param name="actionName">Action 名称</param>
        public static void ProcessComboRelease(string actionName)
        {
            sComboMatcher?.ProcessInput(actionName, ComboInputType.Release);
        }

        /// <summary>
        /// 处理方向输入（类型安全）
        /// </summary>
        /// <param name="action">InputAction 引用</param>
        /// <param name="direction">方向向量</param>
        public static void ProcessComboDirection(InputAction action, Vector2 direction)
        {
            if (action == default) return;
            sComboMatcher?.ProcessDirectionInput(action.id.ToString(), direction);
        }

        /// <summary>
        /// 处理方向输入（字符串，向后兼容）
        /// </summary>
        /// <param name="actionName">Action 名称</param>
        /// <param name="direction">方向向量</param>
        public static void ProcessComboDirection(string actionName, Vector2 direction)
        {
            sComboMatcher?.ProcessDirectionInput(actionName, direction);
        }

        /// <summary>
        /// 更新连招系统（检查超时，需要在 Update 中调用）
        /// </summary>
        public static void UpdateCombo()
        {
            sComboMatcher?.Update();
        }

        #endregion

        #region 内部方法

        private static void EnsureComboMatcherInitialized()
        {
            if (sComboMatcher != default) return;
            
            sComboMatcher = new ComboMatcher();
            sComboMatcher.OnComboTriggered += HandleComboTriggered;
            sComboMatcher.OnComboProgress += HandleComboProgress;
            sComboMatcher.OnComboFailed += HandleComboFailed;
        }

        private static void HandleComboTriggered(string comboId)
        {
            OnComboTriggered?.Invoke(comboId);
        }

        private static void HandleComboProgress(string comboId, int current, int total)
        {
            OnComboProgress?.Invoke(comboId, current, total);
        }

        private static void HandleComboFailed(string comboId)
        {
            OnComboFailed?.Invoke(comboId);
        }

        /// <summary>
        /// 重置连招系统（内部调用）
        /// </summary>
        internal static void ResetCombo()
        {
            if (sComboMatcher != default)
            {
                sComboMatcher.OnComboTriggered -= HandleComboTriggered;
                sComboMatcher.OnComboProgress -= HandleComboProgress;
                sComboMatcher.OnComboFailed -= HandleComboFailed;
                sComboMatcher.Clear();
                sComboMatcher = default;
            }
            
            OnComboTriggered = null;
            OnComboProgress = null;
            OnComboFailed = null;
        }

        #endregion
    }
}
