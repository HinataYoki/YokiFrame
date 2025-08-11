using System;

public ref struct SpanSplitter
{
    private ReadOnlySpan<char> _span;
    private readonly char _sep;
    private int _pos;

    public SpanSplitter(ReadOnlySpan<char> span, char sep)
    {
        _span = span;
        _sep = sep;
        _pos = -1;
    }

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

    /*  
     *  使用示例：
        var text = inputString;
        var splitter = new SpanSplitter(text.AsSpan(), ',');
        while (splitter.MoveNext(out var part))
        {
            // part 是 ReadOnlySpan<char>，不会分配新字符串
            // 比如：Console.WriteLine(part.ToString());
        }
    */
}