namespace PgmStudio.Api.Tests;

/// <summary>The seed documents under <c>tools/seeds/</c>, found by walking up from the test binary.</summary>
internal static class Seeds
{
    public static string Read(string file)
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            var candidate = Path.Combine(dir, "tools", "seeds", file);
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new FileNotFoundException($"seed {file} not found above the test binary");
    }
}
