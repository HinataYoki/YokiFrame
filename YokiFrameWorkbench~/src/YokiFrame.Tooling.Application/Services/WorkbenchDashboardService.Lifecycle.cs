using YokiFrame.Client;
using YokiFrame.Client.Telemetry.SharedMemory;
using YokiFrame.Tooling.Application.Engines;
using YokiFrame.Tooling.Application.Services.LogKit;
using YokiFrame.Tooling.Application.Services.Settings;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 Dashboard 共享 Client 的创建、所有权和释放生命周期。</summary>
public sealed partial class WorkbenchDashboardService
{
    private readonly IYokiFrameClient mClient;
    private readonly CommandExecutionService mCommandExecutionService;
    private readonly WorkbenchDoctorService mDoctorService;
    private readonly EngineSelectionService mEngineSelectionService;
    private readonly EngineSessionCoordinator mEngineSessionCoordinator;
    private readonly YokiFrameProjectSettingsStore mProjectSettingsStore;
    private readonly LogKitRuntimeSettingsService mLogKitRuntimeSettingsService;
    private readonly IDisposable? mOwnedClient;

    /// <summary>使用项目根目录创建并拥有 Workbench Client。</summary>
    /// <param name="projectRoot">Unity/Godot 项目根目录。</param>
    public WorkbenchDashboardService(string projectRoot)
        : this(new YokiFrameClient(projectRoot), true)
    {
    }

    /// <summary>使用外部 Client 创建服务，但不接管调用方生命周期。</summary>
    /// <param name="client">统一 YokiFrame Client。</param>
    public WorkbenchDashboardService(IYokiFrameClient client)
        : this(client, false)
    {
    }

    /// <summary>创建服务并明确 Client 所有权，避免测试注入对象被越权关闭。</summary>
    private WorkbenchDashboardService(IYokiFrameClient client, bool ownsClient)
    {
        mClient = client;
        mOwnedClient = ownsClient ? client as IDisposable : null;
        mCommandExecutionService = new CommandExecutionService(client);
        mDoctorService = new WorkbenchDoctorService(client);
        mEngineSelectionService = new EngineSelectionService(client);
        mEngineSessionCoordinator = new EngineSessionCoordinator(client);
        mProjectSettingsStore = new YokiFrameProjectSettingsStore(client.Paths.ProjectRoot);
        mLogKitRuntimeSettingsService = new LogKitRuntimeSettingsService(mProjectSettingsStore);
    }

    /// <summary>获取绑定当前项目的统一项目配置 Store，供其它页面服务复用。</summary>
    public YokiFrameProjectSettingsStore ProjectSettingsStore => mProjectSettingsStore;

    /// <summary>创建复用当前 Client 的宿主生命周期监视器。</summary>
    /// <returns>与 Dashboard 使用同一客户端边界的监视器。</returns>
    public EngineLifecycleMonitor CreateLifecycleMonitor()
    {
        return new EngineLifecycleMonitor(mClient);
    }

    /// <summary>
    /// 尝试创建当前项目和 engine 的 Shared Memory telemetry 通知 listener。
    /// </summary>
    /// <param name="engineId">目标 engine 标识。</param>
    /// <returns>宿主已发布通知且平台支持时返回 listener，否则为空。</returns>
    public SharedMemoryTelemetryNotificationListener? CreateTelemetryNotificationListener(string engineId)
    {
        return mClient.CreateTelemetryNotificationListener(engineId);
    }

    /// <summary>只释放由项目根构造路径创建的 Client。</summary>
    public void Dispose()
    {
        mOwnedClient?.Dispose();
    }
}
