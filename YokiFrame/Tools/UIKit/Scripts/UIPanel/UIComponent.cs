namespace YokiFrame
{
    public abstract class UIComponent : UIElement
    {
        public override BindType Bind => BindType.Component;
    }
}