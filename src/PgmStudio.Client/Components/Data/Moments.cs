using System.Globalization;

namespace PgmStudio.Client.Components;

/// <summary>When something happened, in the words the studio's lists read in.</summary>
public static class Moments
{
    public static string Ago(DateTime at)
    {
        var since = DateTime.UtcNow - DateTime.SpecifyKind(at, DateTimeKind.Utc);
        return since.TotalMinutes < 1 ? "just now"
            : since.TotalHours < 1 ? $"{(int)since.TotalMinutes} min ago"
            : since.TotalDays < 1 ? $"{(int)since.TotalHours} h ago"
            : since.TotalDays < 2 ? "yesterday"
            : since.TotalDays < 7 ? $"{(int)since.TotalDays} days ago"
            : at.ToLocalTime().ToString("dd.MM.yyyy", CultureInfo.InvariantCulture);
    }
}
