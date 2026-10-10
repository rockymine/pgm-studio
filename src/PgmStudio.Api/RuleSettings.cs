using PgmStudio.Api.Endpoints;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api;

/// <summary>
/// Reads which rules stop the work from the configuration: <c>Rules:Mode</c> (<c>full</c>, the default, or
/// <c>minimal</c>) and <c>Rules:Levels:&lt;rule&gt;</c> (<c>refuse</c> or <c>hint</c>). A level naming a rule the
/// studio does not declare, or a word that is not a level, stops the studio starting, because a setting that
/// silently does nothing is the fault a setting must not have.
/// </summary>
public static class RuleSettings
{
    public static void Configure(IConfiguration configuration)
    {
        var mode = configuration["Rules:Mode"];
        if (string.Equals(mode, "minimal", StringComparison.OrdinalIgnoreCase))
            RulePolicy.UseMinimal(MinimalRules.Enforced());
        else if (mode is not null && !string.Equals(mode, "full", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Rules:Mode '{mode}' is not one of full, minimal");

        var levels = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in configuration.GetSection("Rules:Levels").GetChildren())
        {
            if (!RulesEndpoint.Ids.Contains(entry.Key))
                throw new InvalidOperationException($"Rules:Levels:{entry.Key} names no rule the studio declares");
            if (!RuleLevels.IsValid(entry.Value))
                throw new InvalidOperationException(
                    $"Rules:Levels:{entry.Key} is '{entry.Value}', not one of {string.Join(", ", RuleLevels.All)}");
            levels[entry.Key] = entry.Value!;
        }
        RulePolicy.Configure(levels);
    }
}
