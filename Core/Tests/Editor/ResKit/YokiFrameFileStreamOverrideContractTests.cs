using System;
using System.IO;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 实证并守护 FileStream 子类的覆写契约：YooAsset XOR 解密流只覆写经典重载是否足以覆盖全部读取路径。
    /// 该契约决定 C38 的修复形态 —— 若 Span 路径会虚回调经典覆写，则额外覆写 Span 会造成双重 XOR。
    /// </summary>
    public sealed class YokiFrameFileStreamOverrideContractTests
    {
        private string mPath;

        /// <summary>创建内容可控的临时文件供各用例复用。</summary>
        [SetUp]
        public void SetUp()
        {
            mPath = Path.Combine(Path.GetTempPath(), "yokiframe-filestream-contract.bin");
            File.WriteAllBytes(mPath, new byte[] { 0x10, 0x20, 0x30, 0x40, 0x50, 0x60, 0x70, 0x08 });
        }

        /// <summary>清理临时文件。</summary>
        [TearDown]
        public void TearDown()
        {
            if (File.Exists(mPath)) File.Delete(mPath);
        }

        /// <summary>记录经典重载被调用次数，并按 0xFF 就地取反以模拟 XOR 解密。</summary>
        private sealed class ByteArrayOnlyStream : FileStream
        {
            /// <summary>经典重载被调用的次数。</summary>
            public int ByteArrayReadCalls;

            /// <summary>打开只读文件。</summary>
            /// <param name="path">目标文件路径。</param>
            public ByteArrayOnlyStream(string path)
                : base(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            {
            }

            /// <summary>镜像 YooAssetXorDecryptStreamV3：只覆写经典重载并就地取反。</summary>
            public override int Read(byte[] array, int offset, int count)
            {
                ByteArrayReadCalls++;
                int read = base.Read(array, offset, count);
                for (var index = 0; index < read; index++) array[offset + index] ^= 0xFF;
                return read;
            }
        }

        /// <summary>在经典重载之外再覆写 Span 重载，用于检验是否发生双重 XOR。</summary>
        private sealed class SpanAndByteArrayStream : FileStream
        {
            /// <summary>经典重载被调用的次数。</summary>
            public int ByteArrayReadCalls;

            /// <summary>Span 重载被调用的次数。</summary>
            public int SpanReadCalls;

            /// <summary>打开只读文件。</summary>
            /// <param name="path">目标文件路径。</param>
            public SpanAndByteArrayStream(string path)
                : base(path, FileMode.Open, FileAccess.Read, FileShare.Read)
            {
            }

            /// <summary>经典重载：就地取反。</summary>
            public override int Read(byte[] array, int offset, int count)
            {
                ByteArrayReadCalls++;
                int read = base.Read(array, offset, count);
                for (var index = 0; index < read; index++) array[offset + index] ^= 0xFF;
                return read;
            }

            /// <summary>Span 重载：先走 base 再取反，用于验证 base 是否已回调经典覆写。</summary>
            public override int Read(Span<byte> buffer)
            {
                SpanReadCalls++;
                int read = base.Read(buffer);
                for (var index = 0; index < read; index++) buffer[index] ^= 0xFF;
                return read;
            }
        }

        /// <summary>基线：经典重载读取会就地取反（模拟解密生效）。</summary>
        [Test]
        public void ClassicOverrideDecryptsBuffer()
        {
            using var stream = new ByteArrayOnlyStream(mPath);
            byte[] buffer = new byte[8];

            int read = stream.Read(buffer, 0, 8);

            Assert.AreEqual(8, read);
            Assert.AreEqual(1, stream.ByteArrayReadCalls);
            Assert.AreEqual(0x10 ^ 0xFF, buffer[0], "经典覆写必须解密首字节");
        }

        /// <summary>
        /// 核心事实一：只覆写经典重载时，Span 读取是否仍会虚回调到该覆写。
        /// 若成立，则 Span 路径已被解密覆盖，无需（且不得）额外覆写 Span。
        /// </summary>
        [Test]
        public void SpanReadReachesClassicOverride()
        {
            using var stream = new ByteArrayOnlyStream(mPath);
            byte[] buffer = new byte[8];

            int read = stream.Read(buffer.AsSpan());

            Assert.AreEqual(8, read);
            Assert.AreEqual(
                1,
                stream.ByteArrayReadCalls,
                "FileStream.Read(Span) 应虚回调 Read(byte[],int,int)，使只覆写经典重载即可覆盖 Span 路径");
        }

        /// <summary>核心事实二：FileStream.ReadByte() 是否会虚回调到经典重载覆写。</summary>
        [Test]
        public void ReadByteDoesNotReachClassicOverride()
        {
            using var stream = new ByteArrayOnlyStream(mPath);

            int value = stream.ReadByte();

            Assert.AreEqual(0x10, value, "未经覆写的 ReadByte 返回的是原始密文字节");
            Assert.AreEqual(
                0,
                stream.ByteArrayReadCalls,
                "FileStream.ReadByte() 不回调经典重载，因此未覆写 ReadByte 的流会返回未解密数据");
        }

        /// <summary>
        /// 核心事实三：同时覆写经典与 Span 重载会在 Span 路径上造成双重取反（等价于双重 XOR）。
        /// </summary>
        [Test]
        public void SpanOverrideTogetherWithClassicOverrideDoubleAppliesXor()
        {
            using var stream = new SpanAndByteArrayStream(mPath);
            byte[] buffer = new byte[8];

            _ = stream.Read(buffer.AsSpan());

            Assert.AreEqual(1, stream.SpanReadCalls, "Span 覆写应被调用");
            Assert.AreEqual(
                1,
                stream.ByteArrayReadCalls,
                "Span 覆写的 base 调用会再次进入经典覆写，这是双重 XOR 的来源");
            Assert.AreEqual(
                0x10,
                buffer[0],
                "两次取反后还原为原文，证明额外覆写 Span 会破坏解密结果");
        }

        /// <summary>核心事实四：异步 byte[] 读取是否仍会虚回调经典覆写（异步加载路径是否已解密）。</summary>
        [Test]
        public void ReadAsyncReachesClassicOverride()
        {
            using var stream = new ByteArrayOnlyStream(mPath);
            byte[] buffer = new byte[8];

            int read = stream.ReadAsync(buffer, 0, 8).GetAwaiter().GetResult();

            Assert.AreEqual(8, read);
            Assert.AreEqual(
                1,
                stream.ByteArrayReadCalls,
                "FileStream.ReadAsync(byte[]) 应虚回调经典覆写，使异步加载路径同样解密");
        }

        /// <summary>核心事实五：异步 Memory 读取是否仍会虚回调经典覆写。</summary>
        [Test]
        public void ReadAsyncMemoryReachesClassicOverride()
        {
            using var stream = new ByteArrayOnlyStream(mPath);
            byte[] buffer = new byte[8];

            int read = stream.ReadAsync(buffer.AsMemory()).GetAwaiter().GetResult();

            Assert.AreEqual(8, read);
            Assert.AreEqual(
                1,
                stream.ByteArrayReadCalls,
                "FileStream.ReadAsync(Memory) 应虚回调经典覆写");
        }
    }
}
