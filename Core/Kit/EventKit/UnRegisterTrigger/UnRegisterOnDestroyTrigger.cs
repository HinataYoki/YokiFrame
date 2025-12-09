namespace YokiFrame
{
    public class UnRegisterOnDestroyTrigger : UnRegisterTrigger<IUnRegister>
    {
        private void OnDestroy() => UnRegister();
    }
}