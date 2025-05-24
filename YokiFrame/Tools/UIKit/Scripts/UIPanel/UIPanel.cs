﻿using System;
using UnityEngine;

namespace YokiFrame
{
    public abstract class UIPanel : MonoBehaviour, IPanel
    {
        public Transform Transform => transform;
        public PanelState State { get; set; }
        public PanelHandler Handler { get; set; }
        private Action mOnClosed;

        public void Init(IUIData data = null)
        {
            OnInit(data);
        }

        public void Open()
        {
            State = PanelState.Open;
            OnOpen();
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
            mOnClosed?.Invoke();
            mOnClosed = null;
            OnClose();
            Destroy(gameObject);
            Handler.Recycle();
        }

        public void OnClosed(Action onClosed) => mOnClosed = onClosed;

        protected virtual void OnInit(IUIData data = null) { }
        protected virtual void OnOpen() { }
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