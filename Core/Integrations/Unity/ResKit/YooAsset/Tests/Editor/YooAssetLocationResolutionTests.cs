#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using NUnit.Framework;

namespace YokiFrame.Unity.Tests
{
    /// <summary>验证 YooAsset 路径解析的显式包名语法。</summary>
    public sealed class YooAssetLocationResolutionTests
    {
        /// <summary>验证普通路径保持自动探测，不把冒号误判成包名前缀。</summary>
        [Test]
        public void Split_普通路径_保持自动探测()
        {
            YooAssetLocation.Split("Prefabs/Enemy", out string packageName, out string location);

            Assert.IsNull(packageName);
            Assert.AreEqual("Prefabs/Enemy", location);
        }

        /// <summary>验证显式语法只切出包名，剩余部分原样保留。</summary>
        [Test]
        public void Split_显式包前缀_分离包名和位置()
        {
            YooAssetLocation.Split(
                "package:DLC/Prefabs/Enemy",
                out string packageName,
                out string location);

            Assert.AreEqual("DLC", packageName);
            Assert.AreEqual("Prefabs/Enemy", location);
        }

        /// <summary>验证残缺显式路径在探测前抛出参数异常。</summary>
        [Test]
        public void Split_残缺显式前缀_抛出参数异常()
        {
            Assert.Throws<ArgumentException>(
                () => YooAssetLocation.Split("package:DLC", out _, out _));
            Assert.Throws<ArgumentException>(
                () => YooAssetLocation.Split("package:DLC/", out _, out _));
        }
    }
}
#endif
