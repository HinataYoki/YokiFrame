using System.Text;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 守护 PoolKit 诊断文本的 UTF-8 字节裁剪在保持既有边界语义的同时不退化为主线程阻塞。
    /// </summary>
    public sealed class YokiFramePoolKitTextBoundaryTests
    {
        /// <summary>验证未超预算的文本原样返回，不做多余复制。</summary>
        [Test]
        public void NormalizeTextKeepsTextWithinBudget()
        {
            const string value = "short";

            Assert.AreSame(value, PoolKitSnapshotBuilder.NormalizeText(value, 64));
        }

        /// <summary>验证 ASCII 文本按字节预算精确裁剪。</summary>
        [Test]
        public void NormalizeTextTruncatesAsciiToBudget()
        {
            string value = new string('a', 100);

            Assert.AreEqual(new string('a', 8), PoolKitSnapshotBuilder.NormalizeText(value, 8));
        }

        /// <summary>验证多字节字符按 UTF-8 字节而非字符数裁剪。</summary>
        [Test]
        public void NormalizeTextCountsUtf8BytesNotChars()
        {
            // 每个 '中' 占 3 字节，预算 7 只能容纳 2 个。
            string value = new string('中', 5);

            Assert.AreEqual(new string('中', 2), PoolKitSnapshotBuilder.NormalizeText(value, 7));
        }

        /// <summary>
        /// 验证代理项对不会被切开，且结果不以致命的高代理项结尾。
        /// 该语义是修复前后必须保持一致的关键边界（递减实现会额外剔除尾部孤立高代理）。
        /// </summary>
        [Test]
        public void NormalizeTextNeverEndsWithLoneHighSurrogate()
        {
            // 表情占 4 字节；预算 3 无法容纳，旧实现返回空串而不是半个代理对。
            string value = "\uD83D\uDE00";

            string result = PoolKitSnapshotBuilder.NormalizeText(value, 3);

            Assert.AreEqual(string.Empty, result);
        }

        /// <summary>验证长文本裁剪结果与逐字节参考实现一致，并覆盖原 O(n²) 会超时的规模。</summary>
        [Test]
        public void NormalizeTextMatchesReferenceOnLargeInput()
        {
            string value = new string('x', 400000);

            string result = PoolKitSnapshotBuilder.NormalizeText(value, 512);

            Assert.AreEqual(512, result.Length);
            Assert.AreEqual(512, Encoding.UTF8.GetByteCount(result));
        }

        /// <summary>验证空值与非正值预算的既有回落行为。</summary>
        [Test]
        public void NormalizeTextHandlesEmptyValue()
        {
            Assert.AreEqual(string.Empty, PoolKitSnapshotBuilder.NormalizeText(null, 16));
            Assert.AreEqual(string.Empty, PoolKitSnapshotBuilder.NormalizeText(string.Empty, 16));
        }
    }
}
