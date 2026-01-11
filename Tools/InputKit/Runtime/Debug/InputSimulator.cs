using System;
using System.Collections.Generic;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 输入模拟器
    /// 用于测试和自动化，模拟用户输入
    /// </summary>
    public class InputSimulator
    {
        #region 字段

        private readonly Queue<SimulatedInput> mPendingInputs = new(32);
        private readonly Dictionary<string, float> mSimulatedAxes = new(8);
        private readonly HashSet<string> mSimulatedButtons = new(8);

        #endregion

        #region 数据结构

        private struct SimulatedInput
        {
            public float ExecuteTime;
            public Action Action;
        }

        #endregion

        #region 即时模拟

        /// <summary>
        /// 模拟按钮按下
        /// </summary>
        public void SimulateButtonDown(string buttonName)
        {
            mSimulatedButtons.Add(buttonName);
#if ENABLE_INPUT_SYSTEM
            SimulateInputSystemButton(buttonName, true);
#endif
        }

        /// <summary>
        /// 模拟按钮抬起
        /// </summary>
        public void SimulateButtonUp(string buttonName)
        {
            mSimulatedButtons.Remove(buttonName);
#if ENABLE_INPUT_SYSTEM
            SimulateInputSystemButton(buttonName, false);
#endif
        }

        /// <summary>
        /// 模拟按钮点击（按下后立即抬起）
        /// </summary>
        public void SimulateButtonClick(string buttonName)
        {
            SimulateButtonDown(buttonName);
            ScheduleInput(0.1f, () => SimulateButtonUp(buttonName));
        }

        /// <summary>
        /// 模拟轴输入
        /// </summary>
        public void SimulateAxis(string axisName, float value)
        {
            mSimulatedAxes[axisName] = value;
        }

        /// <summary>
        /// 重置轴输入
        /// </summary>
        public void ResetAxis(string axisName)
        {
            mSimulatedAxes.Remove(axisName);
        }

        /// <summary>
        /// 模拟方向输入
        /// </summary>
        public void SimulateDirection(Vector2 direction, float duration = 0f)
        {
            SimulateAxis("Horizontal", direction.x);
            SimulateAxis("Vertical", direction.y);

            if (duration > 0f)
            {
                ScheduleInput(duration, () =>
                {
                    ResetAxis("Horizontal");
                    ResetAxis("Vertical");
                });
            }
        }

        #endregion

        #region 延迟模拟

        /// <summary>
        /// 调度延迟输入
        /// </summary>
        public void ScheduleInput(float delay, Action action)
        {
            mPendingInputs.Enqueue(new SimulatedInput
            {
                ExecuteTime = Time.unscaledTime + delay,
                Action = action
            });
        }

        /// <summary>
        /// 调度按钮序列
        /// </summary>
        public void ScheduleButtonSequence(string[] buttons, float interval = 0.1f)
        {
            float delay = 0f;
            for (int i = 0; i < buttons.Length; i++)
            {
                string button = buttons[i];
                ScheduleInput(delay, () => SimulateButtonClick(button));
                delay += interval;
            }
        }

        #endregion

        #region 查询

        /// <summary>
        /// 检查模拟按钮是否按下
        /// </summary>
        public bool IsButtonSimulated(string buttonName) => mSimulatedButtons.Contains(buttonName);

        /// <summary>
        /// 获取模拟轴值
        /// </summary>
        public float GetSimulatedAxis(string axisName)
        {
            return mSimulatedAxes.TryGetValue(axisName, out float value) ? value : 0f;
        }

        #endregion

        #region 更新

        /// <summary>
        /// 更新模拟器（需要在 Update 中调用）
        /// </summary>
        public void Update()
        {
            float currentTime = Time.unscaledTime;

            while (mPendingInputs.Count > 0)
            {
                var input = mPendingInputs.Peek();
                if (input.ExecuteTime > currentTime) break;

                mPendingInputs.Dequeue();
                input.Action?.Invoke();
            }
        }

        /// <summary>
        /// 清空所有模拟状态
        /// </summary>
        public void Clear()
        {
            mPendingInputs.Clear();
            mSimulatedAxes.Clear();
            mSimulatedButtons.Clear();
        }

        #endregion

        #region InputSystem 集成

#if ENABLE_INPUT_SYSTEM
        private static void SimulateInputSystemButton(string actionName, bool pressed)
        {
            var keyboard = Keyboard.current;
            if (keyboard == default) return;

            // 尝试映射常见按钮名到键盘键
            var key = MapActionToKey(actionName);
            if (key == Key.None) return;

            using (StateEvent.From(keyboard, out var eventPtr))
            {
                keyboard[key].WriteValueIntoEvent(pressed ? 1f : 0f, eventPtr);
                InputSystem.QueueEvent(eventPtr);
            }
        }

        private static Key MapActionToKey(string actionName)
        {
            return actionName.ToLowerInvariant() switch
            {
                "jump" or "space" => Key.Space,
                "attack" or "fire" => Key.Z,
                "dodge" or "roll" => Key.X,
                "interact" => Key.E,
                "menu" or "escape" => Key.Escape,
                "confirm" or "enter" => Key.Enter,
                "cancel" => Key.Escape,
                _ => Key.None
            };
        }
#endif

        #endregion
    }
}
