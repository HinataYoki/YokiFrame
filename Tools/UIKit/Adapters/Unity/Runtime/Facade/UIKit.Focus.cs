#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    public static partial class UIKit
    {
        /// <summary>供可选输入 Integration 注册 EventSystem 输入模块工厂。</summary>
        /// <param name="factory">接收 EventSystem owner 并返回新输入模块的工厂。</param>
        internal static void RegisterInputModuleFactory(Func<GameObject, BaseInputModule> factory)
        {
            UIRoot.RegisterInputModuleFactory(factory);
        }

        /// <summary>确保当前 UIKit 存在可用 EventSystem。</summary>
        public static EventSystem EnsureEventSystem()
        {
            return RequireRoot().EnsureEventSystem();
        }

        /// <summary>获取当前 EventSystem 焦点，不创建 UIRoot。</summary>
        public static GameObject CurrentFocus
        {
            get
            {
                UIRoot root = Root;
                return root != null ? root.CurrentFocus : null;
            }
        }

        /// <summary>获取当前输入模式，不创建 UIRoot。</summary>
        public static UIInputMode InputMode
        {
            get
            {
                UIRoot root = Root;
                return root != null ? root.InputMode : UIInputMode.Pointer;
            }
        }

        /// <summary>显式切换 Pointer/Navigation 输入模式；进入导航模式时尝试恢复焦点。</summary>
        public static void SetInputMode(UIInputMode mode)
        {
            RequireRoot().SetInputMode(mode);
        }

        /// <summary>设置 GameObject 为当前焦点。</summary>
        public static bool SetFocus(GameObject target)
        {
            return RequireRoot().SetFocus(target);
        }

        /// <summary>设置 Selectable 为当前焦点。</summary>
        public static bool SetFocus(Selectable selectable)
        {
            return RequireRoot().SetFocus(selectable);
        }

        /// <summary>清除当前焦点。</summary>
        public static void ClearFocus()
        {
            UIRoot root = Root;
            if (root != null) root.ClearFocus();
        }

        /// <summary>恢复最近焦点，失败时使用顶部面板默认或首个可交互控件。</summary>
        public static bool RestoreLastFocus()
        {
            UIRoot root = Root;
            return root != null && root.RestoreLastFocus();
        }

        /// <summary>查找指定子树的首个可交互控件，不创建 UIRoot。</summary>
        public static Selectable FindFirstSelectable(Transform root)
        {
            return UIRoot.FindFirstSelectable(root);
        }
    }
}
#endif
