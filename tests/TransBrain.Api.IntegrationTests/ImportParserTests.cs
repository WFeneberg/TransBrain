using System.IO.Compression;
using System.Text;
using AwesomeAssertions;
using Microsoft.Data.Sqlite;
using TransBrain.Application.Abstractions;
using TransBrain.Application.Features.Imports;
using TransBrain.Domain.Common;
using TransBrain.Domain.Imports;
using TransBrain.Infrastructure.Imports;

namespace TransBrain.Api.IntegrationTests;

/// <summary>
/// Plain unit tests for the file parsers behind <see cref="IImportSourceParser"/>. They live in
/// this project because the parsers are Infrastructure internals; no containers are involved.
/// </summary>
public class ImportParserTests
{
    private static readonly ImportSourceParser Parser = new();

    private static async Task<ImportTableSet> ParseAsync(ImportSourceType type, string fileName, byte[] content)
    {
        Result<ImportTableSet> result = await Parser.ParseFileAsync(type, fileName, content, CancellationToken.None);
        result.IsSuccess.Should().BeTrue(because: result.Error?.Message ?? string.Empty);
        return result.Value;
    }

    [Fact]
    public async Task ParseFile_SemicolonCsvWithUmlauts_ReadsHeaderAndRows()
    {
        byte[] csv = Encoding.UTF8.GetBytes("Firma;Ort;Zahlungsziel\nHuber GmbH;München;30\nMaier KG;Köln;14\n");

        ImportTableSet tables = await ParseAsync(ImportSourceType.Csv, "kunden.csv", csv);

        ImportTable table = tables.Tables.Single();
        table.Name.Should().Be("kunden");
        table.Columns.Should().Equal("Firma", "Ort", "Zahlungsziel");
        table.Rows.Should().HaveCount(2);
        table.Rows[0][1].Should().Be("München");
    }

    [Fact]
    public async Task ParseFile_CommaCsvWithQuotedFieldsAndEmbeddedNewline_ParsesRfc4180()
    {
        byte[] csv = Encoding.UTF8.GetBytes(
            "name,notes\n\"Huber, GmbH\",\"Zeile 1\nZeile 2 mit \"\"Zitat\"\"\"\n");

        ImportTableSet tables = await ParseAsync(ImportSourceType.Csv, "export.csv", csv);

        ImportTable table = tables.Tables.Single();
        table.Rows[0][0].Should().Be("Huber, GmbH");
        table.Rows[0][1].Should().Be("Zeile 1\nZeile 2 mit \"Zitat\"");
    }

    [Fact]
    public async Task ParseFile_Latin1Csv_FallsBackWithoutManglingUmlauts()
    {
        byte[] csv = Encoding.Latin1.GetBytes("Firma;Ort\nHuber;München\n");

        ImportTableSet tables = await ParseAsync(ImportSourceType.Csv, "alt.csv", csv);

        tables.Tables.Single().Rows[0][1].Should().Be("München");
    }

