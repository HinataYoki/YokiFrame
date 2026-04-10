#if YOKIFRAME_INPUTSYSTEM_SUPPORT
using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 连招匹配器
    /// </summary>
    public sealed class ComboMatcher
    {
        private readonly Dictionary<string, ComboDefinition> mCombos = new();
        private readonly Dictionary<string, ComboState> mStates = new();
        
        /// <summary>连招触发事件</summary>
        public event Action<string> OnComboTriggered;
        
        /// <summary>连招进度事件（comboId, currentStep, totalSteps）</summary>
        public event Action<string, int, int> OnComboProgress;
        
        /// <summary>连招失败/超时事件</summary>
        public event Action<string> OnComboFailed;

        #region 注册/注销

        /// <summary>
        /// 注册连招定义
        /// </summary>
        public void Register(ComboDefinition combo)
        {
            if (combo == default || string.IsNullOrEmpty(combo.ComboId)) return;
            
            mCombos[combo.ComboId] = combo;
            mStates[combo.ComboId] = new ComboState();
        }

        /// <summary>
        /// 注册连招（代码方式）
        /// </summary>
        public void Register(string comboId, float windowBetweenSteps, params ComboStep[] steps)
        {
            if (string.IsNullOrEmpty(comboId) || steps == default || steps.Length == 0) return;
            
            var combo = ScriptableObject.CreateInstance<ComboDefinition>();
            combo.ComboId = comboId;
            combo.Steps = steps;
            combo.WindowBetweenSteps = windowBetweenSteps;
            combo.RequireExactOrder = true;
            
            mCombos[comboId] = combo;
            mStates[comboId] = new ComboState();
        }

        /// <summary>
        /// 注销连招
        /// </summary>
        public void Unregister(string comboId)
        {
            mCombos.Remove(comboId);
            mStates.Remove(comboId);
        }

        /// <summary>
        /// 清空所有连招
        /// </summary>
        public void Clear()
        {
            mCombos.Clear();
            mStates.Clear();
        }

        #endregion

        #region 输入处理

        /// <summary>
        /// 处理输入（Tap/Release）
        /// </summary>
        public void ProcessInput(string actionName, ComboInputType inputType)
        {
            float currentTime = Time.unscaledTime;
            
            foreach (var kvp in mCombos)
            {
                var comboId = kvp.Key;
                var combo = kvp.Value;
                var state = mStates[comboId];
                
                ProcessComboInput(comboId, combo, state, actionName, inputType, currentTime);
            }
        }

        /// <summary>
        /// 处理方向输入
        /// </summary>
        public void ProcessDirectionInput(string actionName, Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.01f) return;
            
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            if (angle < 0) angle += 360f;
            
            float currentTime = Time.unscaledTime;
            
            foreach (var kvp in mCombos)
            {
                var comboId = kvp.Key;
                var combo = kvp.Value;
                var state = mStates[comboId];
                
                ProcessComboDirection(comboId, combo, state, actionName, angle, currentTime);
            }
        }

        /// <summary>
        /// 更新（检查超时）
        /// </summary>
        public void Update()
        {
            float currentTime = Time.unscaledTime;
            
            foreach (var kvp in mCombos)
            {
                var comboId = kvp.Key;
                var combo = kvp.Value;
                var state = mStates[comboId];
                
                if (state.CurrentStep > 0 && state.IsExpired(currentTime, combo.WindowBetweenSteps))
                {
                    ResetCombo(comboId, state);
                    OnComboFailed?.Invoke(comboId);
                }
            }
        }

        #endregion

        #region 内部方法

        private void ProcessComboInput(
            string comboId, 
            ComboDefinition combo, 
            ComboState state, 
            string actionName, 
            ComboInputType inputType,
            float currentTime)
        {
            if (combo.StepCount == 0) return;
            
            // 检查超时
            if (state.CurrentStep > 0 && state.IsExpired(currentTime, combo.WindowBetweenSteps))
            {
                ResetCombo(comboId, state);
            }
            
            var expectedStep = combo.Steps[state.CurrentStep];
            
            // 检查是否匹配当前步骤
            if (expectedStep.ActionName == actionName && expectedStep.InputType == inputType)
            {
                AdvanceCombo(comboId, combo, state, currentTime);
            }
            else if (combo.RequireExactOrder && state.CurrentStep > 0)
            {
                // 精确顺序模式下，错误输入重置连招
                if (!combo.AllowInterrupt)
                {
                    ResetCombo(comboId, state);
                    OnComboFailed?.Invoke(comboId);
                }
            }
        }

        private void ProcessComboDirection(
            string comboId,
            ComboDefinition combo,
            ComboState state,
            string actionName,
            float angle,
            float currentTime)
        {
            if (combo.StepCount == 0) return;
            
            var expectedStep = combo.Steps[state.CurrentStep];
            
            if (expectedStep.ActionName != actionName || expectedStep.InputType != ComboInputType.Direction)
                return;
            
            // 检查角度是否在范围内
            float minAngle = expectedStep.DirectionRange.x;
            float maxAngle = expectedStep.DirectionRange.y;
            
            bool inRange;
            if (minAngle <= maxAngle)
            {
                inRange = angle >= minAngle && angle <= maxAngle;
            }
            else
            {
                // 跨越 0 度的情况
                inRange = angle >= minAngle || angle <= maxAngle;
            }
            
            if (inRange)
            {
                AdvanceCombo(comboId, combo, state, currentTime);
            }
        }

        private void AdvanceCombo(string comboId, ComboDefinition combo, ComboState state, float currentTime)
        {
            state.CurrentStep++;
            state.LastInputTime = currentTime;
            
            if (state.CurrentStep >= combo.StepCount)
            {
                // 连招完成
                OnComboTriggered?.Invoke(comboId);
                ResetCombo(comboId, state);
            }
            else
            {
                // 连招进行中
                OnComboProgress?.Invoke(comboId, state.CurrentStep, combo.StepCount);
            }
        }

        private static void ResetCombo(string comboId, ComboState state)
        {
            state.CurrentStep = 0;
            state.LastInputTime = 0f;
        }

        #endregion

        #region 内部类

        private sealed class ComboState
        {
            public int CurrentStep;
            public float LastInputTime;

            public bool IsExpired(float currentTime, float window)
            {
                return currentTime - LastInputTime > window;
            }
        }

        #endregion
    }
}

#endif