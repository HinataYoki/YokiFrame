#if UNITY_INCLUDE_TESTS && UNITY_EDITOR && UNITY_2022_3_OR_NEWER && YOKIFRAME_NINO_SUPPORT
using Nino.Core;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>覆盖 Nino Integration 的独立二进制编解码边界。</summary>
    public sealed class NinoSaveSerializerTests
    {
        /// <summary>验证 Nino 模块可以跨实例往返。</summary>
        [Test]
        public void SerializeDeserialize_RoundTripsModule()
        {
            var serializer = new NinoSaveSerializer();
            var source = new NinoModule { Level = 12 };

            var bytes = serializer.Serialize(source);
            var restored = serializer.Deserialize<NinoModule>(bytes);

            Assert.IsNotNull(bytes);
            Assert.Greater(bytes.Length, 0);
            Assert.AreEqual(12, restored.Level);
        }

        /// <summary>验证 Nino overwrite 入口由 Nino 后端负责。</summary>
        [Test]
        public void DeserializeOverwrite_UpdatesExistingModule()
        {
            var serializer = new NinoSaveSerializer();
            var bytes = serializer.Serialize(new NinoModule { Level = 33 });
            var target = new NinoModule { Level = 1 };

            serializer.DeserializeOverwrite(bytes, target);

            Assert.AreEqual(33, target.Level);
        }

        /// <summary>Nino 测试模块。</summary>
        [NinoType(false)]
        public sealed class NinoModule
        {
            /// <summary>测试等级字段。</summary>
            [NinoMember(0)]
            public int Level;
        }
    }
}
#endif
