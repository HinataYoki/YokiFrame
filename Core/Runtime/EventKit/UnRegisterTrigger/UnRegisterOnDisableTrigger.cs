namespace YokiFrame
{
    public class UnRegisterOnDisableTrigger : UnRegisterTrigger<IUnRegister>
    {
        private void OnDisable() => UnRegister();
    }
}