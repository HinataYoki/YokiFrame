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
        public void Vector2_ZeroMagnitudeAndOperatorsPreserveComponents()
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
        /// 验证二维向量的对象比较、哈希和诊断文本均采用值语义而非装箱引用语义。
        /// </summary>
        [Test]
        public void Vector2_EqualityHashAndDiagnosticTextUseValueSemantics()
        {
            YokiVector2 value = new(1.25f, -2.5f);
            YokiVector2 equalValue = new(1.25f, -2.5f);
            object boxedEqualValue = equalValue;

            Assert.True(value == equalValue);
            Assert.False(value != equalValue);
            Assert.True(value.Equals(equalValue));
            Assert.True(value.Equals(boxedEqualValue));
            Assert.False(value.Equals(new object()));
            Assert.AreEqual(value.GetHashCode(), equalValue.GetHashCode());

            string text = value.ToString();
            Assert.True(text.StartsWith("(", System.StringComparison.Ordinal));
            Assert.True(text.Contains(", ", System.StringComparison.Ordinal));
            Assert.True(text.EndsWith(")", System.StringComparison.Ordinal));
        }
    }
}
