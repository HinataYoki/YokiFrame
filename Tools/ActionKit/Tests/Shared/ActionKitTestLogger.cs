using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>记录 ActionKit 测试期间的 Error，并要求每条预期错误都被显式消费。</summary>
    public sealed class ActionKitTestLogger : IEngineLogger
    {
        private readonly List<string> mErrors = new();

        /// <summary>记录宿主日志；只有 Error 会影响 ActionKit 故障测试契约。</summary>
        /// <param name="level">日志等级。</param>
        /// <param name="message">日志正文。</param>
        /// <param name="context">本测试不使用的宿主上下文。</param>
        public void Log(LogLevel level, string message, object context = null)
        {
            if (level == LogLevel.Error)
                mErrors.Add(message ?? string.Empty);
        }

        /// <summary>清除上一用例记录，保证 fixture 实例复用时仍相互隔离。</summary>
        public void Clear() => mErrors.Clear();

        /// <summary>按顺序断言错误前缀，并在断言前取走记录，避免 TearDown 重复报告。</summary>
        /// <param name="expectedPrefixes">本用例允许出现的完整错误前缀序列。</param>
        public void AssertErrors(params string[] expectedPrefixes)
        {
            string[] actual = mErrors.ToArray();
            mErrors.Clear();
            Assert.AreEqual(expectedPrefixes.Length, actual.Length, "ActionKit Error 数量与测试契约不一致。");
            for (var index = 0; index < expectedPrefixes.Length; index++)
                StringAssert.StartsWith(expectedPrefixes[index], actual[index]);
        }

        /// <summary>断言并消费唯一 Error，适合 Adapter 与 Integration 故障终态用例。</summary>
        /// <param name="expectedPrefix">预期错误前缀。</param>
        public void AssertSingleError(string expectedPrefix) => AssertErrors(expectedPrefix);

        /// <summary>断言固定数量的同类错误，适合有界历史和 payload 压力用例。</summary>
        /// <param name="count">预期错误数量。</param>
        /// <param name="expectedPrefix">每条错误必须具备的前缀。</param>
        public void AssertRepeatedErrors(int count, string expectedPrefix)
        {
            string[] actual = mErrors.ToArray();
            mErrors.Clear();
            Assert.AreEqual(count, actual.Length, "ActionKit 重复 Error 数量与测试契约不一致。");
            for (var index = 0; index < actual.Length; index++)
                StringAssert.StartsWith(expectedPrefix, actual[index]);
        }

        /// <summary>断言用例没有遗留未声明 Error，防止记录型 logger 把新回归静默吞掉。</summary>
        public void AssertNoErrors()
        {
            string[] actual = mErrors.ToArray();
            mErrors.Clear();
            Assert.AreEqual(0, actual.Length, "存在未声明的 ActionKit Error: " + string.Join("\n", actual));
        }
    }
}
