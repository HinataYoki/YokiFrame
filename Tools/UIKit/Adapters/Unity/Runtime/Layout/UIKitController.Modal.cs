#if UNITY_2022_3_OR_NEWER
using System;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    internal sealed partial class UIKitController
    {
        private const float DEFAULT_MODAL_ALPHA = 0.45f;

        /// <summary>
        /// 设置受管面板的模态状态；只有可见面板会持有 blocker。
        /// </summary>
        internal bool SetModal(IPanel panel, bool isModal)
        {
            EnsureAvailable();
            if (!TryGetOwnedEntry(panel, out PanelEntry entry)) return false;
            entry.IsModal = isModal;
            if (isModal) EnsureModalBlocker(entry);
            else RemoveModalBlocker(entry);
            if (entry.IsLevelRegistered) SortLevel(entry.Level);
            OnStateChanged();
            return true;
        }

        /// <summary>
        /// 判断当前是否存在至少一个有效模态 blocker。
        /// </summary>
        internal bool HasModalBlocker()
        {
            EnsureAvailable();
            foreach (PanelEntry entry in mEntries.Values)
            {
                if (entry.ModalBlocker != default) return true;
            }

            return false;
        }

        /// <summary>
        /// 为可见模态面板创建唯一 blocker，并放到同一个层级容器。
        /// </summary>
        private void EnsureModalBlocker(PanelEntry entry)
        {
            if (!entry.IsModal || entry.State != PanelState.Open || entry.Panel == default) return;
            if (entry.ModalBlocker != default) return;
            Transform parent = entry.Panel.transform.parent;
            if (parent == default) return;
            entry.ModalBlocker = CreateModalBlocker(entry.Panel.PanelName, parent);
        }

        /// <summary>
        /// 创建铺满层级容器且只负责射线阻断的半透明 Image。
        /// </summary>
        private static GameObject CreateModalBlocker(string panelName, Transform parent)
        {
            var blocker = new GameObject(panelName + ".ModalBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rect = blocker.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = Vector2.zero;
            var image = blocker.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, DEFAULT_MODAL_ALPHA);
            image.raycastTarget = true;
            return blocker;
        }

        /// <summary>
        /// 销毁 entry 自己拥有的 blocker，不修改用户 CanvasGroup 或 GraphicRaycaster。
        /// </summary>
        private void RemoveModalBlocker(PanelEntry entry)
        {
            GameObject blocker = entry.ModalBlocker;
            entry.ModalBlocker = null;
            if (blocker == default) return;
            blocker.SetActive(false);
            DestroyObject(blocker);
        }
    }
}
#endif
