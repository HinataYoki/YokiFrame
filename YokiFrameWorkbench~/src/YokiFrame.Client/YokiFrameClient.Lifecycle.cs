namespace YokiFrame.Client;

/// <summary>承载统一 Client 的公开入口门禁与资源释放生命周期。</summary>
public sealed partial class YokiFrameClient
{
    /// <summary>在线程安全门禁内拒绝客户端释放后的所有公开操作。</summary>
    private void ThrowIfDisposed()
    {
        lock (mTelemetryTargetsGate)
        {
            ThrowIfDisposedUnderGate();
        }
    }

    /// <summary>调用方已持有生命周期门禁时执行无嵌套锁的释放状态检查。</summary>
    private void ThrowIfDisposedUnderGate()
    {
        ObjectDisposedException.ThrowIf(mDisposed, this);
    }

    /// <summary>
    /// 释放 Client 直接持有的 Shared Memory map/accessor 与 FastChannel 连接；Workbench 与 CLI 结束时必须调用。
    /// </summary>
    public void Dispose()
    {
        lock (mTelemetryTargetsGate)
        {
            if (mDisposed)
            {
                return;
            }

            mDisposed = true;
            List<Exception>? failures = null;
            try
            {
                ClearTelemetryTargets();
            }
            catch (Exception exception)
            {
                failures = new List<Exception> { exception };
            }

            try
            {
                mFastChannelCommandTransport.Dispose();
            }
            catch (Exception exception)
            {
                failures ??= new List<Exception>();
                failures.Add(exception);
            }

            if (failures != null)
            {
                throw new AggregateException("YokiFrame Client resources could not be fully disposed.", failures);
            }
        }
    }
}
