using System.Text;
using VSP.Device.Import;
using Xunit;

namespace VSP.Tests.Import;

public class CsvImportParserTests
{
    private readonly CsvImportParser _parser = new();

    [Theory]
    [InlineData("csv")]
    [InlineData(".csv")]
    [InlineData("text/csv")]
    public void CanParse_ReturnsTrue_ForSupportedFileTypes(string fileType)
    {
        Assert.True(_parser.CanParse(fileType));
    }

    [Fact]
    public void Parse_ReturnsRows_WithNormalizedHeadersAndRowNumbers()
    {
        const string csv = """
            name,brand,model,ip address,http port,rtsp port,sdk port,username,connection type,rtsp url,location
            Front Door,Hikvision,DS-2CD,192.168.1.10,80,554,8000,admin,HikvisionISAPI,rtsp://front,Gate
            
            Lobby,RTSP,Generic,192.168.1.11,81,8554,9000,user,RTSP,"rtsp://lobby/main,sub",Hall
            """;

        using var stream = CreateStream(csv, new UTF8Encoding(false));

        var rows = _parser.Parse(stream).ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(2, rows[0].RowNumber);
        Assert.Equal("Front Door", rows[0].Values["Name"]);
        Assert.Equal("192.168.1.10", rows[0].Values["IP Address"]);
        Assert.Equal("HikvisionISAPI", rows[0].Values["Connection Type"]);

        Assert.Equal(4, rows[1].RowNumber);
        Assert.Equal("Lobby", rows[1].Values["Name"]);
        Assert.Equal("rtsp://lobby/main,sub", rows[1].Values["RTSP URL"]);
        Assert.False(rows[0].Values.ContainsKey("Password"));
    }

    [Fact]
    public void Parse_SupportsQuotedFields_AndCommaInsideQuotedField()
    {
        const string csv = """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Connection Type,RTSP URL,Location
            "Camera, A",RTSP,Model A,10.0.0.1,80,554,8000,admin,RTSP,"rtsp://10.0.0.1/stream,1","Room, A"
            """;

        using var stream = CreateStream(csv, new UTF8Encoding(false));

        var row = Assert.Single(_parser.Parse(stream));

        Assert.Equal("Camera, A", row.Values["Name"]);
        Assert.Equal("rtsp://10.0.0.1/stream,1", row.Values["RTSP URL"]);
        Assert.Equal("Room, A", row.Values["Location"]);
    }

    [Fact]
    public void Parse_ReturnsEmpty_WhenContentIsEmpty()
    {
        using var stream = CreateStream(string.Empty, new UTF8Encoding(false));

        Assert.Empty(_parser.Parse(stream));
    }

    [Fact]
    public void Parse_Throws_WhenStreamIsNull()
    {
        Assert.Throws<ArgumentNullException>(() => _parser.Parse(null!));
    }

    [Fact]
    public void Parse_RejectsEntireFile_WhenPasswordHeaderIsPresent()
    {
        const string csv = """
            Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Password,Connection Type,RTSP URL,Location
            Front Door,Hikvision,DS-2CD,192.168.1.10,80,554,8000,admin,secret,HikvisionISAPI,rtsp://front,Gate
            """;

        using var stream = CreateStream(csv, new UTF8Encoding(false));

        var exception = Assert.Throws<InvalidDataException>(() => _parser.Parse(stream).ToList());

        Assert.Equal("Import file contains a prohibited credential column.", exception.Message);
        Assert.DoesNotContain("secret", exception.Message);
    }

    [Fact]
    public void Parse_SupportsUtf8Encoding()
    {
        var csv = "Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Connection Type,RTSP URL,Location\r\n" +
                  "\u6E2C\u8A66\u651D\u5F71\u6A5F,Hikvision,\u578B\u865FA,192.168.10.1,80,554,8000,admin,HikvisionISAPI,rtsp://test/1,\u5927\u5EF3";

        using var stream = CreateStream(csv, Encoding.UTF8);

        var row = Assert.Single(_parser.Parse(stream));

        Assert.Equal("\u6E2C\u8A66\u651D\u5F71\u6A5F", row.Values["Name"]);
        Assert.Equal("\u5927\u5EF3", row.Values["Location"]);
    }

    [Fact]
    public void Parse_SupportsUtf8BomEncoding()
    {
        var csv = "Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Connection Type,RTSP URL,Location\r\n" +
                  "\u6E2C\u8A66BOM,Hikvision,\u578B\u865FB,192.168.10.2,80,554,8000,admin,HikvisionISAPI,rtsp://test/2,\u5165\u53E3";

        using var stream = CreateStream(csv, new UTF8Encoding(true));

        var row = Assert.Single(_parser.Parse(stream));

        Assert.Equal("\u6E2C\u8A66BOM", row.Values["Name"]);
        Assert.Equal("\u5165\u53E3", row.Values["Location"]);
    }

    [Fact]
    public void Parse_SupportsUtf8WithoutBomEncoding()
    {
        var csv = "Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Connection Type,RTSP URL,Location\r\n" +
                  "\u7121BOM\u6E2C\u8A66,Hikvision,\u578B\u865FC,192.168.10.3,80,554,8000,admin,HikvisionISAPI,rtsp://test/3,\u5009\u5EAB";

        using var stream = CreateStream(csv, new UTF8Encoding(false));

        var row = Assert.Single(_parser.Parse(stream));

        Assert.Equal("\u7121BOM\u6E2C\u8A66", row.Values["Name"]);
        Assert.Equal("\u5009\u5EAB", row.Values["Location"]);
    }

    [Fact]
    public void Parse_SupportsBig5Encoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var csv = "Name,Brand,Model,IP Address,HTTP Port,RTSP Port,SDK Port,Username,Connection Type,RTSP URL,Location\r\n" +
                  "\u5927\u9580\u651D\u5F71\u6A5F,Hikvision,\u578B\u865FD,192.168.10.4,80,554,8000,admin,HikvisionISAPI,rtsp://test/4,\u5927\u9580";

        using var stream = CreateStream(csv, Encoding.GetEncoding(950));

        var row = Assert.Single(_parser.Parse(stream));

        Assert.Equal("\u5927\u9580\u651D\u5F71\u6A5F", row.Values["Name"]);
        Assert.Equal("\u5927\u9580", row.Values["Location"]);
    }

    private static MemoryStream CreateStream(string content, Encoding encoding)
    {
        var preamble = encoding.GetPreamble();
        var body = encoding.GetBytes(content);
        var bytes = new byte[preamble.Length + body.Length];

        Buffer.BlockCopy(preamble, 0, bytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, bytes, preamble.Length, body.Length);

        return new MemoryStream(bytes);
    }
}
