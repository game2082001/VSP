using ClosedXML.Excel;
using System.Text;
using VSP.Device.Import;
using VSP.Device.Import.Validation;
using Xunit;

namespace VSP.Tests.Import;

public class ImportPipelineServiceTests
{
    [Fact]
    public void Process_UsesCsvPipeline_AndExecutesValidationAndDuplicateStages()
    {
        const string csv = """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Connection Type,RTSP URL,Location
            Front Door,RTSP,Model A,192.168.1.10,80,554,8000,admin,RTSP,rtsp://shared,Gate
            Front Door,RTSP,Model B,invalid-ip,80,554,8000,admin,RTSP,rtsp://shared,Lobby
            """;

        using var stream = CreateUtf8Stream(csv);
        var service = new ImportPipelineService();

        var result = service.Process(stream, "csv");

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(2, result.InvalidRows);
        Assert.Equal(2, result.Results.Count);
        Assert.Contains(result.Results[0].Messages, x => x.Code == "DuplicateName");
        Assert.Contains(result.Results[0].Messages, x => x.Code == "DuplicateRtspUrl");
        Assert.False(result.Results[0].IsValid);
        Assert.Contains(result.Results[1].Messages, x => x.Code == "InvalidIpAddress");
        Assert.False(result.Results[1].IsValid);
    }

    [Fact]
    public void Process_UsesExcelPipeline_AndReturnsExpectedCounts()
    {
        using var stream = CreateWorkbook(
            [
                ["Name", "Brand", "Model", "IP Address", "HTTP Port", "RTSP Port", "SDK Port", "Username", "Connection Type", "RTSP URL", "Location"],
                ["Front Door", "RTSP", "Model A", "192.168.1.10", "80", "554", "8000", "admin", "RTSP", "rtsp://front", "Gate"],
                ["Lobby", "RTSP", "Model B", "192.168.1.11", "80", "554", "8000", "admin", "RTSP", "rtsp://lobby", "Hall"]
            ]);

        var service = new ImportPipelineService();

        var result = service.Process(stream, ".xlsx");

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(2, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.All(result.Results, validationResult => Assert.True(validationResult.IsValid));
    }

    [Fact]
    public void Process_PreservesInvalidRows_FromValidationStage()
    {
        const string csv = """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Connection Type,RTSP URL,Location
            ,RTSP,Model A,192.168.1.10,80,554,8000,admin,RTSP,rtsp://front,Gate
            """;

        using var stream = CreateUtf8Stream(csv);
        var service = new ImportPipelineService();

        var result = service.Process(stream, "text/csv");

        Assert.Equal(1, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        var validationResult = Assert.Single(result.Results);
        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Messages, x => x.Code == "Required" && x.FieldName == "Name");
    }

    [Fact]
    public void Process_PreservesDuplicateMessages_FromDuplicateStage()
    {
        const string csv = """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Connection Type,RTSP URL,Location
            Camera A,RTSP,Model A,192.168.1.10,80,554,8000,admin,RTSP,rtsp://shared,Gate
            Camera B,RTSP,Model B,192.168.1.11,80,554,8000,admin,RTSP,rtsp://shared,Hall
            """;

        using var stream = CreateUtf8Stream(csv);
        var service = new ImportPipelineService();

        var result = service.Process(stream, ".csv");

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(2, result.InvalidRows);
        Assert.All(result.Results, validationResult =>
            Assert.Contains(validationResult.Messages, x => x.Code == "DuplicateRtspUrl"));
    }

    [Fact]
    public void Process_ReturnsZeroCounts_ForEmptyCsvFile()
    {
        using var stream = CreateUtf8Stream(string.Empty);
        var service = new ImportPipelineService();

        var result = service.Process(stream, "csv");

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void Process_ReturnsZeroCounts_ForEmptyExcelWorksheet()
    {
        using var workbook = new XLWorkbook();
        workbook.AddWorksheet("Sheet1");
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        var service = new ImportPipelineService();

        var result = service.Process(stream, "xlsx");

        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
        Assert.Empty(result.Results);
    }

    [Fact]
    public void Process_ThrowsNotSupportedException_ForUnsupportedFileType()
    {
        using var stream = CreateUtf8Stream("{}");
        var service = new ImportPipelineService();

        var exception = Assert.Throws<NotSupportedException>(() => service.Process(stream, "json"));

        Assert.Contains("Unsupported import file type", exception.Message);
    }

    [Fact]
    public void Process_RejectsPasswordHeader_WithoutLeakingSecretValue()
    {
        const string csv = """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Camera A,RTSP,Model A,192.168.1.10,80,554,8000,admin,super-secret,RTSP,rtsp://front,Gate
            """;

        using var stream = CreateUtf8Stream(csv);
        var service = new ImportPipelineService();

        var exception = Assert.Throws<InvalidDataException>(() => service.Process(stream, "csv"));

        Assert.Equal("Import file contains a prohibited credential column.", exception.Message);
        Assert.DoesNotContain("super-secret", exception.Message);
    }

    [Fact]
    public void Process_PropagatesParserException_WhenParserFails()
    {
        using var stream = CreateUtf8Stream("ignored");
        var service = new ImportPipelineService(
            parsers: [new ThrowingParser()],
            validationEngine: new ImportValidationEngine(),
            duplicateChecker: new DuplicateChecker());

        var exception = Assert.Throws<InvalidOperationException>(() => service.Process(stream, "csv"));

        Assert.Equal("Parser failure.", exception.Message);
    }

    private static MemoryStream CreateUtf8Stream(string content)
    {
        return new MemoryStream(Encoding.UTF8.GetBytes(content));
    }

    private static MemoryStream CreateWorkbook(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Sheet1");

        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            for (var columnIndex = 0; columnIndex < rows[rowIndex].Count; columnIndex++)
            {
                worksheet.Cell(rowIndex + 1, columnIndex + 1).Value = rows[rowIndex][columnIndex];
            }
        }

        var stream = new MemoryStream();
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private sealed class ThrowingParser : IImportParser
    {
        public string ParserName => "Throwing";

        public bool CanParse(string fileType)
        {
            return string.Equals(fileType, "csv", StringComparison.OrdinalIgnoreCase);
        }

        public IEnumerable<ImportRow> Parse(Stream stream)
        {
            throw new InvalidOperationException("Parser failure.");
        }
    }
}
