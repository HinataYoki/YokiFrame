#if UNITY_EDITOR

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// Unity Editor 的 Shared Memory Telemetry 写入门面：只保留平台门控、Unity 生命周期钩子和告警日志，
    /// 帧写入协议全部委托给跨宿主共享的 <see cref="YokiFrameSharedMemoryTelemetryFrameWriter"/>。
    /// </summary>
    internal static class YokiFrameEditorTelemetryWriter
    {
        private static YokiFrameSharedMemoryTelemetryFrameWriter sWriter;

        /// <summary>
        /// 获取当前 Editor 是否已经建立项目级 telemetry 变化通知。
        /// </summary>
        public static bool IsNotificationReady
        {
            get { return sWriter != null && sWriter.IsNotificationReady; }
        }

        /// <summary>
        /// 注册 Editor 生命周期清理，避免 Domain Reload 前保留旧 accessor，并尽力建立变化通知。
        /// </summary>
        public static void RegisterLifecycleHooks()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= Dispose;
            AssemblyReloadEvents.beforeAssemblyReload += Dispose;
            EditorApplication.quitting -= Dispose;
            EditorApplication.quitting += Dispose;
            if (Application.platform == RuntimePlatform.WindowsEditor)
            {
                Writer.OpenNotification();
            }
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

            Writer.WriteFrame(kit, name, payloadJson, generation, sequence);
        }

        /// <summary>
        /// 释放指定 Kit 已不再活动的命名 Telemetry map，避免频繁创建 FSM 时累积 64 KiB 段。
        /// </summary>
        /// <param name="kit">命名 Telemetry 所属 Kit。</param>
        /// <param name="activeNames">本轮仍活动的安全名称。</param>
        public static void RetainNamedStates(string kit, IReadOnlyList<string> activeNames)
        {
            if (sWriter == null)
            {
                return;
            }

            sWriter.RetainNamedStates(kit, activeNames);
        }

        /// <summary>
        /// 释放当前持有的 shared memory 资源；下一次写入会重新惰性创建写入器。
        /// </summary>
        public static void Dispose()
        {
            if (sWriter == null)
            {
                return;
            }

            sWriter.Dispose();
            sWriter = null;
        }

        /// <summary>惰性获取共享帧写入器；通知失败经 Debug.LogWarning 报告一次。</summary>
        private static YokiFrameSharedMemoryTelemetryFrameWriter Writer
        {
            get
            {
                if (sWriter == null)
                {
                    sWriter = new YokiFrameSharedMemoryTelemetryFrameWriter(
                        YokiFrameEditorFileBridgePaths.GetProjectRoot(),
                        YokiFrameEditorFileBridgePaths.ENGINE_ID,
                        message => Debug.LogWarning(message));
                }

                return sWriter;
            }
        }
    }
}

#endif
