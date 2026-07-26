using VSP.Device.Discovery.NetworkScan;
using VSP.Device.Discovery.Workspace;
using Xunit;

namespace VSP.Tests.Discovery.Workspace;

public class NetworkScanTargetParserTests
{
    [Fact]
    public void Parse_WithNullOrWhitespace_ReturnsEmpty()
    {
        Assert.Empty(NetworkScanTargetParser.Parse(null, 80));
        Assert.Empty(NetworkScanTargetParser.Parse("   ", 80));
    }

    [Fact]
    public void Parse_WithSingleHost_ReturnsOneTarget()
    {
        var targets = NetworkScanTargetParser.Parse("192.168.1.10", 80);

        var target = Assert.Single(targets);
        Assert.Equal(NetworkScanTargetKind.HostPort, target.Kind);
        Assert.Equal("192.168.1.10", target.Host);
        Assert.Equal(80, target.Port);
    }

    [Fact]
    public void Parse_WithCommaAndNewlineSeparatedHosts_ReturnsAllTargets()
    {
        var targets = NetworkScanTargetParser.Parse("192.168.1.10, 192.168.1.11\n192.168.1.12", 554);

        Assert.Equal(3, targets.Count);
        Assert.Contains(targets, target => target.Host == "192.168.1.10");
        Assert.Contains(targets, target => target.Host == "192.168.1.11");
        Assert.Contains(targets, target => target.Host == "192.168.1.12");
    }

    [Fact]
    public void Parse_DeduplicatesCaseInsensitiveHosts()
    {
        var targets = NetworkScanTargetParser.Parse("nvr.local, NVR.LOCAL", 80);

        Assert.Single(targets);
    }

    [Fact]
    public void Parse_WithLastOctetRange_ExpandsToIndividualHosts()
    {
        var targets = NetworkScanTargetParser.Parse("192.168.1.10-12", 80);

        Assert.Equal(3, targets.Count);
        Assert.Contains(targets, target => target.Host == "192.168.1.10");
        Assert.Contains(targets, target => target.Host == "192.168.1.11");
        Assert.Contains(targets, target => target.Host == "192.168.1.12");
    }

    [Fact]
    public void Parse_WithInvalidRange_FallsBackToLiteralHostname()
    {
        var targets = NetworkScanTargetParser.Parse("my-nvr", 80);

        var target = Assert.Single(targets);
        Assert.Equal("my-nvr", target.Host);
    }

    [Fact]
    public void Parse_WithFullOctetRange_ExpandsToTwoHundredFiftySixHosts()
    {
        var targets = NetworkScanTargetParser.Parse("192.168.1.0-255", 80);

        Assert.Equal(256, targets.Count);
    }

    [Fact]
    public void Parse_WithOutOfRangeOctets_FallsBackToLiteralEntry()
    {
        var targets = NetworkScanTargetParser.Parse("192.168.1.10-300", 80);

        var target = Assert.Single(targets);
        Assert.Equal("192.168.1.10-300", target.Host);
    }
}
