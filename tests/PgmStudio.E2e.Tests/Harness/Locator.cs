using System.Text.Json;
using System.Text.Json.Serialization;

namespace PgmStudio.E2e.Tests.Harness;

/// <summary>
/// A lazily-resolved set of elements: a chain of CSS steps, each searched inside the previous step's matches
/// and optionally narrowed by the text it contains or by a descendant it holds.
///
/// Resolved in the page on every use, so it never goes stale across a re-render. Reads that name one element
/// (<see cref="TextContentAsync"/>, <see cref="ClickAsync"/>, …) wait for it to exist and act on the first
/// match; <see cref="CountAsync"/> and the <c>All</c> reads take the page as it stands.
/// </summary>
public sealed class Locator
{
    /// <summary>The in-page resolver: a function from the serialized steps to the matching elements.</summary>
    private const string Resolver = """
        (steps) => {
          const norm = s => (s ?? "").replace(/\s+/g, " ").trim();
          const contains = (el, text) => norm(el.textContent).toLowerCase().includes(norm(text).toLowerCase());
          const equals = (el, text) => norm(el.textContent) === norm(text);
          let scope = [document];
          for (const step of steps) {
            const found = [];
            for (const root of scope) {
              for (const el of root.querySelectorAll(step.css)) {
                if (found.includes(el)) continue;
                if (step.text != null && !contains(el, step.text)) continue;
                if (step.hasCss != null && ![...el.querySelectorAll(step.hasCss)].some(inner =>
                    step.hasText == null || (step.hasExact ? equals(inner, step.hasText) : contains(inner, step.hasText))))
                  continue;
                found.push(el);
              }
            }
            scope = step.first ? found.slice(0, 1) : found;
          }
          return scope;
        }
        """;

    internal sealed record Step(
        [property: JsonPropertyName("css")] string Css,
        [property: JsonPropertyName("text")] string? Text = null,
        [property: JsonPropertyName("hasCss")] string? HasCss = null,
        [property: JsonPropertyName("hasText")] string? HasText = null,
        [property: JsonPropertyName("hasExact")] bool HasExact = false,
        [property: JsonPropertyName("first")] bool First = false);

    private readonly StudioPage page;
    private readonly IReadOnlyList<Step> steps;

    internal Locator(StudioPage page, IReadOnlyList<Step> steps)
    {
        this.page = page;
        this.steps = steps;
    }

    /// <summary>Descendants matching <paramref name="css"/>, optionally only those containing <paramref name="hasText"/>.</summary>
    public Locator Locate(string css, string? hasText = null) => With(new Step(css, Text: hasText));

    /// <summary>
    /// Only the matches holding a descendant that matches <paramref name="css"/> — and, where
    /// <paramref name="text"/> is given, whose text contains it (or equals it, with <paramref name="exact"/>).
    /// </summary>
    public Locator Has(string css, string? text = null, bool exact = false) =>
        Replace(steps[^1] with { HasCss = css, HasText = text, HasExact = exact });

    /// <summary>The first match only.</summary>
    public Locator First() => Replace(steps[^1] with { First = true });

    public Task<int> CountAsync() => All<int>("els => els.length");

    public Task<string[]> AllTextContentsAsync() => All<string[]>("els => els.map(el => el.textContent ?? \"\")");

    public Task<string[]> AllInnerTextsAsync() => All<string[]>("els => els.map(el => el.innerText)");

    /// <summary>Runs <paramref name="function"/> over every current match, without waiting for one.</summary>
    public Task<T> EvaluateAllAsync<T>(string function) => All<T>(function);

    /// <summary>Runs <paramref name="function"/> on the first match, once one exists.</summary>
    public async Task<T> EvaluateAsync<T>(string function, int timeout = StudioPage.DefaultTimeout)
    {
        await WaitForAsync(timeout);
        return await page.EvaluateAsync<T>(
            $"(steps) => {{ const el = ({Resolver})(JSON.parse(steps))[0]; return ({function})(el); }}", Serialized);
    }

    public Task<string> TextContentAsync() => EvaluateAsync<string>("el => el.textContent ?? \"\"");

    public Task<string?> GetAttributeAsync(string name) =>
        EvaluateAsync<string?>($"el => el.getAttribute({JsonSerializer.Serialize(name)})");

    public Task<string> InputValueAsync() => EvaluateAsync<string>("el => el.value");

