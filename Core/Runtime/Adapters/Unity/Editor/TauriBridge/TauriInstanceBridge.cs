#if !GODOT
using System;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using UnityEngine;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Tauri 实例桥接 — env 注入常量 + 工具方法。
    ///
    /// v2（文件 I/O 版本）：不再需要 WS 端口扫描/发现文件。
    /// 保留 HTTP 端口扫描（docs 服务）和 owner 窗口绑定。
    ///
    /// 协议常量必须与 Rust 端 main.rs 严格一致。
    /// </summary>
    internal static class TauriInstanceBridge
    {
        /// <summary>env：HTTP 基础端口（仅 docs 服务，非通信关键）</summary>
        internal const string ENV_HTTP_PORT = "YOKI_HTTP_PORT";
        /// <summary>env：Unity 主窗口 HWND，供 Tauri 设 owner 子窗口（仅 Windows）</summary>
        internal const string ENV_OWNER_HWND = "YOKI_OWNER_HWND";
        /// <summary>env：前端 dist 目录绝对路径</summary>
        internal const string ENV_DIST_PATH = "YOKI_DIST_PATH";
        /// <summary>env：.yokiframe 目录绝对路径（Tauri 文件监视器使用）</summary>
        internal const string ENV_YOKIFRAME_DIR = "YOKI_YOKIFRAME_DIR";

        private const int PORT_SCAN_RANGE = 100;
#if UNITY_EDITOR_WIN
        private const uint GW_OWNER = 4;

        private delegate bool EnumWindowsProc(IntPtr hwnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hwnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindow(IntPtr hwnd, uint command);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
#endif

        /// <summary>
        /// 从 basePort 向上扫描第一个空闲 TCP 端口（仅 127.0.0.1）。
        /// 返回 -1 表示范围内无空闲端口。
        /// </summary>
        internal static int ScanFreePort(int basePort)
        {
            for (var offset = 0; offset < PORT_SCAN_RANGE; offset++)
            {
                var port = basePort + offset;
                if (port > ushort.MaxValue)
                    break;
                if (IsPortFree(port))
                    return port;
            }
            return -1;
        }

        private static bool IsPortFree(int port)
        {
            TcpListener listener = default;
            try
            {
                listener = new TcpListener(IPAddress.Loopback, port);
                listener.Start();
                return true;
            }
            catch (SocketException)
            {
                return false;
            }
            finally
            {
                listener?.Stop();
            }
        }

        /// <summary>
        /// 取 Unity 主窗口 HWND。非 Windows / 取不到返回 0。
        /// </summary>
        internal static long GetUnityMainWindowHwnd()
        {
#if UNITY_EDITOR_WIN
            try
            {
                var process = Process.GetCurrentProcess();
                var handle = FindBestUnityTopLevelWindow(process.Id);
                if (handle == IntPtr.Zero)
                    handle = process.MainWindowHandle;
                if (handle == IntPtr.Zero)
                    LogKit.Warning("[TauriInstanceBridge] 未找到 Unity 主窗口 HWND，Tauri 将按独立窗口启动");
                return handle.ToInt64();
            }
            catch (Exception e)
            {
                LogKit.Warning($"[TauriInstanceBridge] 取 Unity HWND 失败: {e.Message}");
                return 0;
            }
#else
            return 0;
#endif
        }

#if UNITY_EDITOR_WIN
        private static IntPtr FindBestUnityTopLevelWindow(int processId)
        {
            var bestHwnd = IntPtr.Zero;
            long bestArea = 0;

            EnumWindows((hwnd, _) =>
            {
                GetWindowThreadProcessId(hwnd, out var windowProcessId);
                if (windowProcessId != processId)
                    return true;
                if (!IsWindowVisible(hwnd))
                    return true;
                if (GetWindow(hwnd, GW_OWNER) != IntPtr.Zero)
                    return true;
                if (!GetWindowRect(hwnd, out var rect))
                    return true;

                var width = rect.Right - rect.Left;
                var height = rect.Bottom - rect.Top;
                if (width <= 0 || height <= 0)
                    return true;

                var area = (long)width * height;
                if (area > bestArea)
                {
                    bestArea = area;
                    bestHwnd = hwnd;
                }

                return true;
            }, IntPtr.Zero);

            return bestHwnd;
        }
#endif
    }
}
#endif
