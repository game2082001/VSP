using System.Net;
using System.Text.RegularExpressions;

namespace VSP.UI.Validation;

public static class CameraValidator
{
    public static bool IsRequired(string? value)
    {
        return !string.IsNullOrWhiteSpace(value);
    }

    public static bool IsValidIPv4(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return false;

        if (!Regex.IsMatch(ip, @"^(\d{1,3}\.){3}\d{1,3}$"))
            return false;

        string[] parts = ip.Split('.');

        foreach (string part in parts)
        {
            if (!int.TryParse(part, out int value))
                return false;

            if (value < 0 || value > 255)
                return false;
        }

        return IPAddress.TryParse(ip, out _);
    }

    public static bool IsValidPort(int port)
    {
        return port >= 1 && port <= 65535;
    }
}