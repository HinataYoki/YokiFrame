#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    public sealed partial class UIRoot
    {
        private static Func<GameObject, BaseInputModule> sInputModuleFactory;
        private readonly Dictionary<UIPanel, GameObject> mPanelFocusMemory = new();
        private EventSystem mEventSystem;
        private GameObject mLastFocusedObject;
        private UIInputMode mInputMode = UIInputMode.Pointer;

        /// <summary>获取当前 UIKit 复用或创建的 EventSystem。</summary>
        public EventSystem EventSystem => mEventSystem;

        /// <summary>获取当前 EventSystem 选中的 GameObject。</summary>
        public GameObject CurrentFocus => mEventSystem != default
            ? mEventSystem.currentSelectedGameObject
            : null;

        /// <summary>获取当前输入模式。</summary>
        public UIInputMode InputMode => mInputMode;

        /// <summary>注册当前输入方案创建 UI 输入模块的工厂；未注册时使用 StandaloneInputModule。</summary>
        /// <param name="factory">接收 EventSystem owner 并返回新输入模块的工厂。</param>
        internal static void RegisterInputModuleFactory(Func<GameObject, BaseInputModule> factory)
        {
            sInputModuleFactory = factory;
        }

        /// <summary>复用场景 EventSystem；缺失时启用 Prefab 内置节点或创建新的 EventSystem。</summary>
        public EventSystem EnsureEventSystem()
        {
            AssertMainThread();
            if (mEventSystem != default) return mEventSystem;
            mEventSystem = EventSystem.current;
            if (mEventSystem != default)
            {
                if (IsEmbeddedEventSystem(mEventSystem)) EnsureEmbeddedEventSystemReady(mEventSystem);
                else EnsureInputModule(mEventSystem.gameObject);
                return mEventSystem;
            }

            mEventSystem = FindEmbeddedEventSystem();
            if (mEventSystem != default)
            {
                EnsureEmbeddedEventSystemReady(mEventSystem);
                return mEventSystem;
            }

            GameObject eventObject = new("EventSystem");
            Transform owner = FindLegacyHierarchyOwner();
            eventObject.transform.SetParent(owner != default ? owner : transform, false);
            mEventSystem = eventObject.AddComponent<EventSystem>();
            EnsureInputModule(eventObject);
            return mEventSystem;
        }

        /// <summary>从旧版 `UIKit` owner 中查找可按需启用的内置 EventSystem。</summary>
        private EventSystem FindEmbeddedEventSystem()
        {
            Transform owner = FindLegacyHierarchyOwner();
            return owner == default ? default : owner.GetComponentInChildren<EventSystem>(true);
        }

        /// <summary>判断当前 EventSystem 是否属于旧版 UIKit Prefab，而不是项目场景输入系统。</summary>
        /// <param name="eventSystem">待判断的 EventSystem。</param>
        private bool IsEmbeddedEventSystem(EventSystem eventSystem)
        {
            Transform owner = FindLegacyHierarchyOwner();
            return eventSystem != default && owner != default && eventSystem.transform.IsChildOf(owner);
        }

        /// <summary>启用内置 EventSystem，并只在缺少输入模块时补齐当前默认模块。</summary>
        /// <param name="eventSystem">旧版 Prefab 内置 EventSystem。</param>
        private static void EnsureEmbeddedEventSystemReady(EventSystem eventSystem)
        {
            if (!eventSystem.gameObject.activeSelf) eventSystem.gameObject.SetActive(true);
            EnsureInputModule(eventSystem.gameObject);
        }

        /// <summary>保留已有模块；缺失时按当前输入方案工厂创建，未注册工厂则回落旧输入模块。</summary>
        /// <param name="owner">EventSystem 所在 GameObject。</param>
        private static BaseInputModule EnsureInputModule(GameObject owner)
        {
            BaseInputModule inputModule = owner.GetComponent<BaseInputModule>();
            if (inputModule != default) return inputModule;
            Func<GameObject, BaseInputModule> factory = sInputModuleFactory;
            return factory != null
                ? factory(owner)
                : owner.AddComponent<StandaloneInputModule>();
        }

        /// <summary>设置当前输入模式；进入导航模式且无焦点时恢复最近有效焦点。</summary>
        public void SetInputMode(UIInputMode mode)
        {
            AssertMainThread();
            if (mInputMode == mode) return;
            mInputMode = mode;
            if (mode == UIInputMode.Navigation && CurrentFocus == default) RestoreLastFocus();
        }

        /// <summary>设置可交互控件为当前焦点，并切换到导航输入模式。</summary>
        public bool SetFocus(Selectable selectable)
        {
            if (!IsValidSelectable(selectable)) return false;
            return SetFocus(selectable.gameObject);
        }

        /// <summary>设置指定 GameObject 为当前 EventSystem 焦点。</summary>
        public bool SetFocus(GameObject target)
        {
            EventSystem eventSystem = EnsureEventSystem();
            if (target == default || !target.activeInHierarchy) return false;
            Selectable selectable = target.GetComponent<Selectable>();
            if (selectable != default && !selectable.interactable) return false;
            eventSystem.SetSelectedGameObject(target);
            if (eventSystem.currentSelectedGameObject != target) return false;
            mLastFocusedObject = target;
            SetInputMode(UIInputMode.Navigation);
            return true;
        }

        /// <summary>清除当前焦点并切换到 Pointer 输入模式。</summary>
        public void ClearFocus()
        {
            ClearSelection();
            SetInputMode(UIInputMode.Pointer);
        }

        /// <summary>恢复最近有效焦点，失败时使用顶部面板的默认或首个控件。</summary>
        public bool RestoreLastFocus()
        {
            if (IsValidFocusObject(mLastFocusedObject)) return SetFocus(mLastFocusedObject);
            UIKitController controller = mController;
            IPanel top = controller != null ? controller.GetGlobalTop() : null;
            if (top == null) return false;
            Selectable fallback = top is UIPanel panel
                ? ResolvePanelFocus(panel)
                : FindFirstSelectable(top.Transform);
            return fallback != default && SetFocus(fallback);
        }

        /// <summary>面板显示完成后按记忆、默认和层级顺序恢复焦点。</summary>
        internal void OnPanelShown(UIPanel panel)
        {
            if (panel == default || mInputMode != UIInputMode.Navigation || !panel.AutoFocusOnShow) return;
            Selectable selectable = ResolvePanelFocus(panel);
            if (selectable != default) SetFocus(selectable);
        }

        /// <summary>面板隐藏前保存其当前子焦点，并在不改变输入模式的情况下清除选择。</summary>
        internal void OnPanelHidden(UIPanel panel)
        {
            if (panel == default) return;
            GameObject current = CurrentFocus;
            if (current == default || !IsDescendant(current.transform, panel.transform)) return;
            mPanelFocusMemory[panel] = current;
            mLastFocusedObject = current;
            ClearSelection();
        }

        /// <summary>面板关闭或销毁时释放其焦点记忆和失效对象引用。</summary>
        internal void OnPanelClosed(UIPanel panel)
        {
            if (panel == default) return;
            GameObject current = CurrentFocus;
            if (current != default && IsDescendant(current.transform, panel.transform)) ClearSelection();
            if (mLastFocusedObject != default
                && IsDescendant(mLastFocusedObject.transform, panel.transform)) mLastFocusedObject = null;
            mPanelFocusMemory.Remove(panel);
        }

        /// <summary>同步 EventSystem 外部导航造成的焦点变化，供后续面板恢复使用。</summary>
        internal void TrackExternalFocusChanges()
        {
            GameObject current = CurrentFocus;
            if (current != default && current != mLastFocusedObject) mLastFocusedObject = current;
        }

        /// <summary>Root 销毁时释放焦点记忆和事件订阅。</summary>
        internal void DisposeFocusState()
        {
            mPanelFocusMemory.Clear();
            mLastFocusedObject = null;
            mEventSystem = null;
        }

        /// <summary>从指定 Transform 子树找到第一个可交互 Selectable。</summary>
        public static Selectable FindFirstSelectable(Transform root)
        {
            if (root == default) return null;
            SelectableGroup group = root.GetComponentInChildren<SelectableGroup>(true);
            if (group != default)
            {
                Selectable first = group.GetFirstSelectable();
                if (first != default) return first;
            }
            UINavigationGrid grid = root.GetComponentInChildren<UINavigationGrid>(true);
            if (grid != default)
            {
                Selectable first = grid.GetFirstSelectable();
                if (first != default) return first;
            }
            Selectable[] selectables = root.GetComponentsInChildren<Selectable>(false);
            for (var index = 0; index < selectables.Length; index++)
            {
                if (IsValidSelectable(selectables[index])) return selectables[index];
            }
            return null;
        }

        /// <summary>优先读取面板记忆焦点，其次使用显式默认和层级首项。</summary>
        private Selectable ResolvePanelFocus(UIPanel panel)
        {
            if (mPanelFocusMemory.TryGetValue(panel, out GameObject remembered)
                && IsValidFocusObject(remembered)) return remembered.GetComponent<Selectable>();
            Selectable configured = panel.GetDefaultSelectable();
            return IsValidSelectable(configured) ? configured : FindFirstSelectable(panel.transform);
        }

        /// <summary>清除 EventSystem 选择，不改变当前 Pointer/Navigation 模式。</summary>
        private void ClearSelection()
        {
            if (mEventSystem != default) mEventSystem.SetSelectedGameObject(null);
        }

        /// <summary>判断 GameObject 当前可作为有效 Selectable 焦点。</summary>
        private static bool IsValidFocusObject(GameObject target)
        {
            if (target == default || !target.activeInHierarchy) return false;
            Selectable selectable = target.GetComponent<Selectable>();
            return selectable != default && IsValidSelectable(selectable);
        }

        /// <summary>判断 Selectable 当前激活且允许交互。</summary>
        private static bool IsValidSelectable(Selectable selectable)
        {
            return selectable != default
                && selectable.interactable
                && selectable.gameObject.activeInHierarchy;
        }

        /// <summary>判断 Transform 是否属于指定面板子树。</summary>
        private static bool IsDescendant(Transform child, Transform parent)
        {
            if (child == default || parent == default) return false;
            Transform current = child;
            while (current != default)
            {
                if (current == parent) return true;
                current = current.parent;
            }
            return false;
        }
    }
}
#endif
