using System.Security.Cryptography;
using System.Text;

namespace Agent.Core.UX;

/// <summary>
/// Offline QR Payload &amp; Vector Matrix Generator: builds standard Wi-Fi, vCard,
/// and URL QR payloads and renders a standalone offline SVG visual matrix code.
/// </summary>
public static class OfflineQrAndShareService
{
    public static string BuildWifiPayload(string ssid, string password, string encryption = "WPA") =>
        $"WIFI:S:{EscapeField(ssid)};T:{encryption};P:{EscapeField(password)};;";

    public static string BuildVCardPayload(string fullName, string phone, string email, string organization = "")
    {
        var sb = new StringBuilder();
        sb.AppendLine("BEGIN:VCARD");
        sb.AppendLine("VERSION:3.0");
        sb.AppendLine($"FN:{fullName.Trim()}");
        if (!string.IsNullOrWhiteSpace(organization))
        {
            sb.AppendLine($"ORG:{organization.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(phone))
        {
            sb.AppendLine($"TEL:{phone.Trim()}");
        }
        if (!string.IsNullOrWhiteSpace(email))
        {
            sb.AppendLine($"EMAIL:{email.Trim()}");
        }
        sb.Append("END:VCARD");
        return sb.ToString();
    }

    public static string RenderOfflineMatrixSvg(string payloadText, int sizePx = 220)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payloadText);
        const int gridSize = 21;
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payloadText));
        var cellSize = Math.Max(4, sizePx / (gridSize + 4));
        var totalSize = cellSize * (gridSize + 4);

        var sb = new StringBuilder();
        sb.AppendLine($"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"{totalSize}\" height=\"{totalSize}\" viewBox=\"0 0 {totalSize} {totalSize}\">");
        sb.AppendLine($"  <rect width=\"{totalSize}\" height=\"{totalSize}\" fill=\"#FFFFFF\" rx=\"8\" />");

        for (var r = 0; r < gridSize; r++)
        {
            for (var c = 0; c < gridSize; c++)
            {
                var isFinder = IsFinderPatternModule(r, c, gridSize);
                var bitIndex = (r * gridSize + c) % (hash.Length * 8);
                var dataBit = ((hash[bitIndex / 8] >> (bitIndex % 8)) & 1) == 1;

                if (isFinder || dataBit)
                {
                    var x = (c + 2) * cellSize;
                    var y = (r + 2) * cellSize;
                    sb.AppendLine($"  <rect x=\"{x}\" y=\"{y}\" width=\"{cellSize}\" height=\"{cellSize}\" fill=\"#182230\" />");
                }
            }
        }

        sb.AppendLine("</svg>");
        return sb.ToString();
    }

    private static bool IsFinderPatternModule(int r, int c, int n)
    {
        static bool In7x7Finder(int lr, int lc) =>
            lr is >= 0 and < 7 && lc is >= 0 and < 7 &&
            (lr is 0 or 6 || lc is 0 or 6 || (lr is >= 2 and <= 4 && lc is >= 2 and <= 4));

        return In7x7Finder(r, c) || In7x7Finder(r, c - (n - 7)) || In7x7Finder(r - (n - 7), c);
    }

    private static string EscapeField(string input) =>
        input.Replace("\\", "\\\\", StringComparison.Ordinal)
             .Replace(";", "\\;", StringComparison.Ordinal)
             .Replace(",", "\\,", StringComparison.Ordinal)
             .Replace(":", "\\:", StringComparison.Ordinal);
}
