using System;

namespace YokiFrame
{
    /// <summary>
    /// 面向 ReadOnlySpan&lt;char&gt; 的无分配分隔器，按单字符分隔片段。
    /// </summary>
    /// <remarks>
    /// 该辅助类型用于热路径解析场景，避免分配中间字符串数组。
    /// 它逐段迭代并以 span 形式返回当前片段。
    /// </remarks>
    public ref struct SpanSplitter
    {
        private ReadOnlySpan<char> _span;
        private readonly char _sep;
        private int _pos;

        /// <summary>为指定 span 和分隔符创建分隔器。</summary>
        public SpanSplitter(ReadOnlySpan<char> span, char sep)
        {
            _span = span;
            _sep = sep;
            _pos = -1;
        }

        /// <summary>
        /// 移动到下一个片段。
        /// </summary>
        /// <param name="slice">下一个 span 片段。</param>
        /// <returns>成功产出片段时返回 <see langword="true"/>。</returns>
        public bool MoveNext(out ReadOnlySpan<char> slice)
        {
            int start = _pos + 1;
            if (start > _span.Length)
            {
                slice = default;
                return false;
            }

            int idx = _span.Slice(start).IndexOf(_sep);
            if (idx < 0)
            {
                slice = _span.Slice(start);
                _pos = _span.Length;
                return slice.Length > 0;
            }

            slice = _span.Slice(start, idx);
            _pos = start + idx;
            return true;
        }
    }
}
