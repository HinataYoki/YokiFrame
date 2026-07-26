using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 CommandBridge JsonHelper 的顶层字段定位与标准 JSON 转义解码。
    /// </summary>
    public sealed class YokiFrameJsonHelperTests
    {
        /// <summary>
        /// 验证同名文本出现在其它字段的字符串值中时，不会被误当作字段定位命中。
        /// </summary>
        [Test]
        public void ExtractStringIgnoresMatchingTextInsideStringValues()
        {
            Assert.AreEqual("x", JsonHelper.ExtractString("{\"kind\":\"path\",\"path\":\"x\"}", "path"));
        }

        /// <summary>
        /// 验证嵌套对象中的同名字段不参与定位，只返回顶层字段值。
        /// </summary>
        [Test]
        public void ExtractStringIgnoresNestedObjectFields()
        {
            Assert.AreEqual("b", JsonHelper.ExtractString("{\"filter\":{\"path\":\"a\"},\"path\":\"b\"}", "path"));
        }

        /// <summary>
        /// 验证嵌套数组中的同名字段同样不会干扰顶层定位。
        /// </summary>
        [Test]
        public void ExtractStringIgnoresNestedArrayFields()
        {
            Assert.AreEqual(
                "b",
                JsonHelper.ExtractString("{\"items\":[{\"path\":\"a\"}],\"path\":\"b\"}", "path"));
        }

        /// <summary>
        /// 验证字段缺失、非对象文本和空白填充时的定位结果保持稳定。
        /// </summary>
        [Test]
        public void ExtractStringHandlesMissingFieldsAndWhitespace()
        {
            Assert.IsNull(JsonHelper.ExtractString("{\"other\":\"a\"}", "path"));
            Assert.IsNull(JsonHelper.ExtractString("[\"path\",\"a\"]", "path"));
            Assert.AreEqual("a", JsonHelper.ExtractString("{ \n \"path\" : \"a\" }", "path"));
        }

        /// <summary>
        /// 验证字段顺序变化不影响顶层扫描结果。
        /// </summary>
        [Test]
        public void ExtractStringSupportsAnyTopLevelFieldOrder()
        {
            Assert.AreEqual("a", JsonHelper.ExtractString("{\"path\":\"a\",\"count\":1}", "path"));
            Assert.AreEqual("a", JsonHelper.ExtractString("{\"count\":1,\"path\":\"a\"}", "path"));
            Assert.AreEqual(
                "a",
                JsonHelper.ExtractString("{\"count\":1,\"path\":\"a\",\"enabled\":true}", "path"));
        }

        /// <summary>
        /// 验证 primitive 提取同样不会命中字符串值与嵌套结构中的同名字段。
        /// </summary>
        [Test]
        public void TryExtractIntUsesTopLevelFieldOnly()
        {
            Assert.IsTrue(JsonHelper.TryExtractInt("{\"kind\":\"limit\",\"limit\":7}", "limit", out int fromValueText));
            Assert.AreEqual(7, fromValueText);

            Assert.IsTrue(JsonHelper.TryExtractInt("{\"filter\":{\"limit\":3},\"limit\":7}", "limit", out int fromNested));
            Assert.AreEqual(7, fromNested);
        }

        /// <summary>
        /// 验证读取侧解码全部标准转义，包含写入侧会产生的 \b 与 \f 控制字符。
        /// </summary>
        [Test]
        public void ExtractStringDecodesStandardEscapes()
        {
            Assert.AreEqual(
                "\"\\\b\f\n\r\t",
                JsonHelper.ExtractString("{\"text\":\"\\\"\\\\\\b\\f\\n\\r\\t\"}", "text"));
        }

        /// <summary>
        /// 验证 \uXXXX 解码非 ASCII 与 Workbench 默认 encoder 会转义的 ASCII 字符。
        /// </summary>
        [Test]
        public void ExtractStringDecodesUnicodeEscapes()
        {
            Assert.AreEqual(
                "YokiFrame.Outer+Inner",
                JsonHelper.ExtractString("{\"typeName\":\"YokiFrame.Outer\\u002BInner\"}", "typeName"));
            Assert.AreEqual("中文", JsonHelper.ExtractString("{\"name\":\"\\u4E2D\\u6587\"}", "name"));
            Assert.AreEqual("<a&b>", JsonHelper.ExtractString("{\"name\":\"\\u003Ca\\u0026b\\u003E\"}", "name"));
            Assert.AreEqual("\u0001", JsonHelper.ExtractString("{\"name\":\"\\u0001\"}", "name"));
        }

        /// <summary>
        /// 验证代理对按两个 code unit 顺序重组为完整增补平面字符。
        /// </summary>
        [Test]
        public void ExtractStringRecombinesSurrogatePairs()
        {
            string value = JsonHelper.ExtractString("{\"name\":\"\\uD83D\\uDE00\"}", "name");

            Assert.AreEqual("\uD83D\uDE00", value);
            Assert.AreEqual(2, value.Length);
            Assert.AreEqual(0x1F600, char.ConvertToUtf32(value, 0));
        }

        /// <summary>
        /// 验证非法或截断的 \uXXXX 转义按既有失败语义返回 null，而不是产生损坏文本。
        /// </summary>
        [Test]
        public void ExtractStringRejectsInvalidUnicodeEscapes()
        {
            Assert.IsNull(JsonHelper.ExtractString("{\"name\":\"\\u12\"}", "name"));
            Assert.IsNull(JsonHelper.ExtractString("{\"name\":\"\\u12g4\"}", "name"));
            Assert.IsNull(JsonHelper.ExtractString("{\"name\":\"\\u\"}", "name"));
        }

        /// <summary>
        /// 验证 EscapeString 与 ExtractString 对控制字符、引号和增补平面字符往返无损。
        /// </summary>
        [Test]
        public void EscapeStringRoundTripsThroughExtractString()
        {
            const string ORIGINAL = "a\"b\\c\bd\fe\nf\rg\th\u0001i中文\uD83D\uDE00";

            string json = "{\"text\":\"" + JsonHelper.EscapeString(ORIGINAL) + "\"}";

            Assert.AreEqual(ORIGINAL, JsonHelper.ExtractString(json, "text"));
        }

        /// <summary>
        /// 验证嵌套同名字段的转义值不会污染顶层字段的解码结果。
        /// </summary>
        [Test]
        public void ExtractStringDecodesTopLevelValueWithNestedEscapedDuplicate()
        {
            Assert.AreEqual(
                "outer+value",
                JsonHelper.ExtractString(
                    "{\"filter\":{\"path\":\"inner\\u002Bvalue\"},\"path\":\"outer\\u002Bvalue\"}",
                    "path"));
        }
    }
}
