#if UNITY_EDITOR || GODOT
using System;
using System.Collections.Generic;
#if UNITY_EDITOR_WIN || GODOT
using System.IO.MemoryMappedFiles;
#endif
#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
using System.Runtime.InteropServices;
#endif
using System.Threading;
using YokiFrame;

namespace YokiFrame
{
    /// <summary>
    /// Adapter 层共用的共享内存 telemetry 写入器。Base 只保存帧格式，平台 API 留在 Adapter 层。
    /// </summary>
    public static class AdapterSharedMemoryTelemetry
    {
        /// <summary>
        /// 默认共享内存遥测载荷容量。
        /// </summary>
        public const int DEFAULT_PAYLOAD_CAPACITY = 64 * 1024;

        /// <summary>
        /// 默认共享内存遥测载荷容量。
        /// </summary>
        public static int DefaultPayloadCapacity => DEFAULT_PAYLOAD_CAPACITY;

        private const ulong FNV_OFFSET_BASIS = 14695981039346656037UL;
        private const ulong FNV_PRIME = 1099511628211UL;

        private static readonly Dictionary<string, ISharedMemoryTelemetryChannel> sChannels =
            new Dictionary<string, ISharedMemoryTelemetryChannel>();
        private static readonly Dictionary<string, SharedMemoryTelemetryFrame> sFrames =
            new Dictionary<string, SharedMemoryTelemetryFrame>();

        public static string ChannelName(string engineId, string kit, string name)
        {
            EnsureIdentifier(engineId, nameof(engineId));
            EnsureIdentifier(kit, nameof(kit));
            EnsureIdentifier(name, nameof(name));

            return "YokiFrame.Telemetry." + engineId + "." + kit + "." + name + ".v1";
        }

