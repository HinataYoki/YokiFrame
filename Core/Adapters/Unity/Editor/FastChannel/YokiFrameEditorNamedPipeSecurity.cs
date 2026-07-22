#if UNITY_EDITOR_WIN

using System;
using System.ComponentModel;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;

namespace YokiFrame
{
    /// <summary>
    /// 为 Unity Editor Mono 创建仅当前 Windows SID 可连接的 Named Pipe；避免依赖 Mono 未实现的 CurrentUserOnly 和 WindowsIdentity.User。
    /// </summary>
    internal static class YokiFrameEditorNamedPipeSecurity
    {
        private const uint TOKEN_QUERY = 0x0008u;
        private const int TOKEN_USER = 1;
        private const int ERROR_INSUFFICIENT_BUFFER = 122;
        private const int SINGLE_SERVER_INSTANCE = 1;
        private const int DEFAULT_BUFFER_SIZE = 0;
        private const PipeAccessRights NO_ADDITIONAL_ACCESS_RIGHTS = (PipeAccessRights)0;

        /// <summary>
        /// 使用受保护的当前用户 DACL 创建异步单实例 Named Pipe；若 Windows token 或 ACL 创建失败则抛出，调用方必须保持 FileBridge 回退。
        /// </summary>
        /// <param name="pipeName">已经通过 SafeId 校验的 Pipe 名称。</param>
        /// <returns>尚未等待客户端连接的安全 Pipe server。</returns>
        public static NamedPipeServerStream CreateServer(string pipeName)
        {
            var security = CreateCurrentUserPipeSecurity();
            return new NamedPipeServerStream(
                pipeName,
                PipeDirection.InOut,
                SINGLE_SERVER_INSTANCE,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous,
                DEFAULT_BUFFER_SIZE,
                DEFAULT_BUFFER_SIZE,
                security,
                HandleInheritability.None,
                NO_ADDITIONAL_ACCESS_RIGHTS);
        }

        /// <summary>
        /// 创建不继承外部规则、且只授予当前进程所属 Windows SID 完全控制权的 PipeSecurity。
        /// </summary>
        /// <returns>可传给十参数 NamedPipeServerStream 构造函数的受保护 ACL。</returns>
        private static PipeSecurity CreateCurrentUserPipeSecurity()
        {
            var currentUserSid = ReadCurrentUserSecurityIdentifier();
            var security = new PipeSecurity();
            security.SetAccessRuleProtection(true, false);
            security.AddAccessRule(new PipeAccessRule(
                currentUserSid,
                PipeAccessRights.FullControl,
                AccessControlType.Allow));
            return security;
        }

        /// <summary>
        /// 从当前 Unity Editor 进程的 Windows access token 读取用户 SID；该路径不触发 Mono 未实现的 WindowsIdentity 属性。
        /// </summary>
        /// <returns>当前进程用户 SID。</returns>
        private static SecurityIdentifier ReadCurrentUserSecurityIdentifier()
        {
            IntPtr tokenHandle;
            if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, out tokenHandle))
            {
                throw CreateWin32Exception("OpenProcessToken");
            }

            try
            {
                return ReadTokenUserSecurityIdentifier(tokenHandle);
            }
            finally
            {
                CloseHandle(tokenHandle);
            }
        }

        /// <summary>
        /// 分配 Windows 返回的 TOKEN_USER 精确缓冲区并转换其中的原生 SID 指针。
        /// </summary>
        /// <param name="tokenHandle">具有 TOKEN_QUERY 权限的当前进程 access token。</param>
        /// <returns>当前 token 所属用户的安全标识符。</returns>
        private static SecurityIdentifier ReadTokenUserSecurityIdentifier(IntPtr tokenHandle)
        {
            int requiredSize;
            if (!GetTokenInformation(tokenHandle, TOKEN_USER, IntPtr.Zero, 0, out requiredSize)
                && Marshal.GetLastWin32Error() != ERROR_INSUFFICIENT_BUFFER)
            {
                throw CreateWin32Exception("GetTokenInformation size query");
            }

            if (requiredSize <= 0)
            {
                throw new InvalidOperationException("GetTokenInformation did not return TOKEN_USER buffer size.");
            }

            var buffer = Marshal.AllocHGlobal(requiredSize);
            try
            {
                if (!GetTokenInformation(tokenHandle, TOKEN_USER, buffer, requiredSize, out requiredSize))
                {
                    throw CreateWin32Exception("GetTokenInformation token user query");
                }

                var tokenUser = (TokenUser)Marshal.PtrToStructure(buffer, typeof(TokenUser));
                if (tokenUser.Sid == IntPtr.Zero)
                {
                    throw new InvalidOperationException("GetTokenInformation returned an empty TOKEN_USER SID.");
                }

                return new SecurityIdentifier(tokenUser.Sid);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }

        /// <summary>
        /// 创建带有当前 Win32 错误码的异常，供 Pump 写入 bridge 状态诊断但不暴露为可用 endpoint。
        /// </summary>
        /// <param name="operation">失败的 Windows API 操作名称。</param>
        /// <returns>保留原生错误码的异常。</returns>
        private static Win32Exception CreateWin32Exception(string operation)
        {
            return new Win32Exception(Marshal.GetLastWin32Error(), operation + " failed.");
        }

        /// <summary>
        /// 表示 TOKEN_USER 返回的 SID_AND_ATTRIBUTES 布局；只读取 SID 指针，属性不参与 ACL 构造。
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct TokenUser
        {
            public IntPtr Sid;
            public uint Attributes;
        }

        /// <summary>
        /// 打开当前 Unity Editor 进程的 access token。
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        /// <summary>
        /// 读取 access token 中指定类型的原生信息。
        /// </summary>
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(
            IntPtr tokenHandle,
            int tokenInformationClass,
            IntPtr tokenInformation,
            int tokenInformationLength,
            out int returnLength);

        /// <summary>
        /// 获取当前 Unity Editor 进程的 Windows 伪句柄。
        /// </summary>
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        /// <summary>
        /// 关闭由 OpenProcessToken 返回的 Windows token 句柄。
        /// </summary>
        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);
    }
}

#endif
