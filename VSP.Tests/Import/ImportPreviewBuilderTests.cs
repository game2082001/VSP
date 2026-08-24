using VSP.Device.Import;
using VSP.Device.Import.Preview;
using VSP.Device.Import.Validation;
using Xunit;

namespace VSP.Tests.Import;

public class ImportPreviewBuilderTests
{
    private readonly ImportPreviewBuilder _builder = new();

    [Fact]
    public void Build_ReturnsEmptyResult_WhenPipelineResultIsNull()
    {
        var result = _builder.Build(null);

        Assert.Empty(result.Rows);
        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
    }

    [Fact]
    public void Build_ReturnsEmptyRows_WhenPipelineResultIsEmpty()
    {
        var pipelineResult = new ImportPipelineResult();

        var result = _builder.Build(pipelineResult);

        Assert.Empty(result.Rows);
        Assert.Equal(0, result.TotalRows);
        Assert.Equal(0, result.ValidRows);
        Assert.Equal(0, result.InvalidRows);
    }

    [Fact]
    public void Build_MapsSingleRow()
    {
        var validationResult = CreateValidationResult(2, isValid: true);
        var pipelineResult = CreatePipelineResult([validationResult], totalRows: 1, validRows: 1, invalidRows: 0);

        var result = _builder.Build(pipelineResult);

        var row = Assert.Single(result.Rows);
        Assert.Equal(2, row.RowNumber);
        Assert.Equal("Camera 2", row.Name);
        Assert.Equal("RTSP", row.Brand);
        Assert.Equal("Model 2", row.Model);
        Assert.Equal("192.168.1.2", row.IPAddress);
        Assert.Equal("82", row.HttpPort);
        Assert.Equal("552", row.RtspPort);
        Assert.Equal("8002", row.SdkPort);
        Assert.Equal("user2", row.Username);
        Assert.Equal("RTSP", row.ConnectionType);
        Assert.Equal("rtsp://camera-2", row.RtspUrl);
        Assert.Equal("Location 2", row.Location);
        Assert.True(row.IsValid);
        Assert.Equal("Valid", row.Status);
    }

    [Fact]
    public void Build_PreservesMultipleRows_AndRowOrder()
    {
        var first = CreateValidationResult(2, isValid: true);
        var second = CreateValidationResult(5, isValid: false);
        var third = CreateValidationResult(9, isValid: true);
        var pipelineResult = CreatePipelineResult([first, second, third], totalRows: 3, validRows: 2, invalidRows: 1);

        var result = _builder.Build(pipelineResult);

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal([2, 5, 9], result.Rows.Select(x => x.RowNumber).ToArray());
    }

    [Fact]
    public void Build_MapsInvalidRow()
    {
        var validationResult = CreateValidationResult(
            3,
            isValid: false,
            messages:
            [
                new ImportValidationMessage
                {
                    RowNumber = 3,
                    FieldName = "Name",
                    Code = "Required",
                    Message = "Name is required.",
                    Severity = ImportValidationSeverity.Error
                }
            ]);
        var pipelineResult = CreatePipelineResult([validationResult], totalRows: 1, validRows: 0, invalidRows: 1);

        var result = _builder.Build(pipelineResult);

        var row = Assert.Single(result.Rows);
        Assert.False(row.IsValid);
        Assert.Equal("Invalid", row.Status);
        Assert.Single(row.Messages);
    }

    [Fact]
    public void Build_MapsDuplicateRow()
    {
        var validationResult = CreateValidationResult(
            4,
            isValid: false,
            messages:
            [
                new ImportValidationMessage
                {
                    RowNumber = 4,
                    FieldName = "RTSP URL",
                    Code = "DuplicateRtspUrl",
                    Message = "RTSP URL is duplicated with row(s): 7.",
                    Severity = ImportValidationSeverity.Error
                }
            ]);
        var pipelineResult = CreatePipelineResult([validationResult], totalRows: 1, validRows: 0, invalidRows: 1);

        var result = _builder.Build(pipelineResult);

        var row = Assert.Single(result.Rows);
        var message = Assert.Single(row.Messages);
        Assert.Equal("DuplicateRtspUrl", message.Code);
        Assert.Equal("Invalid", row.Status);
    }

