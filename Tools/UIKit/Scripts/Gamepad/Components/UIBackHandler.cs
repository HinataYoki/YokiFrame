using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 返回键处理器 - 自定义面板的返回行为
    /// </summary>
    public class UIBackHandler : MonoBehaviour
    {
        #region 配置

        [Header("返回行为")]
        [Tooltip("返回行为类型")]
        [SerializeField] private BackBehavior mBehavior = BackBehavior.PopStack;

        [Tooltip("自定义返回目标面板类型名")]
        [SerializeField] private string mTargetPanelTypeName;

        #endregion

        #region 事件

        /// <summary>
        /// 自定义返回处理（当 Behavior 为 Custom 时触发）
        /// </summary>
        public event Action OnCustomBack;

        #endregion

        #region 属性

        /// <summary>
        /// 返回行为
        /// </summary>
        public BackBehavior Behavior
        {
            get => mBehavior;
            set => mBehavior = value;
        }

        #endregion

        #region 生命周期

        private void OnEnable()
        {
            EventKit.Type.Register<GamepadCancelEvent>(HandleCancel).UnRegisterWhenDisabled(this);
        }

        #endregion

        #region 处理

        private void HandleCancel(GamepadCancelEvent evt)
        {
            // 检查是否是当前面板
            var panel = GetComponent<IPanel>();
            if (panel == null || evt.CurrentPanel != panel) return;

            ExecuteBack();
        }

        /// <summary>
        /// 执行返回操作
        /// </summary>
        public void ExecuteBack()
        {
            switch (mBehavior)
            {
                case BackBehavior.PopStack:
                    UIKit.PopPanel();
                    break;

                case BackBehavior.ClosePanel:
                    var panel = GetComponent<IPanel>();
                    if (panel != null)
                    {
                        UIKit.ClosePanel(panel);
                    }
                    break;

                case BackBehavior.HidePanel:
                    GetComponent<IPanel>()?.Hide();
                    break;

                case BackBehavior.DoNothing:
                    // 不做任何事
                    break;

                case BackBehavior.Custom:
                    OnCustomBack?.Invoke();
                    break;
            }
        }

        #endregion
    }

    /// <summary>
    /// 返回行为类型
    /// </summary>
    public enum BackBehavior
    {
        /// <summary>
        /// 从栈中弹出（默认）
        /// </summary>
        PopStack,

        /// <summary>
        /// 关闭面板
        /// </summary>
        ClosePanel,

        /// <summary>
        /// 隐藏面板
        /// </summary>
        HidePanel,

        /// <summary>
        /// 不做任何事
        /// </summary>
        DoNothing,

        /// <summary>
        /// 自定义处理
        /// </summary>
        Custom
    }
}
