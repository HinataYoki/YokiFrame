#if !GODOT
using System;
using YokiFrame;

namespace YokiFrame.Unity
{
    public sealed class UnityUIBackend : IUIBackend
    {
        public string BackendName
        {
            get { return "Unity.UI"; }
        }

        public IPanel OpenPanel(UIOpenRequest request)
        {
            if (request.PanelType == null || !typeof(IPanel).IsAssignableFrom(request.PanelType))
                return null;

            var panel = Activator.CreateInstance(request.PanelType) as IPanel;
            if (panel == null)
                return null;

            Show(panel);
            return panel;
        }

        public void Show(IPanel panel)
        {
            if (panel != null)
                panel.State = PanelState.Open;
        }

        public void Hide(IPanel panel)
        {
            if (panel != null)
                panel.State = PanelState.Hide;
        }

        public void Close(IPanel panel)
        {
            if (panel != null)
                panel.State = PanelState.Close;
        }
    }
}
#endif
