#if UNITY_EDITOR && UNITY_EDITOR_WIN

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Unity Windows Editor bootstrap 的 Job Object 进程树所有权。
    /// </summary>
    internal static partial class YokiFrameWorkbenchLauncher
    {
        /// <summary>
        /// 用 Windows Job Object 接管 bootstrap 进程树；关闭句柄会由系统终止所有成员进程。
        /// </summary>
        private sealed class WindowsRuntimeBootstrapJob : IRuntimeBootstrapProcessTreeOwner
        {
            private const uint JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE = 0x00002000;
            private const int JOB_OBJECT_EXTENDED_LIMIT_INFORMATION_CLASS = 9;

            private readonly SafeJobHandle mHandle;
            private int mDisposed;

            /// <summary>保存已经配置并接管 bootstrap 进程的 Job Object 句柄。</summary>
            /// <param name="handle">启用了关闭即终止限制的有效句柄。</param>
            private WindowsRuntimeBootstrapJob(SafeJobHandle handle)
            {
                mHandle = handle;
            }

            /// <summary>创建 Job Object、启用关闭即终止限制，并把指定进程加入该 Job。</summary>
            /// <param name="process">已经启动的 bootstrap 根进程。</param>
            /// <param name="error">失败时返回 Win32 或进程生命周期诊断。</param>
            /// <returns>成功接管进程树时返回 owner，否则返回 null。</returns>
            internal static WindowsRuntimeBootstrapJob TryCreate(Process process, out string error)
            {
                IntPtr rawHandle = CreateJobObject(IntPtr.Zero, null);
                if (rawHandle == IntPtr.Zero)
                {
                    error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    return null;
                }

                var handle = new SafeJobHandle(rawHandle);
                var information = new JobObjectExtendedLimitInformation();
                information.BasicLimitInformation.LimitFlags = JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE;
                int informationSize = Marshal.SizeOf(typeof(JobObjectExtendedLimitInformation));
                error = string.Empty;
                if (!SetInformationJobObject(
                        handle,
                        JOB_OBJECT_EXTENDED_LIMIT_INFORMATION_CLASS,
                        ref information,
                        (uint)informationSize)
                    || !TryAssignProcess(handle, process, out error))
                {
                    if (string.IsNullOrEmpty(error))
                        error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    handle.Dispose();
                    return null;
                }

                error = string.Empty;
                return new WindowsRuntimeBootstrapJob(handle);
            }

            /// <summary>关闭 Job Object 句柄，让系统同步提交完整成员树终止。</summary>
            /// <returns>首次关闭或已经关闭时均返回 true。</returns>
            public bool TryTerminate()
            {
                Dispose();
                return true;
            }

            /// <summary>幂等释放 Job Object；正常完成时成员已退出，取消时触发树终止。</summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref mDisposed, 1) == 0) mHandle.Dispose();
            }

            /// <summary>把已启动进程加入 Job Object，并把句柄访问竞态转换为稳定诊断。</summary>
            /// <param name="handle">目标 Job Object。</param>
            /// <param name="process">待接管进程。</param>
            /// <param name="error">失败诊断。</param>
            /// <returns>进程已加入 Job 时返回 true。</returns>
            private static bool TryAssignProcess(SafeJobHandle handle, Process process, out string error)
            {
                try
                {
                    if (AssignProcessToJobObject(handle, process.Handle))
                    {
                        error = string.Empty;
                        return true;
                    }

                    error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
                    return false;
                }
                catch (Exception exception) when (IsExpectedProcessLifecycleException(exception))
                {
                    error = exception.Message;
                    return false;
                }
            }

            /// <summary>创建一个未命名 Windows Job Object。</summary>
            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            private static extern IntPtr CreateJobObject(IntPtr jobAttributes, string name);

            /// <summary>设置 Job Object 的扩展限制信息。</summary>
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool SetInformationJobObject(
                SafeJobHandle job,
                int informationClass,
                ref JobObjectExtendedLimitInformation information,
                uint informationLength);

            /// <summary>把 bootstrap 根进程加入 Job Object。</summary>
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool AssignProcessToJobObject(SafeJobHandle job, IntPtr process);

            /// <summary>关闭 Job Object 原生句柄。</summary>
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool CloseHandle(IntPtr handle);

            [StructLayout(LayoutKind.Sequential)]
            private struct JobObjectBasicLimitInformation
            {
                internal long PerProcessUserTimeLimit;
                internal long PerJobUserTimeLimit;
                internal uint LimitFlags;
                internal UIntPtr MinimumWorkingSetSize;
                internal UIntPtr MaximumWorkingSetSize;
                internal uint ActiveProcessLimit;
                internal UIntPtr Affinity;
                internal uint PriorityClass;
                internal uint SchedulingClass;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct IoCounters
            {
                internal ulong ReadOperationCount;
                internal ulong WriteOperationCount;
                internal ulong OtherOperationCount;
                internal ulong ReadTransferCount;
                internal ulong WriteTransferCount;
                internal ulong OtherTransferCount;
            }

            [StructLayout(LayoutKind.Sequential)]
            private struct JobObjectExtendedLimitInformation
            {
                internal JobObjectBasicLimitInformation BasicLimitInformation;
                internal IoCounters IoInfo;
                internal UIntPtr ProcessMemoryLimit;
                internal UIntPtr JobMemoryLimit;
                internal UIntPtr PeakProcessMemoryUsed;
                internal UIntPtr PeakJobMemoryUsed;
            }

            /// <summary>保证 Job Object 在异常路径和脚本域卸载时仍由 SafeHandle 关闭。</summary>
            private sealed class SafeJobHandle : SafeHandleZeroOrMinusOneIsInvalid
            {
                /// <summary>包装 CreateJobObject 返回的有效句柄。</summary>
                /// <param name="handle">待接管的原生句柄。</param>
                internal SafeJobHandle(IntPtr handle)
                    : base(true)
                {
                    SetHandle(handle);
                }

                /// <summary>由 SafeHandle 终结路径关闭 Job Object。</summary>
                /// <returns>原生句柄成功关闭时返回 true。</returns>
                protected override bool ReleaseHandle()
                {
                    return CloseHandle(handle);
                }
            }
        }
    }
}

#endif