    [Fact]
    public void Build_ReusesMessagesWithoutRemapping()
    {
        var messages = new[]
        {
            new ImportValidationMessage
            {
                RowNumber = 6,
                FieldName = "IP Address",
                Code = "InvalidIpAddress",
                Message = "IP Address must be a valid IPv4 address.",
                Severity = ImportValidationSeverity.Error
            }
        };
        var validationResult = CreateValidationResult(6, isValid: false, messages: messages);
        var pipelineResult = CreatePipelineResult([validationResult], totalRows: 1, validRows: 0, invalidRows: 1);

        var result = _builder.Build(pipelineResult);

        var row = Assert.Single(result.Rows);
        Assert.Same(messages, row.Messages);
    }

    [Fact]
    public void Build_MapsSummaryCount()
    {
        var pipelineResult = CreatePipelineResult(
            [CreateValidationResult(2, true), CreateValidationResult(3, false)],
            totalRows: 2,
            validRows: 1,
            invalidRows: 1);

        var result = _builder.Build(pipelineResult);

        Assert.Equal(2, result.TotalRows);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
    }

    [Fact]
    public void Build_ReturnsEmptyStrings_ForMissingFields()
    {
        var row = new ImportRow { RowNumber = 8 };
        var validationResult = new ImportValidationResult
        {
            Row = row,
            RowNumber = 8,
            IsValid = true,
            Messages = Array.Empty<ImportValidationMessage>()
        };
        var pipelineResult = CreatePipelineResult([validationResult], totalRows: 1, validRows: 1, invalidRows: 0);

        var result = _builder.Build(pipelineResult);

        var previewRow = Assert.Single(result.Rows);
        Assert.Equal(string.Empty, previewRow.Name);
        Assert.Equal(string.Empty, previewRow.Brand);
        Assert.Equal(string.Empty, previewRow.Model);
        Assert.Equal(string.Empty, previewRow.IPAddress);
        Assert.Equal(string.Empty, previewRow.HttpPort);
        Assert.Equal(string.Empty, previewRow.RtspPort);
        Assert.Equal(string.Empty, previewRow.SdkPort);
        Assert.Equal(string.Empty, previewRow.Username);
        Assert.Equal(string.Empty, previewRow.ConnectionType);
        Assert.Equal(string.Empty, previewRow.RtspUrl);
        Assert.Equal(string.Empty, previewRow.Location);
    }

    private static ImportPipelineResult CreatePipelineResult(
        IReadOnlyList<ImportValidationResult> results,
        int totalRows,
        int validRows,
        int invalidRows)
    {
        return new ImportPipelineResult
        {
            Results = results,
            TotalRows = totalRows,
            ValidRows = validRows,
            InvalidRows = invalidRows
        };
    }

    private static ImportValidationResult CreateValidationResult(
        int rowNumber,
        bool isValid,
        IReadOnlyList<ImportValidationMessage>? messages = null)
    {
        var row = new ImportRow { RowNumber = rowNumber };
        row.Values["Name"] = $"Camera {rowNumber}";
        row.Values["Brand"] = "RTSP";
        row.Values["Model"] = $"Model {rowNumber}";
        row.Values["IP Address"] = $"192.168.1.{rowNumber}";
        row.Values["HTTP Port"] = $"8{rowNumber}";
        row.Values["RTSP Port"] = $"55{rowNumber}";
        row.Values["SDK Port"] = $"800{rowNumber}";
        row.Values["Username"] = $"user{rowNumber}";
        row.Values["Connection Type"] = "RTSP";
        row.Values["RTSP URL"] = $"rtsp://camera-{rowNumber}";
        row.Values["Location"] = $"Location {rowNumber}";

        return new ImportValidationResult
        {
            Row = row,
            RowNumber = rowNumber,
            IsValid = isValid,
            Messages = messages ?? Array.Empty<ImportValidationMessage>()
        };
    }
}
