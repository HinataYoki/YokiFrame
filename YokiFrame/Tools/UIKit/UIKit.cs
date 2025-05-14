using System;

namespace YokiFrame
{
    public class UIKit
    {
        public UIRoot Root => UIRoot.Instance;

        public static T GetPanel<T>() where T : UIPanel
        {
            var handler = PanelHandler.Allocate();
            handler.Type = typeof(T);

            var panel = UIManager.Instance.GetUI(handler);

            return panel as T;
        }

        public static T OpenPanel<T>(UILevel level = UILevel.Common, IUIData data = null) where T : UIPanel
        {
            var handler = PanelHandler.Allocate();

            handler.Type = typeof(T);
            handler.Level = level;
            handler.Data = data;

            var panel = UIManager.Instance.OpenUI(handler);

            return panel as T;
        }

        public static void OpenPanelAsync<T>(UILevel level = UILevel.Common, IUIData data = null, Action<IPanel> callbaack = null) where T : UIPanel
        {
            var handler = PanelHandler.Allocate();

            handler.Type = typeof(T);
            handler.Level = level;
            handler.Data = data;

            UIManager.Instance.OpenUIAsync(handler, callbaack);
        }

        public static void ShowPanel<T>() where T : UIPanel
        {
            var handler = PanelHandler.Allocate();

            handler.Type = typeof(T);
            UIManager.Instance.ShowUI(handler);
        }

        public static void HidePanel<T>() where T : UIPanel
        {
            var handler = PanelHandler.Allocate();

            handler.Type = typeof(T);
            UIManager.Instance.HideUI(handler);
        }

        public static void HideAllPanel()
        {
            UIManager.Instance.HideAllUI();
        }

        public static void ClosePanel<T>() where T : UIPanel
        {
            var handler = PanelHandler.Allocate();

            handler.Type = typeof(T);

            UIManager.Instance.CloseUI(handler);
        }

        public static void ClosePanel(IPanel panel)
        {
            var handler = PanelHandler.Allocate();

            handler.Type = panel.Handler.Type;

            UIManager.Instance.CloseUI(handler);
        }

        public static void CloseAllPanel()
        {
            UIManager.Instance.CloseAllUI();
        }

        public static void PushOpenPanel<T>() where T : UIPanel
        {
            var handler = PanelHandler.Allocate();
            handler.Type = typeof(T);
            UIManager.Instance.PushOpenUI(handler);
        }

        public static void PushOpenPanelAysnc<T>() where T : UIPanel
        {
            var handler = PanelHandler.Allocate();
            handler.Type = typeof(T);

            UIManager.Instance.PushOpenUIAsync(handler);
        }

        public static void PushPanel<T>() where T : UIPanel
        {
            var handler = PanelHandler.Allocate();
            handler.Type = typeof(T);

            UIManager.Instance.PushUI(handler);
        }

        public static void PushPanel(IPanel panel)
        {
            UIManager.Instance.PushUI(panel);
        }

        public static UIPanel PopPanel()
        {
            var panel = UIManager.Instance.PopUI();
            return panel as UIPanel;
        }

        public static void CloseAllStackPanel()
        {
            UIManager.Instance.CloseAllStackUI();
        }
    }
}