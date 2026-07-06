using ClosedXML.Excel;
using VSP.Device.Import;
using Xunit;

namespace VSP.Tests.Import;

public class ExcelImportParserTests
{
    private readonly ExcelImportParser _parser = new();

    [Theory]
    [InlineData("xlsx")]
    [InlineData(".xlsx")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public void CanParse_ReturnsTrue_ForSupportedFileTypes(string fileType)
    {
        Assert.True(_parser.CanParse(fileType));
    }

    [Fact]
    public void Parse_ReadsFirstWorksheet_AndReturnsImportRows()
    {
        using var stream = CreateWorkbook(
            [
                ["name", "brand", "model", "ip address", "http port", "rtsp port", "sdk port", "username", "password", "connection type", "rtsp url", "location"],
                ["Front Door", "Hikvision", "DS-2CD", "192.168.1.10", "80", "554", "8000", "admin", "1234", "HikvisionISAPI", "rtsp://front", "Gate"],
                ["", "", "", "", "", "", "", "", "", "", "", ""],
                ["Lobby", "RTSP", "Generic", "192.168.1.11", "81", "8554", "9000", "user", "pass", "RTSP", "rtsp://lobby", "Hall"]
            ],
            [
                ["Name", "Brand"],
                ["Should Not", "Be Read"]
            ]);

        var rows = _parser.Parse(stream).ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].RowNumber);
        Assert.Equal("Front Door", rows[0].Values["Name"]);
        Assert.Equal("192.168.1.10", rows[0].Values["IP Address"]);

        Assert.Equal(4, rows[1].RowNumber);
        Assert.Equal("Lobby", rows[1].Values["Name"]);
        Assert.Equal("Hall", rows[1].Values["Location"]);
    }

    [Fact]
    public void Parse_PreservesBlankCells_AsEmptyStrings()
    {
        using var stream = CreateWorkbook(
            [
                ["Name", "Brand", "Model", "IP Address", "HTTP Port", "RTSP Port", "SDK Port", "Username", "Password", "Connection Type", "RTSP URL", "Location"],
                ["Camera A", "ONVIF", "", "10.0.0.5", "80", "554", "8000", "admin", "", "ONVIF", "", "Office"]
            ]);

        var row = Assert.Single(_parser.Parse(stream));

        Assert.Equal(string.Empty, row.Values["Model"]);
        Assert.Equal(string.Empty, row.Values["Password"]);
        Assert.Equal(string.Empty, row.Values["RTSP URL"]);
    }

    [Fact]
    public void Parse_NormalizesHeaders_CaseInsensitively()
    {
        using var stream = CreateWorkbook(
            [
                ["NAME", "BRAND", "MODEL", "IP ADDRESS", "HTTP PORT", "RTSP PORT", "SDK PORT", "USERNAME", "PASSWORD", "CONNECTION TYPE", "RTSP URL", "LOCATION"],
                ["Camera B", "Dahua", "Model B", "10.0.0.6", "80", "554", "8000", "admin", "1234", "DahuaNetSDK", "rtsp://camera-b", "Warehouse"]
            ]);

        var row = Assert.Single(_parser.Parse(stream));

        Assert.Equal("Camera B", row.Values["Name"]);
        Assert.Equal("10.0.0.6", row.Values["IP Address"]);
        Assert.Equal("DahuaNetSDK", row.Values["Connection Type"]);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenWorksheetHasNoUsedRange()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Sheet1");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;

        Assert.Empty(_parser.Parse(stream));
    }

    [Fact]
    public void Parse_Throws_WhenStreamIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
    }

    private static MemoryStream CreateWorkbook(
        IReadOnlyList<IReadOnlyList<string>> firstSheetRows,
        IReadOnlyList<IReadOnlyList<string>>? secondSheetRows = null)
    {
        using var workbook = new XLWorkbook();
        AddSheet(workbook, "Sheet1", firstSheetRows);

        if (secondSheetRows is not null)
        {
            AddSheet(workbook, "Sheet2", secondSheetRows);
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private static void AddSheet(XLWorkbook workbook, string sheetName, IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var worksheet = workbook.Worksheets.Add(sheetName);

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                worksheet.Cell(rowIndex + 1, columnIndex + 1).Value = rows[rowIndex][columnIndex];
            }
        }
    }
}
