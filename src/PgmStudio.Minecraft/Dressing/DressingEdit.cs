namespace PgmStudio.Minecraft.Dressing;

/// <summary>
/// The three edits a dressing document takes one placement at a time: add one, replace one, remove one.
///
/// <para>Pure, and separate from the plumbing that reads and stores the layout the document rides in, for the
/// reason <c>MapEdit</c> and the intent editors are separate: what an edit <em>means</em> is a fact about the
/// document, and what it costs to persist is a fact about the store. A caller holding a
/// <see cref="DressingDoc"/> gets the same answer whether it came off a map row or out of a test.</para>
///
/// <para>Every edit answers <see cref="DressingEditResult.Missing"/> rather than throwing where the id names
/// no placement, because a stale id is the ordinary case for a client that had the document a moment ago and
/// not the exceptional one.</para>
/// </summary>
public static class DressingEdit
{
    /// <summary>The document with <paramref name="prop"/> appended, carrying an id: the one it states where
    /// it states a free one, and a minted <c>{kind}-{n}</c> otherwise. Placement order is the order props were
    /// placed in, so an addition goes on the end.</summary>
    public static DressingEditResult Add(DressingDoc doc, PlacedProp prop)
    {
        var taken = doc.Props.Select(placed => placed.Id).ToHashSet(StringComparer.Ordinal);
        var id = prop.Id.Length > 0 && !taken.Contains(prop.Id) ? prop.Id : Mint(prop, taken);
        return new(doc with { Props = [.. doc.Props, prop with { Id = id }] }, id);
    }

    /// <summary>The document with the placement at <paramref name="id"/> replaced, keeping its position in the
    /// order — a prop edited in place is the same prop, and moving it to the end would move it past everything
    /// the pass places after it. The replacement keeps the id it is addressed by, whatever the body says.</summary>
    public static DressingEditResult Replace(DressingDoc doc, string id, PlacedProp prop)
    {
        var at = IndexOf(doc, id);
        if (at < 0) return DressingEditResult.Missing;
        var props = new List<PlacedProp>(doc.Props) { [at] = prop with { Id = id } };
        return new(doc with { Props = props }, id);
    }

    /// <summary>The document without the placement at <paramref name="id"/>. The recipes it named are left in
    /// the registry: a key is shared by every placement wearing it, and reference-counting them here would
    /// drop a style the moment its last placement moved.</summary>
    public static DressingEditResult Remove(DressingDoc doc, string id)
    {
        var at = IndexOf(doc, id);
        if (at < 0) return DressingEditResult.Missing;
        var props = new List<PlacedProp>(doc.Props);
        props.RemoveAt(at);
        return new(doc with { Props = props }, id);
    }

    private static int IndexOf(DressingDoc doc, string id) =>
        doc.Props.FindIndex(prop => string.Equals(prop.Id, id, StringComparison.Ordinal));

    /// <summary>The lowest <c>{kind}-{n}</c> no placement holds. Named for the kind so a document read by hand
    /// says what each id is, the same shape a plan's markers take.</summary>
    private static string Mint(PlacedProp prop, IReadOnlySet<string> taken)
    {
        var kind = PlacedProp.KindOf(prop) ?? "prop";
        var next = 1;
        while (taken.Contains($"{kind}-{next}")) next++;
        return $"{kind}-{next}";
    }
}

/// <summary>What an edit did: the document it produced and the id it acted on, or <see cref="Missing"/> where
/// the id named no placement. <see cref="Doc"/> is null exactly when the edit did not happen, so a caller
/// tests one field rather than two.</summary>
public readonly record struct DressingEditResult(DressingDoc? Doc, string Id)
{
    public static DressingEditResult Missing => new(null, "");
    public bool Applied => Doc is not null;
}
