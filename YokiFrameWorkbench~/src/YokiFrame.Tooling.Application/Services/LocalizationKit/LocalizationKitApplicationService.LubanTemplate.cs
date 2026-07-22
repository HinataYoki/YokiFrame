using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using YokiFrame;

namespace YokiFrame.Tooling.Application.Services.LocalizationKit;

/// <summary>承载 LocalizationKit Luban XML schema 和单工作表 Excel 模板的受控写入细节。</summary>
public sealed partial class LocalizationKitApplicationService
{
    private const int LUBAN_LEFT_STYLE_ID = 1;
    private const int LUBAN_CENTER_STYLE_ID = 2;
    private const int LUBAN_GREEN_LEFT_STYLE_ID = 3;
    private const int LUBAN_GREEN_CENTER_STYLE_ID = 4;
    private const int LUBAN_RED_LEFT_STYLE_ID = 5;
    private const int LUBAN_RED_CENTER_STYLE_ID = 6;
    private const double LUBAN_MARKER_COLUMN_WIDTH = 13d;
    private const double LUBAN_ID_COLUMN_WIDTH = 12d;
    private const double LUBAN_KEY_COLUMN_WIDTH = 25d;
    private const double LUBAN_VALUE_KIND_COLUMN_WIDTH = 18d;
    private const double LUBAN_TRANSLATION_COLUMN_WIDTH = 28d;

    /// <summary>模板默认示例，覆盖普通文本、索引/命名格式文本、普通与复数共存以及全部复数枚举分类。</summary>
    private static readonly (string Id, string Key, string ValueKind, string ChineseSimplified, string English)[] sTemplateExamples =
    {
        ("1000", "menu.start", "Text", "开始游戏", "Start Game"),
        ("1001", "player.greeting", "Text", "你好，{0}", "Hello, {0}"),
        ("1002", "inventory.summary", "Text", "{name}：{count:F0} 个物品", "{name}: {count:F0} items"),
        ("2000", "inventory.item", "Text", "物品", "Item"),
        (string.Empty, string.Empty, "One", "{0} 个物品", "{0} item"),
        (string.Empty, string.Empty, "Other", "{0} 个物品", "{0} items"),
        ("3000", "demo.plural.categories", "Zero", "没有奖励", "No rewards"),
        (string.Empty, string.Empty, "One", "{0} 个奖励", "{0} reward"),
        (string.Empty, string.Empty, "Two", "{0} 个奖励", "{0} rewards"),
        (string.Empty, string.Empty, "Few", "{0} 个奖励", "{0} rewards"),
        (string.Empty, string.Empty, "Many", "{0} 个奖励", "{0} rewards"),
        (string.Empty, string.Empty, "Other", "{0} 个奖励", "{0} rewards")
    };

