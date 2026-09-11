using System;
using System.Text;
using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 FileBridge 命令 payload 的 JSON 语法校验既能拒绝非法文本，也不会被深层嵌套击穿进程。
    /// </summary>
    public sealed class YokiFrameJsonSyntaxValidatorTests
    {
        /// <summary>
        /// 深度上限与 System.Text.Json 默认 MaxDepth 一致，工具侧与本校验共同遵守同一约定。
        /// </summary>
        private const int MAX_DEPTH = 64;

        /// <summary>
        /// 验证合法 payload 仍被接受，确认新增限深没有改变正常路径行为。
        /// </summary>
        [Test]
        public void EnsureValidJsonAcceptsOrdinaryPayload()
        {
            Assert.DoesNotThrow(() => YokiFrameJsonSyntaxValidator.EnsureValidJson("{\"kit\":\"UIKit\",\"items\":[1,2.5,-3e2,true,false,null,\"x\"]}"));
        }

        /// <summary>
        /// 验证空 payload 仍按空对象处理，兼容旧命令。
        /// </summary>
        [Test]
        public void EnsureValidJsonTreatsBlankPayloadAsEmptyObject()
        {
            Assert.DoesNotThrow(() => YokiFrameJsonSyntaxValidator.EnsureValidJson("   "));
        }

        /// <summary>
        /// 验证恰好达到上限的嵌套仍被接受，避免限深过严破坏既有能力。
        /// </summary>
        [Test]
        public void EnsureValidJsonAcceptsNestingAtDepthLimit()
        {
            Assert.DoesNotThrow(
                () => YokiFrameJsonSyntaxValidator.EnsureValidJson(CreateNestedArray(MAX_DEPTH)));
        }

        /// <summary>
        /// 验证超出上限一层即被拒绝，且以可捕获的 FormatException 表达而非栈溢出。
        /// </summary>
        [Test]
        public void EnsureValidJsonRejectsNestingOverDepthLimit()
        {
            FormatException exception = Assert.Throws<FormatException>(
                () => YokiFrameJsonSyntaxValidator.EnsureValidJson(CreateNestedArray(MAX_DEPTH + 1)));

            Assert.AreEqual("JSON nesting is too deep.", exception!.Message);
        }

        /// <summary>
        /// 验证极深嵌套不再击穿进程栈；修复前该输入会触发不可捕获的栈溢出并终止宿主。
        /// </summary>
        [Test]
        public void EnsureValidJsonRejectsExtremelyDeepNestingWithoutStackOverflow()
        {
            FormatException exception = Assert.Throws<FormatException>(
                () => YokiFrameJsonSyntaxValidator.EnsureValidJson(CreateNestedArray(10000)));

            Assert.AreEqual("JSON nesting is too deep.", exception!.Message);
        }

        /// <summary>
        /// 验证深层嵌套的对象同样受限，确认守卫覆盖对象而不是只覆盖数组。
        /// </summary>
        [Test]
        public void EnsureValidJsonRejectsDeeplyNestedObjects()
        {
            StringBuilder builder = new StringBuilder();
            for (int index = 0; index < MAX_DEPTH + 1; index++)
            {
                builder.Append("{\"a\":");
            }

            builder.Append('1');

            Assert.Throws<FormatException>(
                () => YokiFrameJsonSyntaxValidator.EnsureValidJson(builder.ToString()));
        }

        /// <summary>
        /// 验证既有语法错误仍被拒绝，确认限深没有放宽其它校验。
        /// </summary>
        /// <param name="payload">非法 payload 文本。</param>
        [TestCase("{\"a\":1} trailing")]
        [TestCase("{\"a\":}")]
        [TestCase("[1,2")]
        [TestCase("\"unterminated")]
        [TestCase("{\"a\":01}")]
        [TestCase("nul")]
        public void EnsureValidJsonStillRejectsMalformedPayload(string payload)
        {
            Assert.Throws<FormatException>(() => YokiFrameJsonSyntaxValidator.EnsureValidJson(payload));
        }

        /// <summary>
        /// 构造指定层数的嵌套数组文本，层数大于 0 时形如 [[[]]]。
        /// </summary>
        /// <param name="depth">嵌套层数。</param>
        /// <returns>完整 JSON 数组文本。</returns>
        private static string CreateNestedArray(int depth)
        {
            StringBuilder builder = new StringBuilder(depth * 2);
            for (int index = 0; index < depth; index++)
            {
                builder.Append('[');
            }

            for (int index = 0; index < depth; index++)
            {
                builder.Append(']');
            }

            return builder.ToString();
        }
    }
}