    [Fact]
    public async Task ParseFile_CsvWithoutDataRows_ReturnsNoDataRowsError()
    {
        Result<ImportTableSet> result = await Parser.ParseFileAsync(
            ImportSourceType.Csv, "leer.csv", Encoding.UTF8.GetBytes("Firma;Ort\n"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.NoDataRows");
    }

    [Fact]
    public async Task ParseFile_JsonRootArray_FlattensNestedObjects()
    {
        byte[] json = Encoding.UTF8.GetBytes(
            """
            [
              { "name": "Huber", "address": { "city": "München", "zip": "80331" }, "tags": ["a", "b"] },
              { "name": "Maier", "address": { "city": "Berlin" }, "active": true }
            ]
            """);

        ImportTableSet tables = await ParseAsync(ImportSourceType.Json, "kunden.json", json);

        ImportTable table = tables.Tables.Single();
        table.Name.Should().Be("kunden");
        table.Columns.Should().Contain(["name", "address.city", "address.zip", "tags", "active"]);
        table.Rows[0][IndexOf(table, "address.city")].Should().Be("München");
        table.Rows[0][IndexOf(table, "tags")].Should().Be("a; b");
        table.Rows[1][IndexOf(table, "address.zip")].Should().BeNull();
    }

    [Fact]
    public async Task ParseFile_JsonRootObject_MakesOneTablePerArrayProperty()
    {
        byte[] json = Encoding.UTF8.GetBytes(
            """
            {
              "kunden": [ { "name": "Huber" } ],
              "fahrer": [ { "vorname": "Max" }, { "vorname": "Eva" } ],
              "version": 3
            }
            """);

        ImportTableSet tables = await ParseAsync(ImportSourceType.Json, "export.json", json);

        tables.Tables.Select(t => t.Name).Should().BeEquivalentTo("kunden", "fahrer");
        tables.Tables.Single(t => t.Name == "fahrer").Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task ParseFile_BrokenJson_ReturnsValidationErrorInsteadOfThrowing()
    {
        Result<ImportTableSet> result = await Parser.ParseFileAsync(
            ImportSourceType.Json, "kaputt.json", Encoding.UTF8.GetBytes("{ nicht json"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task ParseFile_XmlWithRepeatedRecords_ReadsAttributesAndElements()
    {
        byte[] xml = Encoding.UTF8.GetBytes(
            """
            <kunden>
              <kunde id="1"><name>Huber GmbH</name><ort>München</ort></kunde>
              <kunde id="2"><name>Maier KG</name><ort>Berlin</ort></kunde>
            </kunden>
            """);

        ImportTableSet tables = await ParseAsync(ImportSourceType.Xml, "kunden.xml", xml);

        ImportTable table = tables.Tables.Single();
        table.Name.Should().Be("kunde");
        table.Columns.Should().Contain(["@id", "name", "ort"]);
        table.Rows.Should().HaveCount(2);
        table.Rows[1][IndexOf(table, "name")].Should().Be("Maier KG");
    }

    [Fact]
    public async Task ParseFile_XmlWithExternalEntities_IsRejectedSafely()
    {
        byte[] xml = Encoding.UTF8.GetBytes(
            """
            <!DOCTYPE foo [<!ENTITY xxe SYSTEM "file:///etc/passwd">]>
            <rows><row><v>&xxe;</v></row></rows>
            """);

        Result<ImportTableSet> result = await Parser.ParseFileAsync(
            ImportSourceType.Xml, "boese.xml", xml, CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task ParseFile_Xlsx_ReadsSharedStringsNumbersAndDates()
    {
        byte[] xlsx = BuildXlsx();

        ImportTableSet tables = await ParseAsync(ImportSourceType.Excel, "flotte.xlsx", xlsx);

        ImportTable table = tables.Tables.Single();
        table.Name.Should().Be("Fahrzeuge");
        table.Columns.Should().Equal("Kennzeichen", "Nutzlast", "HU");
        table.Rows.Should().HaveCount(1);
        table.Rows[0][0].Should().Be("M-TB 100");
        table.Rows[0][1].Should().Be("24000");
        table.Rows[0][2].Should().Be("2026-02-16"); // serial 46069, date-styled
    }

    [Fact]
    public async Task ParseFile_ZipThatIsNoWorkbook_ReturnsInvalidXlsx()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipArchiveEntry entry = zip.CreateEntry("readme.txt");
            using StreamWriter writer = new(entry.Open());
            writer.Write("kein excel");
        }

        Result<ImportTableSet> result = await Parser.ParseFileAsync(
            ImportSourceType.Excel, "fake.xlsx", stream.ToArray(), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.InvalidXlsx");
    }

    [Fact]
    public async Task ParseFile_SqliteDatabase_ReadsTablesRowsAndNulls()
    {
        byte[] database = BuildSqliteDatabase();

        ImportTableSet tables = await ParseAsync(ImportSourceType.Sqlite, "altdaten.db", database);

        ImportTable table = tables.Tables.Single(t => t.Name == "kunden");
        table.Columns.Should().Equal("name", "ort", "zahlungsziel", "seit");
        table.Rows.Should().HaveCount(2);
        table.Rows[0].Should().Equal("Huber GmbH", "München", "30", "2020-01-15");
        table.Rows[1][1].Should().BeNull();
    }

    [Fact]
    public async Task ParseFile_NotASqliteFile_ReturnsInvalidSqlite()
    {
        Result<ImportTableSet> result = await Parser.ParseFileAsync(
            ImportSourceType.Sqlite, "fake.db", Encoding.UTF8.GetBytes("bloss text"), CancellationToken.None);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be("Import.InvalidSqlite");
    }

    private static int IndexOf(ImportTable table, string column)
        => table.Columns.ToList().IndexOf(column);

    /// <summary>A minimal but spec-conforming workbook: shared string, number, and a date-styled serial.</summary>
    private static byte[] BuildXlsx()
    {
        using var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddEntry(zip, "[Content_Types].xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/sharedStrings.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            AddEntry(zip, "_rels/.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            AddEntry(zip, "xl/workbook.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"
                          xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Fahrzeuge" sheetId="1" r:id="rId1"/></sheets>
                </workbook>
                """);
            AddEntry(zip, "xl/_rels/workbook.xml.rels",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                </Relationships>
                """);
            AddEntry(zip, "xl/sharedStrings.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="4" uniqueCount="4">
                  <si><t>Kennzeichen</t></si><si><t>Nutzlast</t></si><si><t>HU</t></si><si><t>M-TB 100</t></si>
                </sst>
                """);
            AddEntry(zip, "xl/styles.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <cellXfs count="2">
                    <xf numFmtId="0" applyNumberFormat="0"/>
                    <xf numFmtId="14" applyNumberFormat="1"/>
                  </cellXfs>
                </styleSheet>
                """);
            AddEntry(zip, "xl/worksheets/sheet1.xml",
                """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <sheetData>
                    <row r="1">
                      <c r="A1" t="s"><v>0</v></c><c r="B1" t="s"><v>1</v></c><c r="C1" t="s"><v>2</v></c>
                    </row>
                    <row r="2">
                      <c r="A2" t="s"><v>3</v></c><c r="B2"><v>24000</v></c><c r="C2" s="1"><v>46069</v></c>
                    </row>
                  </sheetData>
                </worksheet>
                """);
        }

        return stream.ToArray();
    }

    private static void AddEntry(ZipArchive zip, string path, string content)
    {
        ZipArchiveEntry entry = zip.CreateEntry(path);
        using StreamWriter writer = new(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private static byte[] BuildSqliteDatabase()
    {
        string path = Path.Combine(Path.GetTempPath(), $"transbrain-test-{Guid.NewGuid():N}.db");
        try
        {
            using (var connection = new SqliteConnection($"Data Source={path};Pooling=False"))
            {
                connection.Open();
                using SqliteCommand command = connection.CreateCommand();
                command.CommandText =
                    """
                    CREATE TABLE kunden (name TEXT, ort TEXT, zahlungsziel INTEGER, seit TEXT);
                    INSERT INTO kunden VALUES ('Huber GmbH', 'München', 30, '2020-01-15');
                    INSERT INTO kunden VALUES ('Ohne Ort', NULL, 14, '2021-06-01');
                    """;
                command.ExecuteNonQuery();
            }

            return File.ReadAllBytes(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
