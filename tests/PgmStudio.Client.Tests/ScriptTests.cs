namespace PgmStudio.Client.Tests;

/// <summary>The client's browser modules, tested by the cases under <c>Scripts/</c>. Each case a script file
/// registers with <c>test(name, body)</c> is one test here, reported by its file and name.</summary>
public sealed class ScriptTests
{
    public sealed record ScriptCase(string File, string Name)
    {
        public override string ToString() => $"{File} › {Name}";
    }

    public static IEnumerable<Func<ScriptCase>> Cases()
    {
        foreach (var file in Repository.ScriptFiles)
            foreach (var name in ScriptSession.For(file).CaseNames)
                yield return () => new ScriptCase(file, name);
    }

    [Test]
    [MethodDataSource(nameof(Cases))]
    [DisplayName("$scriptCase")]
    public void Passes(ScriptCase scriptCase)
    {
        var (failure, output) = ScriptSession.For(scriptCase.File).Outcome(scriptCase.Name);
        if (output.Length > 0) Console.Write(output);
        if (failure is not null) Assert.Fail(failure);
    }
}