    /// <summary>先生成 XML 和 Excel 临时文件，再以可回滚顺序提交作者文件。</summary>
    /// <param name="plan">已完成路径 containment 和注册判断的模板计划。</param>
    /// <param name="languages">模板包含的规范语言列。</param>
    /// <param name="force">已有作者文件时是否允许替换。</param>
    private static void WriteLubanTemplateFiles(
        LocalizationLubanPlan plan,
        IReadOnlyList<LanguageId> languages,
        bool force)
    {
        if (!force && (File.Exists(plan.SchemaPath) || File.Exists(plan.WorkbookPath)))
        {
            throw new IOException("LocalizationKit XML 或 Excel 已存在；使用 force 才能覆盖。");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(plan.SchemaPath)!);
        Directory.CreateDirectory(Path.GetDirectoryName(plan.WorkbookPath)!);
        string schemaTemporaryPath = plan.SchemaPath + ".tmp-" + Guid.NewGuid().ToString("N");
        string workbookTemporaryPath = plan.WorkbookPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            WriteLubanSchema(schemaTemporaryPath, plan.WorkbookInputPath, languages);
            WriteLubanWorkbook(workbookTemporaryPath, languages);
            CommitTemplateFiles(plan.SchemaPath, schemaTemporaryPath, plan.WorkbookPath, workbookTemporaryPath);
        }
        finally
        {
            DeleteIfExists(schemaTemporaryPath);
            DeleteIfExists(workbookTemporaryPath);
        }
    }

    /// <summary>写入单表 Luban XML schema；每个 Id 只保留一条记录，普通和复数文本收纳到枚举键控映射。</summary>
    /// <param name="path">同目录临时 XML 文件路径。</param>
    /// <param name="workbookInputPath">相对于 luban.conf dataDir 的 Excel input 路径。</param>
    /// <param name="languages">文本表中需要生成的语言字段。</param>
    private static void WriteLubanSchema(string path, string workbookInputPath, IReadOnlyList<LanguageId> languages)
    {
        XElement valueKindEnum = new("enum", new XAttribute("name", LUBAN_VALUE_KIND_ENUM_NAME), new XAttribute("group", "*"),
            new XElement("var", new XAttribute("name", LUBAN_NORMAL_VALUE_KIND_NAME), new XAttribute("value", "0")),
            new XElement("var", new XAttribute("name", "Zero"), new XAttribute("value", "1")),
            new XElement("var", new XAttribute("name", "One"), new XAttribute("value", "2")),
            new XElement("var", new XAttribute("name", "Two"), new XAttribute("value", "3")),
            new XElement("var", new XAttribute("name", "Few"), new XAttribute("value", "4")),
            new XElement("var", new XAttribute("name", "Many"), new XAttribute("value", "5")),
            new XElement("var", new XAttribute("name", "Other"), new XAttribute("value", "6")));
        XElement translationsBean = new("bean", new XAttribute("name", LUBAN_TRANSLATIONS_BEAN_NAME), new XAttribute("group", "*"));
        foreach (LanguageId language in languages)
        {
            translationsBean.Add(new XElement("var", new XAttribute("name", language.ToString()), new XAttribute("type", "string")));
        }

        XElement entryBean = new("bean", new XAttribute("name", LUBAN_ENTRY_BEAN_NAME), new XAttribute("group", "*"),
            new XElement("var", new XAttribute("name", "id"), new XAttribute("type", "int")),
            new XElement("var", new XAttribute("name", "key"), new XAttribute("type", "string")),
            new XElement("var", new XAttribute("name", LUBAN_VARIANTS_FIELD_NAME), new XAttribute("type", "map," + LUBAN_VALUE_KIND_ENUM_NAME + "," + LUBAN_TRANSLATIONS_BEAN_NAME)));

        XDocument document = new(
            new XDeclaration("1.0", "utf-8", null),
            new XElement("module", new XAttribute("name", "yokiframe"),
                valueKindEnum,
                translationsBean,
                entryBean,
                new XElement("table", new XAttribute("name", LUBAN_ENTRY_TABLE_NAME), new XAttribute("value", LUBAN_ENTRY_BEAN_NAME), new XAttribute("index", "id"), new XAttribute("input", "Localization@" + workbookInputPath))));
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        using XmlWriter writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false), Indent = true });
        document.Save(writer);
        writer.Flush();
        stream.Flush(true);
    }

    /// <summary>写入 Luban 可读取的单一 Localization 工作表，不引入 Excel 编辑运行时依赖。</summary>
    /// <param name="path">同目录临时 xlsx 文件路径。</param>
    /// <param name="languages">variants 翻译 bean 需要的语言列和示例 map 行。</param>
    private static void WriteLubanWorkbook(string path, IReadOnlyList<LanguageId> languages)
    {
        using FileStream stream = new(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using ZipArchive archive = new(stream, ZipArchiveMode.Create);
        WriteWorkbookEntry(archive, "[Content_Types].xml", BuildContentTypesXml());
        WriteWorkbookEntry(archive, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>");
        WriteWorkbookEntry(archive, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets><sheet name=\"Localization\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        WriteWorkbookEntry(archive, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/><Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/></Relationships>");
        WriteWorkbookEntry(archive, "xl/styles.xml", BuildStylesXml());
        WriteWorkbookEntry(archive, "xl/worksheets/sheet1.xml", BuildLocalizationSheet(languages));
    }

    /// <summary>构造 OpenXML content types，声明唯一工作表及其样式资源。</summary>
    /// <returns>可由 Excel 和 Luban 读取的 content types XML。</returns>
    private static string BuildContentTypesXml() => "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/><Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/></Types>";

    /// <summary>构造作者模板的最小样式表，使标题层级、对齐和数据区在常见 Excel 客户端中清晰可读。</summary>
    /// <returns>引用本类样式编号的 OpenXML styles.xml 内容。</returns>
    private static string BuildStylesXml()
    {
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
            + "<fonts count=\"3\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><color rgb=\"FF006100\"/><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><color rgb=\"FF9C0006\"/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>"
            + "<fills count=\"4\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFC6EFCE\"/><bgColor indexed=\"64\"/></patternFill></fill><fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFC7CE\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>"
            + "<borders count=\"2\"><border/><border><left style=\"thin\"><color rgb=\"FFB7C9D6\"/></left><right style=\"thin\"><color rgb=\"FFB7C9D6\"/></right><top style=\"thin\"><color rgb=\"FFB7C9D6\"/></top><bottom style=\"thin\"><color rgb=\"FFB7C9D6\"/></bottom></border></borders>"
            + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
            + "<cellXfs count=\"7\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"1\" xfId=\"0\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf><xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf><xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"left\" vertical=\"center\" wrapText=\"1\"/></xf><xf numFmtId=\"0\" fontId=\"2\" fillId=\"3\" borderId=\"1\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\" applyBorder=\"1\" applyAlignment=\"1\"><alignment horizontal=\"center\" vertical=\"center\" wrapText=\"1\"/></xf></cellXfs>"
            + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles><dxfs count=\"0\"/><tableStyles count=\"0\" defaultTableStyle=\"TableStyleMedium2\" defaultPivotStyle=\"PivotStyleLight16\"/></styleSheet>";
    }

    /// <summary>构造单一 Localization 工作表；map 的 Text 键表示普通文本，其余枚举键表示复数分类。</summary>
    /// <param name="languages">模板语言列表，按列顺序输出。</param>
    /// <returns>Localization 工作表 XML。</returns>
    private static string BuildLocalizationSheet(IReadOnlyList<LanguageId> languages)
    {
        List<string> fields = new() { "id", "key", "*" + LUBAN_VARIANTS_FIELD_NAME };
        fields.AddRange(languages.Select(static _ => string.Empty));
        List<string> types = new() { "int", "string", "map," + LUBAN_VALUE_KIND_ENUM_NAME + "," + LUBAN_TRANSLATIONS_BEAN_NAME };
        types.AddRange(languages.Select(static _ => string.Empty));
        List<string> childFields = new() { string.Empty, string.Empty, "$key" };
        childFields.AddRange(languages.Select(static language => language.ToString()));
        List<string> comments = new() { "稳定文本 ID", "业务键", "Text 为普通文本；Zero/One/Two/Few/Many/Other 为复数分类" };
        comments.AddRange(languages.Select(static language => language + " 译文"));
        List<string> rows = new()
        {
            BuildWorkbookRow(1, PrependMarker("##", fields), firstColumnStyleId: LUBAN_GREEN_LEFT_STYLE_ID, remainingColumnStyleId: LUBAN_GREEN_CENTER_STYLE_ID),
            BuildWorkbookRow(2, PrependMarker("##type", types), firstColumnStyleId: LUBAN_RED_LEFT_STYLE_ID, remainingColumnStyleId: LUBAN_RED_CENTER_STYLE_ID),
            BuildWorkbookRow(3, PrependMarker("##var", childFields), firstColumnStyleId: LUBAN_GREEN_LEFT_STYLE_ID, remainingColumnStyleId: LUBAN_GREEN_CENTER_STYLE_ID),
            BuildWorkbookRow(4, PrependMarker("##comment", comments), firstColumnStyleId: LUBAN_GREEN_LEFT_STYLE_ID, remainingColumnStyleId: LUBAN_GREEN_CENTER_STYLE_ID)
        };

        int rowCount = AppendTemplateExamples(rows, languages, 5);
        string variantsEndColumn = ColumnName(fields.Count + 1);
        return BuildWorksheet(
            rows,
            fields.Count + 1,
            rowCount,
            new[] { "D1:" + variantsEndColumn + "1", "D2:" + variantsEndColumn + "2" });
    }

    /// <summary>追加可直接导出并覆盖完整枚举模型的作者样例；续行保留空 Id 和 Key 以表达同一 map。</summary>
    /// <param name="rows">工作表行 XML 集合。</param>
    /// <param name="languages">当前模板声明的语言列。</param>
    /// <param name="firstRowIndex">第一条示例的一基 Excel 行索引。</param>
    /// <returns>最后一条示例所在的 Excel 行索引。</returns>
    private static int AppendTemplateExamples(
        ICollection<string> rows,
        IReadOnlyList<LanguageId> languages,
        int firstRowIndex)
    {
        int rowIndex = firstRowIndex;
        foreach ((string id, string key, string valueKind, string chineseSimplified, string english) in sTemplateExamples)
        {
            List<string> values = new() { string.Empty, id, key, valueKind };
            foreach (LanguageId language in languages)
            {
                values.Add(ResolveExampleTranslation(language, chineseSimplified, english));
            }

            rows.Add(BuildWorkbookRow(rowIndex, values, numericColumn: 2));
            rowIndex++;
        }

        return rowIndex - 1;
    }

    /// <summary>为当前模板语言选择预置示例译文；未提供示例的语言保持空白，避免误把演示文本当作完成翻译。</summary>
    /// <param name="language">当前 Excel 语言列。</param>
    /// <param name="chineseSimplified">简体中文示例文本。</param>
    /// <param name="english">英文示例文本。</param>
    /// <returns>当前语言对应的示例，或空文本。</returns>
    private static string ResolveExampleTranslation(LanguageId language, string chineseSimplified, string english)
    {
        return language switch
        {
            LanguageId.ChineseSimplified => chineseSimplified,
            LanguageId.English => english,
            _ => string.Empty
        };
    }

    /// <summary>在字段列表前加入 Luban 标题行标记，避免调用方重复构造可变数组。</summary>
    /// <param name="marker">例如 ##var、##type 或 ##comment。</param>
    /// <param name="values">字段、类型或注释列表。</param>
    /// <returns>带首列标记的完整行数据。</returns>
    private static IReadOnlyList<string> PrependMarker(string marker, IReadOnlyList<string> values)
    {
        string[] result = new string[values.Count + 1];
        result[0] = marker;
        for (int index = 0; index < values.Count; index++)
        {
            result[index + 1] = values[index];
        }

        return result;
    }

    /// <summary>把多行单元格 XML 组装为最小工作表文档。</summary>
    /// <param name="rows">已经构造完成的 row XML。</param>
    /// <param name="columnCount">最大列数。</param>
    /// <param name="rowCount">最大行数。</param>
    /// <param name="mergedRanges">需要合并的 OpenXML 单元格范围；复合字段必须借此声明横向列边界。</param>
    /// <returns>工作表 XML。</returns>
    private static string BuildWorksheet(
        IReadOnlyList<string> rows,
        int columnCount,
        int rowCount,
        IReadOnlyList<string>? mergedRanges = null)
    {
        string mergedCells = mergedRanges == null || mergedRanges.Count == 0
            ? string.Empty
            : "<mergeCells count=\"" + mergedRanges.Count.ToString(CultureInfo.InvariantCulture) + "\">"
                + string.Concat(mergedRanges.Select(static range => "<mergeCell ref=\"" + range + "\"/>"))
                + "</mergeCells>";
        return "<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><dimension ref=\"A1:"
            + ColumnName(columnCount) + rowCount.ToString(CultureInfo.InvariantCulture)
            + "\"/><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"4\" topLeftCell=\"A5\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><sheetFormatPr defaultRowHeight=\"18\"/>"
            + BuildColumnDefinitions(columnCount)
            + "<sheetData>" + string.Concat(rows) + "</sheetData>" + mergedCells + "</worksheet>";
    }

    /// <summary>构造列宽定义，使标题、业务键和翻译文本无需手工调整即可阅读。</summary>
    /// <param name="columnCount">当前工作表最大列数。</param>
    /// <returns>OpenXML cols 节点内容。</returns>
    private static string BuildColumnDefinitions(int columnCount)
    {
        StringBuilder columns = new("<cols>");
        for (int index = 1; index <= columnCount; index++)
        {
            double width = index switch
            {
                1 => LUBAN_MARKER_COLUMN_WIDTH,
                2 => LUBAN_ID_COLUMN_WIDTH,
                3 => LUBAN_KEY_COLUMN_WIDTH,
                4 => LUBAN_VALUE_KIND_COLUMN_WIDTH,
                _ => LUBAN_TRANSLATION_COLUMN_WIDTH
            };
            string columnIndex = index.ToString(CultureInfo.InvariantCulture);
            columns.Append("<col min=\"").Append(columnIndex).Append("\" max=\"").Append(columnIndex)
                .Append("\" width=\"").Append(width.ToString(CultureInfo.InvariantCulture)).Append("\" customWidth=\"1\"/>");
        }

        return columns.Append("</cols>").ToString();
    }

    /// <summary>构造一行带样式的 OpenXML 单元格；数值列写为数字，其余统一写入 inline string。</summary>
    /// <param name="rowIndex">Excel 的一基行索引。</param>
    /// <param name="values">当前行各列的文本值。</param>
    /// <param name="numericColumn">可按数值写入的一基列索引；为 null 时全部按字符串写入。</param>
    /// <param name="firstColumnStyleId">第一列的样式编号，用于保持标记列左对齐。</param>
    /// <param name="remainingColumnStyleId">其余列的样式编号，用于保持内容居中。</param>
    /// <returns>完整 row XML。</returns>
    private static string BuildWorkbookRow(
        int rowIndex,
        IReadOnlyList<string> values,
        int? numericColumn = null,
        int firstColumnStyleId = LUBAN_LEFT_STYLE_ID,
        int remainingColumnStyleId = LUBAN_CENTER_STYLE_ID)
    {
        StringBuilder builder = new("<row r=\"" + rowIndex.ToString(CultureInfo.InvariantCulture) + "\">");
        for (int index = 0; index < values.Count; index++)
        {
            string cell = ColumnName(index + 1) + rowIndex.ToString(CultureInfo.InvariantCulture);
            string value = values[index] ?? string.Empty;
            int styleId = index == 0 ? firstColumnStyleId : remainingColumnStyleId;
            if (numericColumn == index + 1
                && decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            {
                builder.Append("<c r=\"").Append(cell).Append("\" s=\"").Append(styleId).Append("\"><v>").Append(value).Append("</v></c>");
            }
            else
            {
                builder.Append("<c r=\"").Append(cell).Append("\" s=\"").Append(styleId).Append("\" t=\"inlineStr\"><is><t>")
                    .Append(XmlEscape(value)).Append("</t></is></c>");
            }
        }

        return builder.Append("</row>").ToString();
    }

    /// <summary>计算 Excel 一基列索引对应的字母名称。</summary>
    /// <param name="index">一基列索引。</param>
    /// <returns>A、B、AA 等列名称。</returns>
    private static string ColumnName(int index)
    {
        StringBuilder result = new();
        while (index > 0)
        {
            index--;
            result.Insert(0, (char)('A' + index % 26));
            index /= 26;
        }

        return result.ToString();
    }

    /// <summary>转义 XML 单元格与属性文本，确保翻译中的特殊字符不破坏工作簿。</summary>
    /// <param name="value">待写入 XML 的文本。</param>
    /// <returns>安全的 XML 文本。</returns>
    private static string XmlEscape(string value) => System.Security.SecurityElement.Escape(value) ?? string.Empty;

    /// <summary>写入 UTF-8 ZIP 条目，供 OpenXML 工作簿复用。</summary>
    /// <param name="archive">当前 xlsx ZIP。</param>
    /// <param name="path">ZIP 内部路径。</param>
    /// <param name="content">UTF-8 文本内容。</param>
    private static void WriteWorkbookEntry(ZipArchive archive, string path, string content)
    {
        ZipArchiveEntry entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    /// <summary>提交两个模板文件，并在后一个提交失败时恢复前一个作者文件。</summary>
    /// <param name="schemaPath">正式 XML schema 路径。</param>
    /// <param name="schemaTemporaryPath">已完整写入的 XML 临时文件。</param>
    /// <param name="workbookPath">正式 Excel 路径。</param>
    /// <param name="workbookTemporaryPath">已完整写入的 Excel 临时文件。</param>
    private static void CommitTemplateFiles(
        string schemaPath,
        string schemaTemporaryPath,
        string workbookPath,
        string workbookTemporaryPath)
    {
        TemplateFileCommit? schemaCommit = null;
        TemplateFileCommit? workbookCommit = null;
        try
        {
            schemaCommit = CommitTemplateFile(schemaPath, schemaTemporaryPath);
            workbookCommit = CommitTemplateFile(workbookPath, workbookTemporaryPath);
        }
        catch
        {
            RestoreTemplateFile(workbookCommit);
            RestoreTemplateFile(schemaCommit);
            throw;
        }
        finally
        {
            DeleteIfExists(schemaCommit?.BackupPath);
            DeleteIfExists(workbookCommit?.BackupPath);
        }
    }

    /// <summary>将一个临时模板替换为正式文件，并保留原文件备份以便多文件失败回滚。</summary>
    /// <param name="path">正式文件路径。</param>
    /// <param name="temporaryPath">完整临时文件路径。</param>
    /// <returns>记录提交前文件状态的回滚信息。</returns>
    private static TemplateFileCommit CommitTemplateFile(string path, string temporaryPath)
    {
        if (!File.Exists(path))
        {
            File.Move(temporaryPath, path);
            return new TemplateFileCommit(path, false, string.Empty);
        }

        string backupPath = path + ".bak-" + Guid.NewGuid().ToString("N");
        File.Replace(temporaryPath, path, backupPath);
        return new TemplateFileCommit(path, true, backupPath);
    }

    /// <summary>恢复单个模板文件的提交前状态；恢复失败保持原始异常优先。</summary>
    /// <param name="commit">已提交文件的备份信息。</param>
    private static void RestoreTemplateFile(TemplateFileCommit? commit)
    {
        if (commit == null)
        {
            return;
        }

        try
        {
            if (commit.HadOriginal)
            {
                File.Copy(commit.BackupPath, commit.Path, true);
            }
            else
            {
                DeleteIfExists(commit.Path);
            }
        }
        catch (IOException)
        {
            // 保留首个提交异常，清理失败仅影响临时恢复证据。
        }
    }

    /// <summary>删除仍存在的临时或备份文件；空路径和已被移动的文件直接忽略。</summary>
    /// <param name="path">待清理路径。</param>
    private static void DeleteIfExists(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            File.Delete(path);
        }
    }

    /// <summary>记录多文件提交时单个正式文件的原始状态与备份位置。</summary>
    /// <param name="Path">正式文件路径。</param>
    /// <param name="HadOriginal">提交前是否存在作者文件。</param>
    /// <param name="BackupPath">原文件备份路径；新文件提交时为空。</param>
    private sealed record TemplateFileCommit(string Path, bool HadOriginal, string BackupPath);
}
