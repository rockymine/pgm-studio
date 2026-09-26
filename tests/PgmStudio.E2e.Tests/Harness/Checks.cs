namespace PgmStudio.E2e.Tests.Harness;

/// <summary>
/// A named list of assertions that prints a PASS/FAIL row as each is made and fails the test at
/// <see cref="Finish"/> with every failed row in the message — so one run reports all of a spec's breaks
/// rather than the first.
/// </summary>
public sealed class Checks(string title)
{
    private readonly List<(string Name, bool Ok, string Detail)> rows = [];

    public bool Add(string name, bool ok, string detail = "")
    {
        rows.Add((name, ok, detail));
        Console.WriteLine($"  {(ok ? "PASS" : "FAIL")}  {name}{(detail.Length > 0 ? $"  — {detail}" : "")}");
        return ok;
    }

    public void Section(string name) => Console.WriteLine($"\n── {name} ──");

    /// <summary>Prints the tally and fails the test when any check failed.</summary>
    public void Finish()
    {
        var failed = rows.Where(row => !row.Ok).ToList();
        var tally = $"{rows.Count - failed.Count}/{rows.Count} passed — {title}";
        Console.WriteLine($"\n{tally}");
        if (failed.Count == 0) return;

        var lines = failed.Select(row => $"  ✗ {row.Name}{(row.Detail.Length > 0 ? $"  — {row.Detail}" : "")}");
        Assert.Fail($"{tally}\nfailures:\n{string.Join("\n", lines)}");
    }
}
