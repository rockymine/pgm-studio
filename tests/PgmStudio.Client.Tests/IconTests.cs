using System.Text.RegularExpressions;
using LucideBlazor;

namespace PgmStudio.Client.Tests;

/// <summary>The icon names the client's markup states outright are all names lucide has. A name chosen in C# at
/// run time is not visible here; <c>Icon</c> logs an unknown one as an error, which the e2e smoke sweep fails.</summary>
public sealed partial class IconTests
{
    [GeneratedRegex(@"\b(?:Name|\w*Icon)=""([a-z0-9]+(?:-[a-z0-9]+)*)""")]
    private static partial Regex LiteralName();

    [GeneratedRegex(@"\b(?:Name|\w*Icon)=""@\((?<expression>(?:[^""\n]|""[^""\n]*"")*?)\)""")]
    private static partial Regex ExpressionName();

    [GeneratedRegex(@"""([a-z0-9]+(?:-[a-z0-9]+)*)""")]
    private static partial Regex StringLiteral();

    private static string ClientDirectory => Path.Combine(Repository.Root, "src", "PgmStudio.Client");

    public static IEnumerable<string> MarkupNames()
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var file in Directory.EnumerateFiles(ClientDirectory, "*.razor", SearchOption.AllDirectories))
        {
            var markup = File.ReadAllText(file);
            foreach (Match match in LiteralName().Matches(markup)) names.Add(match.Groups[1].Value);
            foreach (Match match in ExpressionName().Matches(markup))
                foreach (Match literal in StringLiteral().Matches(match.Groups["expression"].Value))
                    names.Add(literal.Groups[1].Value);
        }
        return names;
    }

    [Test]
    public async Task The_markup_names_icons()
    {
        await Assert.That(MarkupNames().Count()).IsGreaterThan(50);
    }

    [Test]
    [MethodDataSource(nameof(MarkupNames))]
    public async Task Lucide_has_the_icon(string name)
    {
        var component = typeof(LucideIcon).Assembly.GetType($"LucideBlazor.{PascalCase(name)}Icon");
        await Assert.That(component).IsNotNull().Because($"lucide has no icon named \"{name}\"");
    }

    private static string PascalCase(string kebab) =>
        string.Concat(kebab.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
}
