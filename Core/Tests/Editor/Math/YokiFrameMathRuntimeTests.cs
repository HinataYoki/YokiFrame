using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证跨引擎数学值类型的基础值语义，防止 Tool Runtime 依赖的二维坐标运算发生回归。
    /// </summary>
    public sealed class YokiFrameMathRuntimeTests
    {
        /// <summary>
        /// 验证二维向量的零值、平方长度和算术运算保留每个分量的确定性结果。
        /// </summary>
        [Test]
        public void 运算符_给定分量_零值长度和算术都保留分量()
        {
            YokiVector2 left = new(3f, -4f);
            YokiVector2 right = new(-2f, 5f);

            Assert.AreEqual(new YokiVector2(0f, 0f), YokiVector2.Zero);
            Assert.AreEqual(25f, left.SqrMagnitude);
            Assert.AreEqual(new YokiVector2(1f, 1f), left + right);
            Assert.AreEqual(new YokiVector2(5f, -9f), left - right);
            Assert.AreEqual(new YokiVector2(1.5f, -2f), left * 0.5f);
        }

        /// <summary>
        /// 验证相同分量的比较、哈希和诊断文本都按值成立，不同对象不会被当成相等。
        /// </summary>
        [Test]
        public void Equals_相同分量_比较哈希和诊断文本都按值成立()
        {
            YokiVector2 value = new(1.25f, -2.5f);
            YokiVector2 equalValue = new(1.25f, -2.5f);
            object boxedEqualValue = equalValue;

            Assert.IsTrue(value == equalValue);
            Assert.IsFalse(value != equalValue);
            Assert.IsTrue(value.Equals(equalValue));
            Assert.IsTrue(value.Equals(boxedEqualValue));
            Assert.IsFalse(value.Equals(new object()));
            Assert.AreEqual(value.GetHashCode(), equalValue.GetHashCode());
            Assert.AreEqual("(1.25, -2.50)", value.ToString());
        }
    }
}
