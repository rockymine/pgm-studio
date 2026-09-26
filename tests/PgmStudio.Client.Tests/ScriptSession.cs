using System.Collections.Concurrent;
using Jint;
using Jint.Native.Function;
using Jint.Runtime;

namespace PgmStudio.Client.Tests;

/// <summary>One script test file, loaded into its own Jint engine and run once: every case it registers runs in
/// declaration order against the one module instance, the way the cases were written to run, and each outcome
/// is kept for the TUnit case that reports it.</summary>
public sealed class ScriptSession
{
    /// <summary>The name a file's cases are reported under when the file itself fails to load.</summary>
    public const string LoadCase = "(the file loads)";

    private static readonly ConcurrentDictionary<string, Lazy<ScriptSession>> Sessions = new();

    private readonly List<(string Name, string? Failure, string Output)> outcomes = [];

    private ScriptSession(string file) => File = file;

    /// <summary>The test file's name, relative to <see cref="Repository.ScriptsDirectory"/>.</summary>
    public string File { get; }

    public IReadOnlyList<string> CaseNames => [.. outcomes.Select(outcome => outcome.Name)];

    /// <summary>The session for a file, loaded and run on first use.</summary>
    public static ScriptSession For(string file) =>
        Sessions.GetOrAdd(file, key => new Lazy<ScriptSession>(() => Run(key))).Value;

    /// <summary>What the named case printed, and why it failed — null when it passed.</summary>
    public (string? Failure, string Output) Outcome(string name)
    {
        var found = outcomes.Single(outcome => outcome.Name == name);
        return (found.Failure, found.Output);
    }

    private static ScriptSession Run(string file)
    {
        var session = new ScriptSession(file);
        var output = new StringWriter();
        var engine = Repository.NewEngine(output);

        try
        {
            engine.Modules.Import($"./{Repository.ScriptsFromRoot}/{file}");
        }
        catch (Exception error)
        {
            session.outcomes.Add((LoadCase, Describe(error), output.ToString()));
            return session;
        }

        var registered = engine.GetValue("__scriptTests").AsArray();
        for (uint index = 0; index < registered.Length; index++)
        {
            var entry = registered[index].AsObject();
            var name = entry.Get("name").AsString();
            var body = (Function)entry.Get("body").AsObject();
            output.GetStringBuilder().Clear();
            string? failure = null;
            try
            {
                engine.Invoke(body).UnwrapIfPromise();
            }
            catch (Exception error)
            {
                failure = Describe(error);
            }
            session.outcomes.Add((name, failure, output.ToString()));
        }
        return session;
    }

    private static string Describe(Exception error) => error switch
    {
        JavaScriptException script => $"{script.Message}\n{script.JavaScriptStackTrace}",
        PromiseRejectedException rejected when rejected.RejectedValue.IsObject() =>
            $"{TypeConverter.ToString(rejected.RejectedValue)}\n{rejected.RejectedValue.AsObject().Get("stack")}",
        PromiseRejectedException rejected => rejected.Message,
        _ => error.ToString(),
    };
}
