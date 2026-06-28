(function (root, factory) {
    const api = factory();
    if (typeof module === 'object' && module.exports) {
        module.exports = api;
    }
    if (root) {
        root.YokiBridgeDiagnostics = api;
    }
})(typeof window !== 'undefined' ? window : globalThis, function () {
    function firstConnectedEngine(status) {
        const engines = Array.isArray(status?.engines) ? status.engines : [];
        return engines.find(e => e?.connected !== false) ?? engines[0] ?? null;
    }

    function summarizeStatus(status) {
        const engine = firstConnectedEngine(status);
        const version = engine?.version ? ` ${engine.version}` : '';
        return {
            connected: !!status?.connected,
            engineId: engine?.engineId ?? '--',
            engineLabel: engine ? `${engine.engine ?? 'Engine'}${version}` : '--',
            heartbeatPath: engine?.heartbeatPath ?? '--',
            engineCount: Array.isArray(status?.engines) ? status.engines.length : 0
        };
    }

    function summarizeBridgeStatus(data) {
        const pending = numberOrZero(data?.pendingCommandCount);
        const processing = numberOrZero(data?.processingCommandCount);
        const reason = data?.lastPollLimitReason ? `: ${data.lastPollLimitReason}` : '';
        return {
            queueLabel: `${pending} pending / ${processing} processing`,
            deadletterLabel: String(numberOrZero(data?.deadletterCommandCount)),
            resultLabel: String(numberOrZero(data?.resultCount)),
            storageLabel: `${numberOrZero(data?.protocolFileCount)} files / ${formatBytes(numberOrZero(data?.protocolBytes))}`,
            oldestFileLabel: data?.oldestProtocolFileUtc ?? '--',
            backpressureLabel: data?.backpressureActive ? `Active${reason}` : 'Idle',
            bridgeBusyLabel: String(numberOrZero(data?.bridgeBusyCount)),
            lastErrorLabel: data?.lastError ?? '--'
        };
    }

    function summarizePing(response) {
        return {
            statusLabel: response?.status ?? '--',
            requestId: response?.requestId ?? '--',
            engineId: response?.engineId ?? '--',
            reason: formatBridgeError(response)
        };
    }

    function extractFsmSnapshotList(snapshot) {
        const directFsms = snapshot?.fsms;
        if (Array.isArray(directFsms)) return directFsms;

        const envelopeFsms = snapshot?.data?.fsms;
        return Array.isArray(envelopeFsms) ? envelopeFsms : [];
    }

    function formatBridgeError(response) {
        const err = response?.error;
        if (err && typeof err === 'object') {
            const code = err.code ? `${err.code}: ` : '';
            return `${code}${err.message ?? response.errorMessage ?? 'error'}`;
        }
        return err ?? response?.errorMessage ?? '--';
    }

    function formatBytes(value) {
        const bytes = numberOrZero(value);
        if (bytes < 1024) return `${bytes} B`;
        if (bytes < 1024 * 1024) return `${Math.round(bytes / 1024)} KB`;
        return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
    }

    function numberOrZero(value) {
        return typeof value === 'number' && isFinite(value) ? value : 0;
    }

    return {
        formatBridgeError,
        formatBytes,
        extractFsmSnapshotList,
        summarizeStatus,
        summarizeBridgeStatus,
        summarizePing
    };
});
