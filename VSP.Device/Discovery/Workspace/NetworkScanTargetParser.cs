using System.Net;
using VSP.Device.Discovery.NetworkScan;

namespace VSP.Device.Discovery.Workspace;

public static class NetworkScanTargetParser
{
    private const int MaxExpandedHostsPerEntry = 256;

    public static IReadOnlyList<NetworkScanTarget> Parse(string? hostsText, int port)
    {
        if (string.IsNullOrWhiteSpace(hostsText))
        {
            return Array.Empty<NetworkScanTarget>();
        }

        var entries = hostsText.Split(
            [',', ';', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var hosts = new List<string>();
        foreach (var entry in entries)
        {
            hosts.AddRange(ExpandEntry(entry));
        }

        return hosts
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(host => NetworkScanTarget.ForHostPort(host, port))
            .ToArray();
    }

    private static IReadOnlyList<string> ExpandEntry(string entry)
    {
        var dashIndex = entry.IndexOf('-');
        if (dashIndex <= 0 || dashIndex == entry.Length - 1)
        {
            return [entry];
        }

        var start = entry[..dashIndex].Trim();
        var endPart = entry[(dashIndex + 1)..].Trim();
        var lastDot = start.LastIndexOf('.');

        if (lastDot < 0
            || !IPAddress.TryParse(start, out _)
            || !int.TryParse(start[(lastDot + 1)..], out var startOctet)
            || !int.TryParse(endPart, out var endOctet)
            || startOctet is < 0 or > 255
            || endOctet < startOctet
            || endOctet > 255)
        {
            return [entry];
        }

        var count = endOctet - startOctet + 1;
        if (count > MaxExpandedHostsPerEntry)
        {
            return [entry];
        }

        var prefix = start[..(lastDot + 1)];
        var expanded = new List<string>(count);
        for (var octet = startOctet; octet <= endOctet; octet++)
        {
            expanded.Add(prefix + octet);
        }

        return expanded;
    }
}
