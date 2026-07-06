using VSP.Device.Import;
using VSP.Device.Import.Validation;
using Xunit;

namespace VSP.Tests.Import;

public class ImportDuplicateCheckerTests
{
    private readonly DuplicateChecker _checker = new();

    [Fact]
    public void Check_ReturnsOriginalResults_WhenThereAreNoDuplicates()
    {
        var results = CreateResults(
            CreateValidRow(2, name: "Front Door", ipAddress: "192.168.1.10", rtspUrl: "rtsp://front"),
            CreateValidRow(3, name: "Lobby", ipAddress: "192.168.1.11", rtspUrl: "rtsp://lobby"));

        var checkedResults = _checker.Check(results);

        Assert.Equal(2, checkedResults.Count);
        Assert.All(checkedResults, result =>
        {
            Assert.True(result.IsValid);
            Assert.Empty(result.Messages);
        });
    }

    [Fact]
    public void Check_AddsDuplicateNameMessages_WhenNameIsDuplicated()
    {
        var checkedResults = _checker.Check(CreateResults(
            CreateValidRow(2, name: "Front Door"),
            CreateValidRow(5, name: "Front Door")));

        AssertDuplicateMessage(checkedResults[0], "Name", "DuplicateName", "Name is duplicated with row(s): 5.");
        AssertDuplicateMessage(checkedResults[1], "Name", "DuplicateName", "Name is duplicated with row(s): 2.");
    }

    [Fact]
    public void Check_AddsDuplicateIpAddressMessages_WhenIpAddressIsDuplicated()
    {
        var checkedResults = _checker.Check(CreateResults(
            CreateValidRow(2, ipAddress: "192.168.1.10"),
            CreateValidRow(4, ipAddress: "192.168.1.10")));

        AssertDuplicateMessage(checkedResults[0], "IP Address", "DuplicateIpAddress", "IP Address is duplicated with row(s): 4.");
        AssertDuplicateMessage(checkedResults[1], "IP Address", "DuplicateIpAddress", "IP Address is duplicated with row(s): 2.");
    }

    [Fact]
    public void Check_AddsDuplicateRtspUrlMessages_WhenRtspUrlIsDuplicated()
    {
        var checkedResults = _checker.Check(CreateResults(
            CreateValidRow(2, rtspUrl: "rtsp://shared"),
            CreateValidRow(6, rtspUrl: "rtsp://shared")));

        AssertDuplicateMessage(checkedResults[0], "RTSP URL", "DuplicateRtspUrl", "RTSP URL is duplicated with row(s): 6.");
        AssertDuplicateMessage(checkedResults[1], "RTSP URL", "DuplicateRtspUrl", "RTSP URL is duplicated with row(s): 2.");
    }

    [Fact]
    public void Check_AddsSeparateMessages_WhenNameAndIpAddressAreDuplicated()
    {
        var checkedResults = _checker.Check(CreateResults(
            CreateValidRow(2, name: "Front Door", ipAddress: "192.168.1.10"),
            CreateValidRow(7, name: "Front Door", ipAddress: "192.168.1.10")));

        Assert.Equal(2, checkedResults[0].Messages.Count);
        Assert.Contains(checkedResults[0].Messages, x => x.Code == "DuplicateName");
        Assert.Contains(checkedResults[0].Messages, x => x.Code == "DuplicateIpAddress");
        Assert.Equal(2, checkedResults[1].Messages.Count);
    }

    [Fact]
    public void Check_TreatsDifferentCasingAsDuplicate()
    {
        var checkedResults = _checker.Check(CreateResults(
            CreateValidRow(2, name: "Front Door"),
            CreateValidRow(3, name: "front door")));

        AssertDuplicateMessage(checkedResults[0], "Name", "DuplicateName", "Name is duplicated with row(s): 3.");
        AssertDuplicateMessage(checkedResults[1], "Name", "DuplicateName", "Name is duplicated with row(s): 2.");
    }

    [Fact]
    public void Check_TrimsWhitespaceBeforeCheckingDuplicates()
    {
        var checkedResults = _checker.Check(CreateResults(
            CreateValidRow(2, rtspUrl: "  rtsp://shared  "),
            CreateValidRow(3, rtspUrl: "rtsp://shared")));

        AssertDuplicateMessage(checkedResults[0], "RTSP URL", "DuplicateRtspUrl", "RTSP URL is duplicated with row(s): 3.");
        AssertDuplicateMessage(checkedResults[1], "RTSP URL", "DuplicateRtspUrl", "RTSP URL is duplicated with row(s): 2.");
    }

