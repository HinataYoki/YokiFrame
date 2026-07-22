using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Installer.Core.Tests.Unity;

/// <summary>
/// 通过回调观察 Unity 来源切换检查点，并在指定阶段修改测试磁盘状态。
/// </summary>
internal sealed class CallbackUnityInstallFaultInjector : IUnityInstallFaultInjector
{
    private readonly Action<UnityInstallCheckpoint> mCallback;

    /// <summary>
    /// 创建回调式 Unity 安装故障注入器。
    /// </summary>
    /// <param name="callback">每个 Unity 安装检查点调用的测试回调。</param>
    internal CallbackUnityInstallFaultInjector(Action<UnityInstallCheckpoint> callback)
    {
        mCallback = callback;
    }

    /// <summary>
    /// 把当前检查点交给测试回调，由测试决定观察或篡改磁盘状态。
    /// </summary>
    /// <param name="checkpoint">当前 Unity 安装检查点。</param>
    public void OnCheckpoint(UnityInstallCheckpoint checkpoint)
    {
        mCallback(checkpoint);
    }
}
