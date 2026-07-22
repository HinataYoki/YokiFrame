#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>返回操作的行为类型。</summary>
    public enum BackBehavior
    {
        PopStack,
        ClosePanel,
        HidePanel,
        DoNothing,
        Custom
    }

    /// <summary>为面板提供可由项目输入层显式调用的返回行为。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Back Handler")]
    public sealed class UIBackHandler : MonoBehaviour
    {
        [SerializeField] private BackBehavior mBehavior = BackBehavior.PopStack;
        [SerializeField] private string mTargetPanelTypeName;

        /// <summary>自定义返回回调。</summary>
        public event Action OnCustomBack;

        /// <summary>获取或设置返回行为。</summary>
        public BackBehavior Behavior
        {
            get { return mBehavior; }
            set { mBehavior = value; }
        }

        /// <summary>执行当前返回策略；输入来源由项目或 Input System Integration 决定。</summary>
        public void ExecuteBack()
        {
            IPanel panel = GetComponent<IPanel>();
            switch (mBehavior)
            {
                case BackBehavior.PopStack:
                    UIKit.PopPanel();
                    break;
                case BackBehavior.ClosePanel:
                    if (panel != null) UIKit.ClosePanel(panel);
                    break;
                case BackBehavior.HidePanel:
                    if (panel != null) panel.Hide();
                    break;
                case BackBehavior.Custom:
                    if (OnCustomBack != null) OnCustomBack();
                    break;
            }
        }
    }
}
#endif
