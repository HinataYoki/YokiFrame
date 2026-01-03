using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    public abstract class UIPanel : MonoBehaviour, IPanel
    {
        public Transform Transform => transform;
        public PanelState State { get; set; }
        public PanelHandler Handler { get; set; }

        private List<Action> mOnClosed = new();

        public void Init(IUIData data = null) => OnInit(data);

        public void Open(IUIData data = null)
        {
            State = PanelState.Open;
            OnOpen(data);
        }

        public void Show()
        {
            gameObject.SetActive(true);
            OnShow();
        }

        public void Hide()
        {
            State = PanelState.Hide;
            gameObject.SetActive(false);
            OnHide();
        }

        void IPanel.Close()
        {
            Hide();
            State = PanelState.Close;
            foreach (var action in mOnClosed)
            {
                action?.Invoke();
            }
            mOnClosed.Clear();
            OnClose();
        }

        public void OnClosed(Action onClosed) => mOnClosed.Add(onClosed);

        protected virtual void OnInit(IUIData data = null) { }
        protected virtual void OnOpen(IUIData data = null) { }
        protected virtual void OnShow() { }
        protected virtual void OnHide() { }
        protected virtual void OnClose() { }

        protected virtual void OnBeforeDestroy()
        {
            ClearUIComponents();
        }

        protected virtual void ClearUIComponents() { }

        protected void CloseSelf() => UIKit.ClosePanel(this);

        private void OnDestroy()
        {
            OnBeforeDestroy();
        }
    }
}