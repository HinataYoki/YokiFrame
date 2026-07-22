#if UNITY_2022_3_OR_NEWER
namespace YokiFrame.Tests
{
    /// <summary>
    /// 为 Play Mode 回归测试提供带稳定层级路径的最小 MonoSingleton 实例。
    /// </summary>
    [MonoSingletonPath("[YokiFrame]/Tests/MonoSingletonSameFrame", false)]
    public sealed class MonoSingletonSameFrameTestComponent : MonoSingleton<MonoSingletonSameFrameTestComponent>
    {
    }
}
#endif