        public static bool TryWriteLatest(string engineId, string kit, string name, string payloadJson)
        {
            try
            {
                WriteLatest(engineId, kit, name, payloadJson, DEFAULT_PAYLOAD_CAPACITY);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static void WriteLatest(string engineId, string kit, string name, string payloadJson, int payloadCapacity)
        {
            if (payloadCapacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(payloadCapacity));

            var channelName = ChannelName(engineId, kit, name);
            var frame = GetOrCreateFrame(channelName, payloadCapacity);
            frame.WriteLatest(engineId, kit, name, payloadJson);

            ulong finalRawSequence;
            var bytes = frame.CreateWriteInProgressCopy(out finalRawSequence);
            var channel = GetOrCreateChannel(channelName, bytes.Length);
            channel.Write(bytes, finalRawSequence);
        }

        public static string PosixChannelName(string logicalChannelName)
        {
            if (string.IsNullOrEmpty(logicalChannelName))
                throw new ArgumentException("Telemetry channel name cannot be empty.", nameof(logicalChannelName));

            return "/YKT." + Fnv1A64Hex(logicalChannelName) + ".v1";
        }

        public static void ResetForTests()
        {
            foreach (var channel in sChannels.Values)
                channel.Dispose();

            sChannels.Clear();
            sFrames.Clear();
        }

        private static SharedMemoryTelemetryFrame GetOrCreateFrame(string channelName, int payloadCapacity)
        {
            SharedMemoryTelemetryFrame frame;
            if (sFrames.TryGetValue(channelName, out frame))
                return frame;

            frame = new SharedMemoryTelemetryFrame(payloadCapacity);
            sFrames[channelName] = frame;
            return frame;
        }

        private static ISharedMemoryTelemetryChannel GetOrCreateChannel(string channelName, int byteLength)
        {
            ISharedMemoryTelemetryChannel channel;
            if (sChannels.TryGetValue(channelName, out channel))
                return channel;

            channel = CreateChannel(channelName, byteLength);
            sChannels[channelName] = channel;
            return channel;
        }

        private static ISharedMemoryTelemetryChannel CreateChannel(string channelName, int byteLength)
        {
#if UNITY_EDITOR_WIN || GODOT
            if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                return new WindowsSharedMemoryTelemetryChannel(channelName, byteLength);
#endif

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
            return new PosixSharedMemoryTelemetryChannel(channelName, byteLength);
#else
            throw new PlatformNotSupportedException("Shared memory telemetry is not supported on this adapter platform.");
#endif
        }

        private static void EnsureIdentifier(string value, string name)
        {
            if (!CommandBridgeProtocol.IsSafeIdentifier(value))
                throw new ArgumentException("Invalid telemetry identifier: " + value, name);
        }

        private static string Fnv1A64Hex(string value)
        {
            var hash = FNV_OFFSET_BASIS;
            for (var i = 0; i < value.Length; i++)
            {
                var ch = value[i];
                hash ^= (byte)(ch & 0xFF);
                hash *= FNV_PRIME;
                hash ^= (byte)(ch >> 8);
                hash *= FNV_PRIME;
            }

            return hash.ToString("x16");
        }

        private static void WriteUInt64LittleEndian(byte[] buffer, int offset, ulong value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
            buffer[offset + 4] = (byte)(value >> 32);
            buffer[offset + 5] = (byte)(value >> 40);
            buffer[offset + 6] = (byte)(value >> 48);
            buffer[offset + 7] = (byte)(value >> 56);
        }

        private interface ISharedMemoryTelemetryChannel : IDisposable
        {
            void Write(byte[] bytes, ulong finalRawSequence);
        }

#if UNITY_EDITOR_WIN || GODOT
        private sealed class WindowsSharedMemoryTelemetryChannel : ISharedMemoryTelemetryChannel
        {
            private readonly MemoryMappedFile mMap;
            private readonly MemoryMappedViewAccessor mView;

            public WindowsSharedMemoryTelemetryChannel(string channelName, int byteLength)
            {
                mMap = MemoryMappedFile.CreateOrOpen(channelName, byteLength);
                mView = mMap.CreateViewAccessor(0, byteLength, MemoryMappedFileAccess.Write);
            }

            public void Write(byte[] bytes, ulong finalRawSequence)
            {
                mView.WriteArray(0, bytes, 0, bytes.Length);
                Thread.MemoryBarrier();

                var sequenceBytes = new byte[sizeof(ulong)];
                WriteUInt64LittleEndian(sequenceBytes, 0, finalRawSequence);
                mView.WriteArray(SharedMemoryTelemetryFrame.SEQUENCE_OFFSET, sequenceBytes, 0, sequenceBytes.Length);
                mView.Flush();
            }

            public void Dispose()
            {
                mView.Dispose();
                mMap.Dispose();
            }
        }
#endif

#if UNITY_EDITOR_OSX || UNITY_EDITOR_LINUX
        private sealed class PosixSharedMemoryTelemetryChannel : ISharedMemoryTelemetryChannel
        {
#if UNITY_EDITOR_OSX
            private const int O_CREAT = 0x0200;
            private const string POSIX_LIBRARY = "libSystem.dylib";
#else
            private const int O_CREAT = 0x0040;
            private const string POSIX_LIBRARY = "libc";
#endif
            private const int O_RDWR = 0x0002;
            private const int PROT_READ = 0x1;
            private const int PROT_WRITE = 0x2;
            private const int MAP_SHARED = 0x01;
            private const uint MODE_USER_READ_WRITE = 384; // 0600
            private static readonly IntPtr MAP_FAILED = new IntPtr(-1);

            private readonly string mNativeName;
            private readonly int mByteLength;
            private int mFileDescriptor;
            private IntPtr mAddress;

            public PosixSharedMemoryTelemetryChannel(string channelName, int byteLength)
            {
                mNativeName = PosixChannelName(channelName);
                mByteLength = byteLength;
                mFileDescriptor = shm_open(mNativeName, O_CREAT | O_RDWR, MODE_USER_READ_WRITE);
                if (mFileDescriptor < 0)
                    ThrowLastError("shm_open");

                if (ftruncate(mFileDescriptor, byteLength) != 0)
                    ThrowLastError("ftruncate");

                mAddress = mmap(IntPtr.Zero, (UIntPtr)byteLength, PROT_READ | PROT_WRITE, MAP_SHARED, mFileDescriptor, IntPtr.Zero);
                if (mAddress == MAP_FAILED)
                    ThrowLastError("mmap");
            }

            public void Write(byte[] bytes, ulong finalRawSequence)
            {
                Marshal.Copy(bytes, 0, mAddress, bytes.Length);
                Thread.MemoryBarrier();

                var sequenceBytes = new byte[sizeof(ulong)];
                WriteUInt64LittleEndian(sequenceBytes, 0, finalRawSequence);
                Marshal.Copy(sequenceBytes, 0, IntPtr.Add(mAddress, SharedMemoryTelemetryFrame.SEQUENCE_OFFSET), sequenceBytes.Length);
            }

            public void Dispose()
            {
                if (mAddress != IntPtr.Zero && mAddress != MAP_FAILED)
                {
                    munmap(mAddress, (UIntPtr)mByteLength);
                    mAddress = IntPtr.Zero;
                }

                if (mFileDescriptor >= 0)
                {
                    close(mFileDescriptor);
                    mFileDescriptor = -1;
                }

                shm_unlink(mNativeName);
            }

            private static void ThrowLastError(string operation)
            {
                throw new InvalidOperationException(operation + " failed with errno " + Marshal.GetLastWin32Error());
            }

            [DllImport(POSIX_LIBRARY, SetLastError = true)]
            private static extern int shm_open(string name, int oflag, uint mode);

            [DllImport(POSIX_LIBRARY, SetLastError = true)]
            private static extern int ftruncate(int fd, long length);

            [DllImport(POSIX_LIBRARY, SetLastError = true)]
            private static extern IntPtr mmap(IntPtr addr, UIntPtr length, int prot, int flags, int fd, IntPtr offset);

            [DllImport(POSIX_LIBRARY, SetLastError = true)]
            private static extern int munmap(IntPtr addr, UIntPtr length);

            [DllImport(POSIX_LIBRARY, SetLastError = true)]
            private static extern int close(int fd);

            [DllImport(POSIX_LIBRARY, SetLastError = true)]
            private static extern int shm_unlink(string name);
        }
#endif
    }
}
#endif
