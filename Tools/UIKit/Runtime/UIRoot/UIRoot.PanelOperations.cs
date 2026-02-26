using System;

namespace YokiFrame
{
    /// <summary>
    /// UIRoot - 面板操作
    /// </summary>
    public partial class UIRoot
    {
        #region 面板操作（供 UIKit 调用）

        internal IPanel OpenPanelInternal(Type type, UILevel level, IUIData data)
        {
            WeakenAllHot();

            if (TryGetCachedHandler(type, out var handler))
            {
                handler.Data = data;
                handler.Hot += OpenHot;
                OpenAndShowPanelInternal(handler.Panel, data);
                return handler.Panel;
            }

            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;

            var panel = LoadPanel(handler);
            if (panel != default && panel.Transform != default)
            {
                SetupPanelInternal(handler, panel);
                OpenAndShowPanelInternal(panel, data);
                return panel;
            }

            handler.Recycle();
            return null;
        }

        internal void OpenPanelAsyncInternal(Type type, UILevel level, IUIData data, Action<IPanel> callback)
        {
            if (TryGetCachedHandler(type, out var handler))
            {
                handler.Data = data;
                handler.Hot += OpenHot;
                OpenAndShowPanelInternal(handler.Panel, data);
                callback?.Invoke(handler.Panel);
                return;
            }

            handler = PanelHandler.Allocate();
            handler.Type = type;
            handler.Level = level;
            handler.Data = data;

            LoadPanelAsync(handler, panel =>
            {
                if (panel != default && panel.Transform != default)
                {
                    SetupPanelInternal(handler, panel);
                    OpenAndShowPanelInternal(panel, data);
                    callback?.Invoke(panel);
                }
                else
                {
                    handler.Recycle();
                    callback?.Invoke(null);
                }
            });
        }

        private void SetupPanelInternal(PanelHandler handler, IPanel panel)
        {
            panel.Transform.gameObject.name = handler.Type.Name;
            AddToOpenedCache(handler.Type, handler);
            handler.Hot += OpenHot;
            panel.Init(handler.Data);
            RegisterPanelToLevel(panel);
        }

        private void OpenAndShowPanelInternal(IPanel panel, IUIData data)
        {
            if (panel == default) return;
            panel.Open(data);
            panel.Show();
        }

        internal void ClosePanelInternal(IPanel panel)
        {
            if (panel == default) return;

            var unityObj = panel as UnityEngine.Object;
            if (unityObj == default)
            {
                if (panel.Handler != default)
                {
                    RemoveFromStack(panel);
                    UnregisterPanelFromLevel(panel);
                    RemoveFromOpenedCache(panel.Handler.Type);
                    panel.Handler.Recycle();
                }
                return;
            }

            panel.Close();
            if (panel.Handler == default) return;

            RemoveFromStack(panel);
            UnregisterPanelFromLevel(panel);
            OnPanelCloseFocus(panel);

            // 根据 CacheMode 决策是否销毁
            if (ShouldDestroyOnClose(panel.Handler))
            {
                DestroyPanelInternal(panel);
                RemoveFromOpenedCache(panel.Handler.Type);
                panel.Handler.Recycle();
            }
        }

        internal void DestroyPanelInternal(IPanel panel)
        {
            if (panel != default && panel.Transform != default && panel.Transform.gameObject != default)
            {
                panel.Cleanup();
                Destroy(panel.Transform.gameObject);
            }
        }

        #endregion
    }
}