    public Task<bool> IsEnabledAsync() =>
        EvaluateAsync<bool>("el => !el.matches(':disabled') && el.getAttribute('aria-disabled') !== 'true'");

    public Task BlurAsync() => EvaluateAsync<bool>("el => { el.blur(); return true; }");

    /// <summary>The first match's box in client pixels, or null where it has none.</summary>
    public async Task<(double X, double Y, double Width, double Height)?> BoundingBoxAsync()
    {
        var box = await EvaluateAsync<JsonElement>("""
            el => { const r = el.getBoundingClientRect(); return r.width || r.height ? [r.x, r.y, r.width, r.height] : null; }
            """);
        return box.ValueKind == JsonValueKind.Array
            ? (box[0].GetDouble(), box[1].GetDouble(), box[2].GetDouble(), box[3].GetDouble())
            : null;
    }

    /// <summary>Scrolls the first match into view once it is visible and enabled, and clicks its middle.</summary>
    public async Task ClickAsync(int timeout = StudioPage.DefaultTimeout)
    {
        var point = await PollAsync("""
            el => {
              if (!el || !el.isConnected) return null;
              if (el.matches(':disabled')) return null;
              const style = getComputedStyle(el);
              if (style.visibility === "hidden") return null;
              el.scrollIntoView({ block: "center", inline: "center" });
              const r = el.getBoundingClientRect();
              if (r.width === 0 || r.height === 0) return null;
              return [r.x + r.width / 2, r.y + r.height / 2];
            }
            """, timeout, "to be visible and enabled");
        await page.Mouse.ClickAsync((decimal)point[0].GetDouble(), (decimal)point[1].GetDouble());
    }

    /// <summary>Focuses the first match, selects what it holds and types <paramref name="value"/> over it.</summary>
    public async Task FillAsync(string value)
    {
        await EvaluateAsync<bool>("el => { el.focus(); el.select?.(); return true; }");
        if (value.Length == 0) await page.Keyboard.PressAsync("Delete");
        else await page.Keyboard.SendCharacterAsync(value);
    }

    /// <summary>Selects the option whose value or label is <paramref name="option"/>, firing input and change.</summary>
    public async Task SelectOptionAsync(string option)
    {
        var chosen = await EvaluateAsync<bool>($$"""
            el => {
              const wanted = {{JsonSerializer.Serialize(option)}};
              const match = [...el.options].find(o => o.value === wanted) ?? [...el.options].find(o => o.label.trim() === wanted);
              if (!match) return false;
              el.value = match.value;
              el.dispatchEvent(new Event("input", { bubbles: true }));
              el.dispatchEvent(new Event("change", { bubbles: true }));
              return true;
            }
            """);
        if (!chosen) throw new InvalidOperationException($"no option '{option}' in {Describe()}");
    }

    /// <summary>Waits until at least one element matches.</summary>
    public Task WaitForAsync(int timeout = StudioPage.DefaultTimeout) =>
        PollAsync("el => el ? true : null", timeout, "to exist");

    private string Serialized => JsonSerializer.Serialize(steps);

    private Task<T> All<T>(string function) =>
        page.EvaluateAsync<T>($"(steps) => ({function})(({Resolver})(JSON.parse(steps)))", Serialized);

    private async Task<JsonElement> PollAsync(string function, int timeout, string waitingFor)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeout);
        while (true)
        {
            var answer = await page.EvaluateAsync(
                $"(steps) => {{ const el = ({Resolver})(JSON.parse(steps))[0]; return ({function})(el); }}", Serialized);
            if (answer.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined)) return answer;
            if (DateTime.UtcNow > deadline) throw new TimeoutException($"timed out after {timeout}ms waiting for {Describe()} {waitingFor}");
            await Task.Delay(100);
        }
    }

    private string Describe() => string.Join(" >> ", steps.Select(step =>
        step.Css
        + (step.Text != null ? $":has-text(\"{step.Text}\")" : "")
        + (step.HasCss != null ? $":has({step.HasCss}{(step.HasText != null ? $" \"{step.HasText}\"" : "")})" : "")
        + (step.First ? " >> nth=0" : "")));

    private Locator With(Step step) => new(page, [.. steps, step]);

    private Locator Replace(Step last) => new(page, [.. steps.Take(steps.Count - 1), last]);
}
