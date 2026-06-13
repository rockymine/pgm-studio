namespace PgmStudio.Domain;

/// <summary>
/// A PGM filter, flat across all types (discriminated by <see cref="Type"/>). Mirrors the
/// Python <c>Filter</c> hierarchy + serializer <c>_encode_filter</c>: each type emits a fixed
/// subset of these fields.
/// </summary>
public sealed class Filter
{
    public string Id = "";
    public string Type = "unknown";

    public List<string>? Children;     // all / any / one
    public string? Child;              // not / deny / allow / blocks / offset
    public string? RegionRef;          // blocks / region   (JSON key "region")
    public string? Team;               // team / variable
    public string? Material;           // material / carrying / wearing / holding
    public string? Cause;              // cause
    public int? Damage;                // carrying / wearing / holding
    public string? Enchantments;       // carrying
    public bool IgnoreMetadata;        // carrying / wearing
    public bool IgnoreDurability = true; // carrying
    public string? Duration;           // time / after / pulse
    public string? FilterRefId;        // after / pulse     (JSON key "filter")
    public string? Period;             // pulse
    public string? Vector;             // offset
    public string? Var;                // variable
    public string? Value;              // variable
    public string? Objective;          // completed / objective
    public int? Min, Max, Count;       // kill-streak / players
    public string? Name;               // class
    public string? Mob;                // spawn

    /// <summary>Transient only: a &lt;filter id="ref"/&gt; reference (type "filter"); never registered.</summary>
    public string? RefId;
}
