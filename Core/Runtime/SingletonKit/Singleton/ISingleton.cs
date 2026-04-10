namespace YokiFrame
{
    /// <summary>
    /// Common contract for all YokiFrame singleton instances.
    /// </summary>
    public interface ISingleton
    {
        /// <summary>
        /// Called once after the singleton instance has been created.
        /// </summary>
        void OnSingletonInit();
    }
}