    [Fact]
    public void Check_IgnoresEmptyValues_WhenCheckingDuplicates()
    {
        var checkedResults = _checker.Check(CreateResults(
            CreateValidRow(2, brand: "Hikvision", connectionType: "HikvisionISAPI", rtspUrl: string.Empty),
            CreateValidRow(3, brand: "Hikvision", connectionType: "HikvisionISAPI", rtspUrl: "   ")));

        Assert.All(checkedResults, result => Assert.Empty(result.Messages));
    }

    [Fact]
    public void Check_AddsMessagesForAllRows_WhenMultipleRowsAreDuplicated()
    {
        var checkedResults = _checker.Check(CreateResults(
            CreateValidRow(2, name: "Front Door"),
            CreateValidRow(5, name: "Front Door"),
            CreateValidRow(8, name: "Front Door")));

        AssertDuplicateMessage(checkedResults[0], "Name", "DuplicateName", "Name is duplicated with row(s): 5, 8.");
        AssertDuplicateMessage(checkedResults[1], "Name", "DuplicateName", "Name is duplicated with row(s): 2, 8.");
        AssertDuplicateMessage(checkedResults[2], "Name", "DuplicateName", "Name is duplicated with row(s): 2, 5.");
    }

    [Fact]
    public void Check_KeepsOriginalRowNumbers()
    {
        var checkedResults = _checker.Check(CreateResults(
            CreateValidRow(10, name: "Front Door"),
            CreateValidRow(20, name: "Front Door")));

        Assert.Equal(10, checkedResults[0].RowNumber);
        Assert.Equal(20, checkedResults[1].RowNumber);
    }

    [Fact]
    public void Check_PreservesExistingValidationMessages_AndAppendsDuplicateMessages()
    {
        var validationResults = CreateResults(
            CreateValidRow(2, name: "Front Door"),
            CreateValidRow(3, name: "Front Door"));

        var withExistingError = new ImportValidationResult
        {
            Row = validationResults[0].Row,
            RowNumber = validationResults[0].RowNumber,
            IsValid = false,
            Messages =
            [
                new ImportValidationMessage
                {
                    RowNumber = 2,
                    FieldName = "IP Address",
                    Code = "InvalidIpAddress",
                    Message = "IP Address must be a valid IPv4 address.",
                    Severity = ImportValidationSeverity.Error
                }
            ]
        };

        var checkedResults = _checker.Check([withExistingError, validationResults[1]]);

        Assert.Equal(2, checkedResults[0].Messages.Count);
        Assert.Contains(checkedResults[0].Messages, x => x.Code == "InvalidIpAddress");
        Assert.Contains(checkedResults[0].Messages, x => x.Code == "DuplicateName");
        Assert.False(checkedResults[0].IsValid);
    }

    [Fact]
    public void Check_DoesNotThrowException_WhenResultsContainDuplicates()
    {
        var results = CreateResults(
            CreateValidRow(2, name: "Front Door", ipAddress: "192.168.1.10", rtspUrl: "rtsp://shared"),
            CreateValidRow(5, name: "Front Door", ipAddress: "192.168.1.10", rtspUrl: "rtsp://shared"));

        var exception = Record.Exception(() => _checker.Check(results));
        var checkedResults = _checker.Check(results);

        Assert.Null(exception);
        Assert.All(checkedResults, result => Assert.False(result.IsValid));
    }

    private static IReadOnlyList<ImportValidationResult> CreateResults(params ImportRow[] rows)
    {
        var engine = new ImportValidationEngine();
        return engine.Validate(rows);
    }

    private static ImportRow CreateValidRow(
        int rowNumber,
        string? name = null,
        string? ipAddress = null,
        string? rtspUrl = null,
        string brand = "RTSP",
        string connectionType = "RTSP")
    {
        var row = new ImportRow { RowNumber = rowNumber };
        row.Values["Name"] = name ?? $"Camera {rowNumber}";
        row.Values["Brand"] = brand;
        row.Values["IP Address"] = ipAddress ?? $"192.168.1.{rowNumber}";
        row.Values["HTTP Port"] = "80";
        row.Values["RTSP Port"] = "554";
        row.Values["SDK Port"] = "8000";
        row.Values["Username"] = "admin";
        row.Values["Connection Type"] = connectionType;
        row.Values["RTSP URL"] = rtspUrl ?? $"rtsp://camera-{rowNumber}";
        return row;
    }

    private static void AssertDuplicateMessage(
        ImportValidationResult result,
        string fieldName,
        string code,
        string message)
    {
        var duplicateMessage = Assert.Single(result.Messages, x => x.Code == code);
        Assert.Equal(fieldName, duplicateMessage.FieldName);
        Assert.Equal(code, duplicateMessage.Code);
        Assert.Equal(message, duplicateMessage.Message);
        Assert.Equal(ImportValidationSeverity.Error, duplicateMessage.Severity);
    }
}
