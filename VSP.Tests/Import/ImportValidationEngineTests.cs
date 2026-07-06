using VSP.Device.Import;
using VSP.Device.Import.Validation;
using Xunit;

namespace VSP.Tests.Import;

public class ImportValidationEngineTests
{
    private readonly ImportValidationEngine _engine = new();

    [Theory]
    [InlineData("Name")]
    [InlineData("Brand")]
    [InlineData("IP Address")]
    [InlineData("Username")]
    [InlineData("Connection Type")]
    public void Validate_ReturnsRequiredError_WhenRequiredFieldIsMissing(string fieldName)
    {
        var row = CreateValidRow();
        row.Values[fieldName] = string.Empty;

        var result = _engine.Validate(row);

        Assert.False(result.IsValid);
        var message = Assert.Single(result.Messages);
        Assert.Equal(row, result.Row);
        Assert.Equal(row.RowNumber, result.RowNumber);
        Assert.Equal(fieldName, message.FieldName);
        Assert.Equal("Required", message.Code);
        Assert.Equal(ImportValidationSeverity.Error, message.Severity);
    }

    [Fact]
    public void Validate_ReturnsInvalidIpAddressError_WhenIpAddressIsNotIpv4()
    {
        var row = CreateValidRow();
        row.Values["IP Address"] = "camera.local";

        var result = _engine.Validate(row);

        Assert.False(result.IsValid);
        var message = Assert.Single(result.Messages);
        Assert.Equal("IP Address", message.FieldName);
        Assert.Equal("InvalidIpAddress", message.Code);
    }

    [Theory]
    [InlineData("HTTP Port", "0")]
    [InlineData("RTSP Port", "70000")]
    [InlineData("SDK Port", "abc")]
    public void Validate_ReturnsInvalidPortError_WhenPortIsOutOfRangeOrNotNumeric(string fieldName, string portValue)
    {
        var row = CreateValidRow();
        row.Values[fieldName] = portValue;

        var result = _engine.Validate(row);

        Assert.False(result.IsValid);
        var message = Assert.Single(result.Messages);
        Assert.Equal(fieldName, message.FieldName);
        Assert.Equal("InvalidPort", message.Code);
    }

    [Fact]
    public void Validate_ReturnsRequiredError_WhenRtspDeviceHasNoRtspUrl()
    {
        var row = CreateValidRow();
        row.Values["Brand"] = "RTSP";
        row.Values["RTSP URL"] = string.Empty;

        var result = _engine.Validate(row);

        Assert.False(result.IsValid);
        var message = Assert.Single(result.Messages);
        Assert.Equal("RTSP URL", message.FieldName);
        Assert.Equal("Required", message.Code);
    }

    [Fact]
    public void Validate_ReturnsInvalidRtspUrlError_WhenRtspUrlDoesNotStartWithRtspScheme()
    {
        var row = CreateValidRow();
        row.Values["Brand"] = "RTSP";
        row.Values["RTSP URL"] = "http://camera/stream";

        var result = _engine.Validate(row);

        Assert.False(result.IsValid);
        var message = Assert.Single(result.Messages);
        Assert.Equal("RTSP URL", message.FieldName);
        Assert.Equal("InvalidRtspUrl", message.Code);
    }

    [Fact]
    public void Validate_ReturnsValidResult_WhenRowIsValid()
    {
        var row = CreateValidRow();

        var result = _engine.Validate(row);

        Assert.True(result.IsValid);
        Assert.Same(row, result.Row);
        Assert.Equal(row.RowNumber, result.RowNumber);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void Validate_MultipleRows_KeepsRowNumbers()
    {
        var firstRow = CreateValidRow(2);
        var secondRow = CreateValidRow(5);
        secondRow.Values["IP Address"] = "invalid";

        var results = _engine.Validate([firstRow, secondRow]);

        Assert.Equal(2, results.Count);
        Assert.Equal(2, results[0].RowNumber);
        Assert.Equal(5, results[1].RowNumber);
        Assert.True(results[0].IsValid);
        Assert.False(results[1].IsValid);
    }

    [Fact]
    public void Validate_ReturnsErrorsInsteadOfThrowingException_WhenRowContainsInvalidValues()
    {
        var row = new ImportRow { RowNumber = 8 };
        row.Values["Name"] = string.Empty;
        row.Values["Brand"] = "RTSP";
        row.Values["IP Address"] = "not-an-ip";
        row.Values["HTTP Port"] = "-1";
        row.Values["RTSP Port"] = "70000";
        row.Values["SDK Port"] = "text";
        row.Values["Username"] = string.Empty;
        row.Values["Connection Type"] = string.Empty;
        row.Values["RTSP URL"] = "http://invalid";

        var exception = Record.Exception(() => _engine.Validate(row));
        var result = _engine.Validate(row);

        Assert.Null(exception);
        Assert.False(result.IsValid);
        Assert.True(result.Messages.Count >= 7);
    }

    private static ImportRow CreateValidRow(int rowNumber = 2)
    {
        var row = new ImportRow { RowNumber = rowNumber };
        row.Values["Name"] = "Front Door";
        row.Values["Brand"] = "Hikvision";
        row.Values["IP Address"] = "192.168.1.10";
        row.Values["HTTP Port"] = "80";
        row.Values["RTSP Port"] = "554";
        row.Values["SDK Port"] = "8000";
        row.Values["Username"] = "admin";
        row.Values["Connection Type"] = "HikvisionISAPI";
        row.Values["RTSP URL"] = "rtsp://front-door";
        return row;
    }
}
