using System;

namespace YokiFrame
{
    /// <summary>
    /// 按单字符逐段读取 ReadOnlySpan 的无中间数组分隔器。
    /// </summary>
    public ref struct SpanSplitter
    {
        private readonly ReadOnlySpan<char> mSpan;
        private readonly char mSeparator;
        private readonly bool mRemoveEmptyEntries;
        private int mNextStart;
        private bool mCompleted;
        private ReadOnlySpan<char> mCurrent;

        /// <summary>
        /// 创建指定字符序列、分隔符和空片段策略的迭代器。
        /// </summary>
        /// <param name="span">需要分段读取的字符序列。</param>
        /// <param name="separator">单字符分隔符。</param>
        /// <param name="options">支持 None 或 RemoveEmptyEntries，其它值会被拒绝。</param>
        public SpanSplitter(
            ReadOnlySpan<char> span,
            char separator,
            StringSplitOptions options = StringSplitOptions.None)
        {
            if (options != StringSplitOptions.None
                && options != StringSplitOptions.RemoveEmptyEntries)
            {
                throw new ArgumentOutOfRangeException(nameof(options));
            }

            mSpan = span;
            mSeparator = separator;
            mRemoveEmptyEntries = options == StringSplitOptions.RemoveEmptyEntries;
            mNextStart = 0;
            mCompleted = false;
            mCurrent = default;
        }

        /// <summary>
        /// 获取最近一次 MoveNext 成功返回的原序列片段。
        /// </summary>
        public ReadOnlySpan<char> Current
        {
            get { return mCurrent; }
        }

        /// <summary>
        /// 返回当前分隔器副本，使 foreach 直接使用 ref struct 枚举模式且不装箱。
        /// </summary>
        /// <returns>保持当前位置的分隔器副本。</returns>
        public SpanSplitter GetEnumerator()
        {
            return this;
        }

        /// <summary>
        /// 移动到下一个符合空片段策略的片段，并更新 Current。
        /// </summary>
        /// <returns>存在下一个片段时返回 true。</returns>
        public bool MoveNext()
        {
            return MoveNext(out _);
        }

        /// <summary>
        /// 移动到下一个符合空片段策略的片段。
        /// </summary>
        /// <param name="slice">成功时返回指向原字符序列的片段。</param>
        /// <returns>存在下一个片段时返回 true。</returns>
        public bool MoveNext(out ReadOnlySpan<char> slice)
        {
            while (!mCompleted)
            {
                int start = mNextStart;
                int relativeIndex = mSpan.Slice(start).IndexOf(mSeparator);
                if (relativeIndex < 0)
                {
                    slice = mSpan.Slice(start);
                    mCompleted = true;
                }
                else
                {
                    slice = mSpan.Slice(start, relativeIndex);
                    mNextStart = start + relativeIndex + 1;
                }

                if (!mRemoveEmptyEntries || slice.Length > 0)
                {
                    mCurrent = slice;
                    return true;
                }
            }

            slice = default;
            mCurrent = default;
            return false;
        }
    }
}
