const assert = require('node:assert/strict');
const test = require('node:test');

const diagnostics = require('./bridge-diagnostics.js');

test('summarizeStatus selects connected engine identity and heartbeat path', () => {
    const summary = diagnostics.summarizeStatus({
        connected: true,
        engines: [
            {
                engineId: 'unity-editor',
                engine: 'Unity',
                version: '6000.6.0a2',
                connected: true,
                heartbeatPath: 'F:/YokiFrame2/.yokiframe/engines/unity-editor/status/heartbeat.json'
            }
        ]
    });

    assert.equal(summary.connected, true);
    assert.equal(summary.engineId, 'unity-editor');
    assert.equal(summary.engineLabel, 'Unity 6000.6.0a2');
    assert.equal(summary.heartbeatPath, 'F:/YokiFrame2/.yokiframe/engines/unity-editor/status/heartbeat.json');
});

test('summarizeBridgeStatus exposes queue, storage, backpressure, and error fields', () => {
    const summary = diagnostics.summarizeBridgeStatus({
        pendingCommandCount: 2,
        processingCommandCount: 1,
        deadletterCommandCount: 3,
        resultCount: 4,
        protocolFileCount: 12,
        protocolBytes: 4096,
        oldestProtocolFileUtc: '2026-06-20T11:50:00.0000000Z',
        bridgeBusyCount: 5,
        backpressureActive: true,
        lastPollLimitReason: 'MaxPendingCommands',
        lastError: 'Bridge busy'
    });

    assert.equal(summary.queueLabel, '2 pending / 1 processing');
    assert.equal(summary.deadletterLabel, '3');
    assert.equal(summary.resultLabel, '4');
    assert.equal(summary.storageLabel, '12 files / 4 KB');
    assert.equal(summary.oldestFileLabel, '2026-06-20T11:50:00.0000000Z');
    assert.equal(summary.backpressureLabel, 'Active: MaxPendingCommands');
    assert.equal(summary.bridgeBusyLabel, '5');
    assert.equal(summary.lastErrorLabel, 'Bridge busy');
});

test('summarizePing records request identity and failure reason', () => {
    const summary = diagnostics.summarizePing({
        protocolVersion: 2,
        requestId: 'req-001',
        engineId: 'unity-editor',
        kit: 'System',
        action: 'ping_response',
        status: 'error',
        error: { code: 'BridgeBusy', message: 'Command bridge is busy' }
    });

    assert.equal(summary.statusLabel, 'error');
    assert.equal(summary.requestId, 'req-001');
    assert.equal(summary.engineId, 'unity-editor');
    assert.equal(summary.reason, 'BridgeBusy: Command bridge is busy');
});

test('extractFsmSnapshotList reads fsms from snapshot envelope data', () => {
    const fsms = diagnostics.extractFsmSnapshotList({
        protocolVersion: 2,
        engineId: 'unity-editor',
        kit: 'FsmKit',
        snapshot: 'state',
        data: {
            fsms: [
                { name: 'PlayerFSM', machineState: 'Running' }
            ],
            count: 1
        }
    });

    assert.deepEqual(fsms, [
        { name: 'PlayerFSM', machineState: 'Running' }
    ]);
});

test('extractFsmSnapshotList reads fsms from unwrapped snapshot data', () => {
    const fsms = diagnostics.extractFsmSnapshotList({
        fsms: [
            { name: 'EnemyFSM', machineState: 'Running' }
        ],
        count: 1
    });

    assert.deepEqual(fsms, [
        { name: 'EnemyFSM', machineState: 'Running' }
    ]);
});
