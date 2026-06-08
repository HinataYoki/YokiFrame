using System;

namespace YokiFrame
{
    /// <summary>
    /// Allocation-free splitter for <see cref="ReadOnlySpan{T}"/> segments separated by one character.
    /// </summary>
    /// <remarks>
    /// This helper is intended for hot-path parsing scenarios where allocating intermediate string arrays would
    /// be too expensive. It iterates one slice at a time and returns each piece as a span.
    /// </remarks>
    public ref struct SpanSplitter
    {
        private ReadOnlySpan<char> _span;
        private readonly char _sep;
        private int _pos;

        /// <summary>
        /// Creates a splitter for the specified span and separator.
        /// </summary>
        public SpanSplitter(ReadOnlySpan<char> span, char sep)
        {
            _span = span;
            _sep = sep;
            _pos = -1;
        }

        /// <summary>
        /// Advances to the next slice.
        /// </summary>
        /// <param name="slice">The next span segment.</param>
        /// <returns><see langword="true"/> when a segment was produced.</returns>
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
