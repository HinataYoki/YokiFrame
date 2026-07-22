#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 在 Unity Editor 中写入最小 Shared Memory Telemetry 帧。
    /// </summary>
    internal static class YokiFrameEditorTelemetryWriter
    {
        private static readonly UTF8Encoding sUtf8 = new UTF8Encoding(false);
        private static readonly ulong sEngineIdHash = YokiFrameSharedMemoryTelemetryEngineIdHash.Compute(
            YokiFrameEditorFileBridgePaths.ENGINE_ID);
        private static readonly string sProjectScopeId = YokiFrameSharedMemoryTelemetryProjectScopeId.Compute(
            YokiFrameEditorFileBridgePaths.GetProjectRoot());
        private static readonly Dictionary<string, MemoryMappedFile> sStateMaps = new Dictionary<string, MemoryMappedFile>();
        private static readonly Dictionary<string, MemoryMappedViewAccessor> sStateAccessors = new Dictionary<string, MemoryMappedViewAccessor>();
        private static EventWaitHandle sTelemetryNotification;
        private static bool sNotificationWarningLogged;

        /// <summary>
        /// 获取当前 Editor 是否已经建立项目级 telemetry 变化通知。
        /// </summary>
        public static bool IsNotificationReady
        {
            get { return sTelemetryNotification != null; }
        }

        /// <summary>
        /// 注册 Editor 生命周期清理，避免 Domain Reload 前保留旧 accessor。
        /// </summary>
        public static void RegisterLifecycleHooks()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting -= Dispose;
            EditorApplication.quitting += Dispose;
            EnsureTelemetryNotification();
        }

        /// <summary>
        /// 写入指定 Kit 的 state telemetry；非 Windows Editor 环境下保持静默跳过。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="payloadJson">System/state payload JSON。</param>
        /// <param name="generation">当前 engine generation。</param>
        /// <param name="sequence">当前帧序号。</param>
        public static void WriteState(string kit, string payloadJson, long generation, long sequence)
        {
            WriteState(kit, "state", payloadJson, generation, sequence);
        }

        /// <summary>
        /// 写入指定 Kit/name 的 Shared Memory latest frame；用于按实例拆分大 payload。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="name">Provider 声明的安全 Telemetry 名称。</param>
        /// <param name="payloadJson">Kit 自有 schema JSON。</param>
        /// <param name="generation">当前 engine generation。</param>
        /// <param name="sequence">当前帧序号。</param>
        public static void WriteState(
            string kit,
            string name,
            string payloadJson,
            long generation,
            long sequence)
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                return;
            }

            var payloadBytes = sUtf8.GetBytes(payloadJson);
            if (payloadBytes.Length > YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES)
            {
                throw new InvalidOperationException("System telemetry payload exceeds max payload bytes.");
            }

            var accessor = EnsureStateAccessor(kit, name);
            var writtenTicks = DateTimeOffset.UtcNow.UtcTicks;
            var payloadCrc32 = YokiFrameSharedMemoryTelemetryCrc32.Compute(payloadBytes);
            accessor.Write(
                YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET,
                YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_WRITING);
            Thread.MemoryBarrier();
            WriteHeaderFields(accessor, generation, sequence, writtenTicks, payloadBytes.Length, payloadCrc32);
            accessor.WriteArray(
                YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET,
                payloadBytes,
                0,
                payloadBytes.Length);
            Thread.MemoryBarrier();
            accessor.Write(
                YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_OFFSET,
                YokiFrameSharedMemoryTelemetryContract.WRITE_STATE_COMMITTED);
            SignalTelemetryNotification();
        }

        /// <summary>
        /// 释放指定 Kit 已不再活动的命名 Telemetry map，避免频繁创建 FSM 时累积 64 KiB 段。
        /// </summary>
        /// <param name="kit">命名 Telemetry 所属 Kit。</param>
        /// <param name="activeNames">本轮仍活动的安全名称。</param>
        public static void RetainNamedStates(string kit, IReadOnlyList<string> activeNames)
        {
            HashSet<string> retainedKeys = new();
            for (var index = 0; index < activeNames.Count; index++)
            {
                retainedKeys.Add(CreateStateKey(kit, activeNames[index]));
            }

            string standardStateKey = CreateStateKey(kit, "state");
            List<string> staleKeys = new();
            foreach (var key in sStateAccessors.Keys)
            {
                if (key.StartsWith(kit + "\n", StringComparison.Ordinal)
                    && key != standardStateKey
                    && !retainedKeys.Contains(key))
                {
                    staleKeys.Add(key);
                }
            }

            for (var index = 0; index < staleKeys.Count; index++)
            {
                ReleaseState(staleKeys[index]);
            }
        }

        /// <summary>
        /// 释放当前持有的 shared memory 资源。
        /// </summary>
        public static void Dispose()
        {
            foreach (var accessor in sStateAccessors.Values)
            {
                accessor.Dispose();
            }

            foreach (var memoryMap in sStateMaps.Values)
            {
                memoryMap.Dispose();
            }

            sStateAccessors.Clear();
            sStateMaps.Clear();
            sTelemetryNotification?.Dispose();
            sTelemetryNotification = null;
            sNotificationWarningLogged = false;
        }

        /// <summary>
        /// 为当前项目创建自动复位通知事件；失败时保留 Shared Memory 本身并由 Workbench 周期刷新兜底。
        /// </summary>
        private static void EnsureTelemetryNotification()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor || sTelemetryNotification != null)
            {
                return;
            }

            try
            {
                string name = YokiFrameSharedMemoryTelemetryNotificationName.Create(
                    sProjectScopeId,
                    YokiFrameEditorFileBridgePaths.ENGINE_ID);
                sTelemetryNotification = new EventWaitHandle(
                    false,
                    EventResetMode.AutoReset,
                    name,
                    out _);
            }
            catch (Exception exception)
            {
                if (!sNotificationWarningLogged)
                {
                    Debug.LogWarning("YokiFrame telemetry notification unavailable: " + exception.Message);
                    sNotificationWarningLogged = true;
                }
            }
        }

        /// <summary>
        /// 在 committed frame 发布后唤醒当前项目唯一 Workbench；通知失败不影响 telemetry 写入结果。
        /// </summary>
        private static void SignalTelemetryNotification()
        {
            if (sTelemetryNotification == null)
            {
                return;
            }

            try
            {
                sTelemetryNotification.Set();
            }
            catch (Exception exception)
            {
                if (!sNotificationWarningLogged)
                {
                    Debug.LogWarning("YokiFrame telemetry notification failed: " + exception.Message);
                    sNotificationWarningLogged = true;
                }
            }
        }

        /// <summary>
        /// 确保指定 Kit 的 state named memory map 已打开。
        /// </summary>
        /// <param name="kit">Kit 标识。</param>
        /// <returns>已打开的 view accessor。</returns>
        private static MemoryMappedViewAccessor EnsureStateAccessor(string kit, string name)
        {
            string stateKey = CreateStateKey(kit, name);
            if (sStateAccessors.TryGetValue(stateKey, out var existingAccessor))
            {
                return existingAccessor;
            }

            var memoryMap = MemoryMappedFile.CreateOrOpen(
                CreateSegmentName(kit, name),
                YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET
                    + YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES,
                MemoryMappedFileAccess.ReadWrite);
            var accessor = memoryMap.CreateViewAccessor(
                0,
                YokiFrameSharedMemoryTelemetryContract.PAYLOAD_OFFSET
                    + YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES,
                MemoryMappedFileAccess.ReadWrite);
            sStateMaps[stateKey] = memoryMap;
            sStateAccessors[stateKey] = accessor;
            return accessor;
        }

        /// <summary>创建不会与合法 SafeId 冲突的内部 Kit/name 字典键。</summary>
        private static string CreateStateKey(string kit, string name)
        {
            return kit + "\n" + name;
        }

        /// <summary>释放一个内部状态键对应的 accessor 与 memory map。</summary>
        private static void ReleaseState(string stateKey)
        {
            if (sStateAccessors.TryGetValue(stateKey, out var accessor))
            {
                sStateAccessors.Remove(stateKey);
                accessor.Dispose();
            }

            if (sStateMaps.TryGetValue(stateKey, out var memoryMap))
            {
                sStateMaps.Remove(stateKey);
                memoryMap.Dispose();
            }
        }

        /// <summary>
        /// 创建与 .NET 工具侧一致的 telemetry segment 名称。
        /// </summary>
        /// <param name="kit">Kit 名称。</param>
        /// <param name="name">telemetry 名称。</param>
        /// <returns>named memory map segment 名称。</returns>
        private static string CreateSegmentName(string kit, string name)
        {
            return YokiFrameSharedMemoryTelemetrySegmentName.Create(
                sProjectScopeId,
                YokiFrameEditorFileBridgePaths.ENGINE_ID,
                kit,
                name);
        }

        /// <summary>
        /// 写入 telemetry 可变 header 字段；writeState 由调用方独立发布以保证提交顺序。
        /// </summary>
        /// <param name="accessor">目标 shared memory accessor。</param>
        /// <param name="generation">engine generation。</param>
        /// <param name="sequence">帧序号。</param>
        /// <param name="writtenTicks">写入时间 UTC ticks。</param>
        /// <param name="payloadLength">payload 字节数。</param>
        /// <param name="payloadCrc32">payload CRC32。</param>
        private static void WriteHeaderFields(
            MemoryMappedViewAccessor accessor,
            long generation,
            long sequence,
            long writtenTicks,
            int payloadLength,
            uint payloadCrc32)
        {
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.MAGIC_OFFSET, YokiFrameSharedMemoryTelemetryContract.MAGIC);
            accessor.Write(
                YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION_OFFSET,
                YokiFrameSharedMemoryTelemetryContract.PROTOCOL_VERSION);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.ENGINE_ID_HASH_OFFSET, sEngineIdHash);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.GENERATION_OFFSET, generation);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.SEQUENCE_OFFSET, sequence);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.WRITTEN_AT_UTC_TICKS_OFFSET, writtenTicks);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.PAYLOAD_LENGTH_OFFSET, payloadLength);
            accessor.Write(YokiFrameSharedMemoryTelemetryContract.PAYLOAD_CRC32_OFFSET, payloadCrc32);
        }
    }
}

#endif
