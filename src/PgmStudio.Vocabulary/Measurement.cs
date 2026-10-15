using System.Text.Json.Serialization;

namespace PgmStudio.Vocabulary;

/// <summary>
/// The number a finding measured and the range it is held to: <see cref="Low"/> to <see cref="High"/>, either
/// edge absent where the rule states only the other — a walk of at least 20 blocks has no ceiling. A finding
/// under a <see cref="RuleLevels.Measure"/> rule is read as this rather than as its sentence.
/// </summary>
/// <param name="Value">What was measured, in the unit the rule states it in.</param>
/// <param name="Low">The least it should be, where the rule states one.</param>
/// <param name="High">The most it should be, where the rule states one.</param>
public sealed record Measurement(
    double Value,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? Low = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] double? High = null);
