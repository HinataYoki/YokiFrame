#if UNITY_2022_3_OR_NEWER
namespace YokiFrame
{
    /// <summary>
    /// 为编辑模式适配测试提供可挂载的具体 MonoSingleton。Editor 目录中的脚本不能加入 GameObject。
    /// </summary>
    public sealed class SingletonKitAdapterProbe : MonoSingleton<SingletonKitAdapterProbe>
    {
    }
}
#endif
