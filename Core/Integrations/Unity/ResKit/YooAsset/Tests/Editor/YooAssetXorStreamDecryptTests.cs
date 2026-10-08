#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_3
using System.IO;
using NUnit.Framework;
using YokiFrame.Unity;

namespace YokiFrame.Unity.Tests
{
    /// <summary>
    /// 守护 V3 XOR 解密流在单字节读取路径上的解密覆盖。
    /// FileStream.ReadByte 不会虚回调 Read(byte[],int,int)，因此未覆写该路径会直接返回密文。
    /// </summary>
    public sealed class YooAssetXorStreamDecryptTests
    {
        /// <summary>两字节循环密钥，用于验证按绝对位置循环取模。</summary>
        private static readonly byte[] KEY = { 0x0F, 0xF0 };

        private string mPath;

        /// <summary>写入内容可控的临时文件。</summary>
        [SetUp]
        public void SetUp()
        {
            mPath = Path.Combine(Path.GetTempPath(), "yokiframe-xor-stream.bin");
            File.WriteAllBytes(mPath, new byte[] { 0x10, 0x20, 0x30 });
        }

        /// <summary>清理临时文件。</summary>
        [TearDown]
        public void TearDown()
        {
            if (File.Exists(mPath)) File.Delete(mPath);
        }

        /// <summary>直接创建内部解密流。</summary>
        /// <param name="path">目标文件路径。</param>
        /// <param name="key">循环 XOR 密钥。</param>
        /// <returns>可直接按 Stream 使用的解密流。</returns>
        private static Stream CreateStream(string path, byte[] key)
        {
            return new YooAssetXorDecryptStreamV3(path, key);
        }

        /// <summary>验证经典重载按绝对位置循环 XOR 解密。</summary>
        [Test]
        public void Read_循环密钥_按绝对位置解密()
        {
            using Stream stream = CreateStream(mPath, KEY);
            byte[] buffer = new byte[3];

            int read = stream.Read(buffer, 0, 3);

            Assert.AreEqual(3, read);
            Assert.AreEqual(0x10 ^ 0x0F, buffer[0]);
            Assert.AreEqual(0x20 ^ 0xF0, buffer[1]);
            Assert.AreEqual(0x30 ^ 0x0F, buffer[2]);
        }

        /// <summary>
        /// 验证单字节读取必须解密，并在读完后返回 -1。
        /// 未覆写 ReadByte 时该路径会把密文交给调用方。
        /// </summary>
        [Test]
        public void ReadByte_循环密钥_按绝对位置解密并在结束返回负一()
        {
            using Stream stream = CreateStream(mPath, KEY);

            Assert.AreEqual(0x10 ^ 0x0F, stream.ReadByte());
            Assert.AreEqual(0x20 ^ 0xF0, stream.ReadByte());
            Assert.AreEqual(0x30 ^ 0x0F, stream.ReadByte());
            Assert.AreEqual(-1, stream.ReadByte(), "文件读完应返回 -1");
        }
    }
}
#endif
