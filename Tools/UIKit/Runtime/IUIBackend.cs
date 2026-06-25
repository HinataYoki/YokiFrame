#if !GODOT
namespace YokiFrame
{
    public interface IUIBackend
    {
        string BackendName { get; }

        IPanel OpenPanel(UIOpenRequest request);

        void Show(IPanel panel);

        void Hide(IPanel panel);

        void Close(IPanel panel);
    }
}
#endif
