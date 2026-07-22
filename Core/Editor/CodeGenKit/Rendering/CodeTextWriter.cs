using System;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 使用固定 LF、Tab 缩进和单一 StringBuilder 渲染确定性 C# 源码。
    /// </summary>
    internal sealed class CodeTextWriter
    {
        private readonly StringBuilder mBuilder;
        private int mIndentCount;

        /// <summary>
        /// 创建指定初始容量的源码 writer，容量只影响分配策略而不影响输出。
        /// </summary>
        /// <param name="initialCapacity">StringBuilder 初始容量，不能为负数。</param>
        internal CodeTextWriter(int initialCapacity)
        {
            if (initialCapacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(initialCapacity));
            }

            mBuilder = new StringBuilder(initialCapacity);
        }

        /// <summary>
        /// 写入一行源码；空字符串生成空行，非空内容只缩进第一物理行。
        /// </summary>
        /// <param name="code">已保证不含换行符的单行源码。</param>
        internal void WriteLine(string code = null)
        {
            if (!string.IsNullOrEmpty(code))
            {
                mBuilder.Append('\t', mIndentCount);
                mBuilder.Append(code);
            }

            mBuilder.Append('\n');
        }

        /// <summary>
        /// 进入下一层结构化作用域，后续非空行增加一个 Tab。
        /// </summary>
        internal void PushIndent()
        {
            mIndentCount++;
        }

        /// <summary>
        /// 离开当前结构化作用域；缩进失衡时立即失败而不是静默夹紧。
        /// </summary>
        internal void PopIndent()
        {
            if (mIndentCount == 0)
            {
                throw new InvalidOperationException("CodeGenKit 缩进作用域不平衡。");
            }

            mIndentCount--;
        }

        /// <summary>
        /// 返回当前完整源码快照，不改变 writer 状态。
        /// </summary>
        /// <returns>使用固定 LF 的源码字符串。</returns>
        public override string ToString()
        {
            return mBuilder.ToString();
        }
    }
}
