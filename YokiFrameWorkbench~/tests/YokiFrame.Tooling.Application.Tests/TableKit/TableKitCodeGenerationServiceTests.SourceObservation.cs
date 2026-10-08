namespace YokiFrame.Tooling.Application.Tests.TableKit;

/// <summary>按完整代码行、标识符和字符串字面量观察 TableKit 生成源码。</summary>
public sealed partial class TableKitCodeGenerationServiceTests
{
    /// <summary>断言每一条预期文本都是生成源码中的完整代码行。</summary>
    /// <param name="source">生成源码。</param>
    /// <param name="expectedLines">去掉缩进后必须原样存在的代码行。</param>
    private static void AssertCodeLines(string source, params string[] expectedLines)
    {
        HashSet<string> lines = ReadCodeLines(source);
        for (int index = 0; index < expectedLines.Length; index++)
        {
            Assert.Contains(expectedLines[index], lines);
        }
    }

    /// <summary>断言生成源码不存在指定的完整代码行。</summary>
    /// <param name="source">生成源码。</param>
    /// <param name="forbiddenLines">去掉缩进后禁止出现的代码行。</param>
    private static void AssertOmitsCodeLines(string source, params string[] forbiddenLines)
    {
        HashSet<string> lines = ReadCodeLines(source);
        for (int index = 0; index < forbiddenLines.Length; index++)
        {
            Assert.DoesNotContain(forbiddenLines[index], lines);
        }
    }

    /// <summary>断言生成源码的标识符集合不包含指定名称。</summary>
    /// <param name="source">生成源码。</param>
    /// <param name="identifiers">禁止作为完整标识符出现的名称。</param>
    private static void AssertOmitsIdentifiers(string source, params string[] identifiers)
    {
        HashSet<string> actual = ReadIdentifiers(source);
        for (int index = 0; index < identifiers.Length; index++)
        {
            Assert.DoesNotContain(identifiers[index], actual);
        }
    }

    /// <summary>断言生成源码的字符串字面量集合不包含指定文本。</summary>
    /// <param name="source">生成源码。</param>
    /// <param name="literals">禁止作为完整字符串字面量出现的文本。</param>
    private static void AssertOmitsStringLiterals(string source, params string[] literals)
    {
        HashSet<string> actual = ReadStringLiterals(source);
        for (int index = 0; index < literals.Length; index++)
        {
            Assert.DoesNotContain(literals[index], actual);
        }
    }

    /// <summary>收集去掉首尾空白后的非空代码行。</summary>
    /// <param name="source">生成源码。</param>
    /// <returns>按原样比较的代码行集合。</returns>
    private static HashSet<string> ReadCodeLines(string source)
    {
        string[] parts = source.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').Split('\n');
        HashSet<string> lines = new(StringComparer.Ordinal);
        for (int index = 0; index < parts.Length; index++)
        {
            string line = parts[index].Trim();
            if (line.Length > 0)
            {
                lines.Add(line);
            }
        }

        return lines;
    }

    /// <summary>按 C# 标识符字符收集完整标识符，不把更长名称的子串算进去。</summary>
    /// <param name="source">生成源码。</param>
    /// <returns>源码中出现过的标识符。</returns>
    private static HashSet<string> ReadIdentifiers(string source)
    {
        HashSet<string> identifiers = new(StringComparer.Ordinal);
        int start = -1;
        for (int index = 0; index <= source.Length; index++)
        {
            bool isIdentifier = index < source.Length && (char.IsLetterOrDigit(source[index]) || source[index] == '_');
            if (isIdentifier)
            {
                if (start < 0)
                {
                    start = index;
                }

                continue;
            }

            if (start >= 0)
            {
                identifiers.Add(source.Substring(start, index - start));
                start = -1;
            }
        }

        return identifiers;
    }

    /// <summary>收集普通 C# 字符串字面量的内容，转义引号不会提前结束字面量。</summary>
    /// <param name="source">生成源码。</param>
    /// <returns>不含外层引号的字面量文本。</returns>
    private static HashSet<string> ReadStringLiterals(string source)
    {
        HashSet<string> literals = new(StringComparer.Ordinal);
        for (int index = 0; index < source.Length; index++)
        {
            if (source[index] != '"')
            {
                continue;
            }

            int start = index + 1;
            int cursor = start;
            while (cursor < source.Length)
            {
                if (source[cursor] == '\\')
                {
                    cursor += 2;
                    continue;
                }

                if (source[cursor] == '"')
                {
                    break;
                }

                cursor++;
            }

            if (cursor >= source.Length)
            {
                break;
            }

            literals.Add(source.Substring(start, cursor - start));
            index = cursor;
        }

        return literals;
    }
}
