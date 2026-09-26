using Jint;

namespace PgmStudio.Client.Tests;

/// <summary>Where the repository is, and the engine every script test runs in: modules resolve from the repository
/// root and may not reach outside it, and the globals a browser module expects of its host are supplied here.</summary>
public static class Repository
{
    /// <summary>The script tests' folder, as a path from the repository root.</summary>
    public const string ScriptsFromRoot = "tests/PgmStudio.Client.Tests/Scripts";

    /// <summary>The repository root — the nearest folder above the test binary holding the solution.</summary>
    public static string Root { get; } = FindRoot();

    public static string ScriptsDirectory => Path.Combine(Root, ScriptsFromRoot);

    /// <summary>Every script test file, by its name inside <see cref="ScriptsDirectory"/>.</summary>
    public static IReadOnlyList<string> ScriptFiles =>
        [.. Directory.GetFiles(ScriptsDirectory, "*.test.js").Select(Path.GetFileName).OfType<string>().Order(StringComparer.Ordinal)];

    /// <summary>A fresh engine whose console writes to <paramref name="output"/>.</summary>
    public static Engine NewEngine(TextWriter output)
    {
        var engine = new Engine(options => options.EnableModules(Root, restrictToBasePath: true));
        engine.SetValue("__print", new Action<string, string>((level, text) => output.WriteLine($"[{level}] {text}")));
        engine.SetValue("__readRepoFile", new Func<string, string>(ReadRepoFile));
        engine.Execute(Prelude);
        return engine;
    }

    private static string ReadRepoFile(string path)
    {
        var full = Path.GetFullPath(Path.Combine(Root, path));
        if (!full.StartsWith(Root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException($"{path} is outside the repository");
        return File.ReadAllText(full);
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "PgmStudio.slnx"))) return directory.FullName;
        throw new InvalidOperationException($"no PgmStudio.slnx above {AppContext.BaseDirectory}");
    }

    /// <summary>The host globals: a console, the repository reader, and the timer and clone functions browser
    /// modules call. A timer never fires — a case runs to completion synchronously or through its own promises.</summary>
    private const string Prelude = """
        globalThis.console = {
          log: (...parts) => __print("log", parts.map(String).join(" ")),
          info: (...parts) => __print("info", parts.map(String).join(" ")),
          debug: (...parts) => __print("debug", parts.map(String).join(" ")),
          warn: (...parts) => __print("warn", parts.map(String).join(" ")),
          error: (...parts) => __print("error", parts.map(String).join(" ")),
        };
        globalThis.__host = { readRepoFile: (path) => __readRepoFile(path) };
        {
          let nextTimer = 1;
          globalThis.setTimeout = () => nextTimer++;
          globalThis.clearTimeout = () => {};
          globalThis.setInterval = () => nextTimer++;
          globalThis.clearInterval = () => {};
          globalThis.requestAnimationFrame = () => nextTimer++;
          globalThis.cancelAnimationFrame = () => {};
        }
        {
          const started = Date.now();
          globalThis.performance = { now: () => Date.now() - started };
        }
        globalThis.structuredClone = function clone(value) {
          if (value === null || typeof value !== "object") return value;
          if (value instanceof Date) return new Date(value.getTime());
          if (value instanceof Map) return new Map([...value].map(([k, v]) => [clone(k), clone(v)]));
          if (value instanceof Set) return new Set([...value].map(clone));
          if (Array.isArray(value)) return value.map(clone);
          if (ArrayBuffer.isView(value)) return value.slice();
          const copy = {};
          for (const key of Object.keys(value)) copy[key] = clone(value[key]);
          return copy;
        };
        """;
}
