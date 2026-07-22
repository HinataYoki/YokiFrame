#if UNITY_2022_3_OR_NEWER
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    public abstract partial class UIPanel
    {
        [Header("焦点配置")]
        [Tooltip("导航模式下显示面板时自动恢复或设置焦点")]
        [SerializeField] protected bool mAutoFocusOnShow;

        [Tooltip("默认焦点；为空时查找首个可交互 Selectable")]
        [SerializeField] protected Selectable mDefaultSelectable;

        /// <summary>获取导航模式下显示面板时是否自动设置焦点。</summary>
        public virtual bool AutoFocusOnShow => mAutoFocusOnShow;

        /// <summary>获取面板配置的默认焦点。</summary>
        public Selectable GetDefaultSelectable()
        {
            return mDefaultSelectable;
        }

        /// <summary>设置面板默认焦点；可在动态构建 UI 后调用。</summary>
        public void SetDefaultSelectable(Selectable selectable)
        {
            mDefaultSelectable = selectable;
        }

        /// <summary>设置导航模式下显示面板时是否自动恢复焦点。</summary>
        public void SetAutoFocusOnShow(bool value)
        {
            mAutoFocusOnShow = value;
        }
    }
}
#endif
