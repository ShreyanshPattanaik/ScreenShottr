using System.Drawing;
using ShottrClone;

var failures = new List<string>();

Check(
    "normalizes reversed screen points",
    CaptureGeometry.FromScreenPoints(
        new System.Windows.Point(300.8, 220.2),
        new System.Windows.Point(-100.4, 20.1)),
    Rectangle.FromLTRB(-101, 20, 301, 221));

Check(
    "projects crop coordinates at 200% image scale",
    CaptureGeometry.ProjectCrop(10, 20, 100, 50, 500, 250, 1000, 500),
    new Rectangle(20, 40, 200, 100));

Check(
    "clips a crop to bitmap bounds",
    CaptureGeometry.ProjectCrop(450, 225, 100, 100, 500, 250, 1000, 500),
    new Rectangle(900, 450, 100, 50));

Check(
    "rejects an empty canvas",
    CaptureGeometry.ProjectCrop(0, 0, 10, 10, 0, 100, 100, 100),
    Rectangle.Empty);

CheckText(
    "formats filename tokens",
    CaptureFileNaming.BuildFileName(
        "Capture_{date}_{time}",
        new DateTime(2026, 7, 27, 18, 5, 9),
        "png"),
    "Capture_20260727_180509.png");

CheckText(
    "normalizes JPEG extension",
    CaptureFileNaming.BuildFileName(
        "Screenshot_{timestamp}.png",
        new DateTime(2026, 7, 27, 18, 5, 9),
        "jpg"),
    "Screenshot_20260727_180509.jpg");

if (failures.Count > 0)
{
    Console.Error.WriteLine("Phase 0 checks failed:");
    foreach (var failure in failures)
        Console.Error.WriteLine($"- {failure}");
    return 1;
}

Console.WriteLine("Capture reliability checks passed (6/6).");
return 0;

void Check(string name, Rectangle actual, Rectangle expected)
{
    if (actual != expected)
        failures.Add($"{name}: expected {expected}, got {actual}");
}

void CheckText(string name, string actual, string expected)
{
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
        failures.Add($"{name}: expected {expected}, got {actual}");
}
