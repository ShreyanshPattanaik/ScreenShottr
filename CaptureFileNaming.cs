using System.IO;

namespace ShottrClone;

internal static class CaptureFileNaming
{
    public static string BuildFileName(string pattern, DateTime timestamp, string format)
    {
        var safePattern = string.IsNullOrWhiteSpace(pattern)
            ? "Screenshot_{timestamp}"
            : pattern.Trim();
        var value = safePattern
            .Replace("{timestamp}", timestamp.ToString("yyyyMMdd_HHmmss"), StringComparison.OrdinalIgnoreCase)
            .Replace("{date}", timestamp.ToString("yyyyMMdd"), StringComparison.OrdinalIgnoreCase)
            .Replace("{time}", timestamp.ToString("HHmmss"), StringComparison.OrdinalIgnoreCase);

        foreach (var invalid in Path.GetInvalidFileNameChars())
            value = value.Replace(invalid, '_');

        value = value.Trim().TrimEnd('.');
        if (string.IsNullOrWhiteSpace(value))
            value = $"Screenshot_{timestamp:yyyyMMdd_HHmmss}";

        var extension = string.Equals(format, "jpg", StringComparison.OrdinalIgnoreCase)
            || string.Equals(format, "jpeg", StringComparison.OrdinalIgnoreCase)
                ? ".jpg"
                : ".png";
        return Path.ChangeExtension(value, extension);
    }
}
