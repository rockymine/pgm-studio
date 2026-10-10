using System.Net.Http.Json;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Components;

/// <summary>
/// Every rule the studio can cite (<c>GET /api/rules</c>) and every scored term (<c>GET /api/rules/terms</c>),
/// asked once per page load and shared: the rules page lists them, and a check's problem rows read a rule's
/// meaning and fix from them. One answer serves every caller; a rule's level, which an admin may change, is
/// written back into it.
/// </summary>
public sealed class RuleBook(HttpClient http)
{
    private Task<IReadOnlyDictionary<string, RuleDto>>? rules;
    private Task<IReadOnlyList<TermDto>>? terms;

    /// <summary>The rules by id, in the order the server lists them; empty where the server did not answer.</summary>
    public Task<IReadOnlyDictionary<string, RuleDto>> RulesAsync() => rules ??= LoadRulesAsync();

    /// <summary>The scored terms; empty where the server did not answer.</summary>
    public Task<IReadOnlyList<TermDto>> TermsAsync() => terms ??= LoadTermsAsync();

    /// <summary>Set what a finding citing <paramref name="rule"/> does on this studio, or with a null
    /// <paramref name="level"/> take the setting off. The rule as the studio now answers it, or null where it
    /// refused.</summary>
    public async Task<RuleDto?> SetLevelAsync(string rule, string? level)
    {
        using var answer = level is null
            ? await http.DeleteAsync($"api/rules/{Uri.EscapeDataString(rule)}/level")
            : await http.PutAsJsonAsync($"api/rules/{Uri.EscapeDataString(rule)}/level", new RuleLevelRequest(level));
        if (!answer.IsSuccessStatusCode) return null;
        var updated = await answer.Content.ReadFromJsonAsync<RuleDto>();
        if (updated is not null && rules is { IsCompletedSuccessfully: true } loaded
            && loaded.Result is Dictionary<string, RuleDto> byId)
            byId[updated.Rule] = updated;
        return updated;
    }

    private async Task<IReadOnlyDictionary<string, RuleDto>> LoadRulesAsync()
    {
        try
        {
            var all = await http.GetFromJsonAsync<List<RuleDto>>("api/rules") ?? [];
            var byId = new Dictionary<string, RuleDto>(StringComparer.Ordinal);
            foreach (var rule in all) byId.TryAdd(rule.Rule, rule);
            return byId;
        }
        catch (HttpRequestException)
        {
            rules = null;
            return new Dictionary<string, RuleDto>();
        }
    }

    private async Task<IReadOnlyList<TermDto>> LoadTermsAsync()
    {
        try { return await http.GetFromJsonAsync<List<TermDto>>("api/rules/terms") ?? []; }
        catch (HttpRequestException)
        {
            terms = null;
            return [];
        }
    }
}
