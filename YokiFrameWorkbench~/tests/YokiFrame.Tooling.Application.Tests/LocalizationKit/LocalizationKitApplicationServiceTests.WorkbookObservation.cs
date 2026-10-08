using System.IO.Compression;
using System.Xml.Linq;

namespace YokiFrame.Tooling.Application.Tests.LocalizationKit;

/// <summary>从生成的 xlsx 读取 OpenXML 条目和单元格值。</summary>
public sealed partial class LocalizationKitApplicationServiceTests
{
    /// <summary>读取 xlsx 内的 XML 条目；条目缺失时直接失败。</summary>
    /// <param name="archive">已打开的工作簿。</param>
    /// <param name="entryPath">ZIP 内路径。</param>
    /// <returns>该条目的 XML 文档。</returns>
    private static XDocument ReadWorkbookXml(ZipArchive archive, string entryPath)
    {
        ZipArchiveEntry entry = archive.GetEntry(entryPath)
            ?? throw new InvalidOperationException("缺少工作簿条目: " + entryPath);
        using Stream stream = entry.Open();
        return XDocument.Load(stream);
    }

    /// <summary>收集工作表单元格的 inline string 与数值，不扫描 XML 子串。</summary>
    /// <param name="sheet">sheet XML。</param>
    /// <returns>单元格文本，数值保持原文字。</returns>
    private static string[] ReadCellValues(XDocument sheet)
    {
        return sheet.Descendants()
            .Where(static element => element.Name.LocalName == "c")
            .Select(static cell => ReadCellValue(cell))
            .ToArray();
    }

    /// <summary>读取单个单元格：数值取 v，文本取 inline string 的 t。</summary>
    /// <param name="cell">c 元素。</param>
    /// <returns>单元格文本；空单元格返回空字符串。</returns>
    private static string ReadCellValue(XElement cell)
    {
        XElement? inline = cell.Elements().FirstOrDefault(static element => element.Name.LocalName == "is");
        if (inline != null)
        {
            return string.Concat(
                inline.Descendants()
                    .Where(static element => element.Name.LocalName == "t")
                    .Select(static element => element.Value));
        }

        XElement? value = cell.Elements().FirstOrDefault(static element => element.Name.LocalName == "v");
        return value == null ? string.Empty : value.Value;
    }
}
