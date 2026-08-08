using System.Xml.Linq;

namespace PgmStudio.Pgm;

/// <summary>
/// The shared XML fragments a map pulls in with <c>&lt;include id="…"/&gt;</c>, read from a directory of
/// <c>&lt;id&gt;.xml</c> files. This is PGM's own arrangement — it resolves an include from
/// <c>config.getIncludesDirectory()</c>, a server directory that ships with neither a map nor a map
/// repository — so the library is configuration here too, and a studio with none configured simply does not
/// resolve (<see cref="MapXml.Includes"/> still records what was referenced, and <see cref="MapValidity"/>
/// still warns).
///
/// <para>Resolution is <b>recursive</b>: a fragment may include another, and several do — <c>global</c> pulls
/// in <c>sound-keys</c> and <c>overcast-pvp</c>, <c>kotf-comp</c> reaches <c>gapple-kill-reward</c> through
/// <c>conquest-comp</c>. A cycle or a missing id resolves to nothing rather than throwing: an unresolvable
/// fragment leaves the document exactly as it would be with no library at all, which is the state every
/// consumer already handles.</para>
///
/// <para>Fragments are cached per instance and never mutated — <see cref="Fragment"/> hands out a copy,
/// because splicing reparents elements into the map being parsed.</para>
/// </summary>
public sealed class IncludeLibrary
{
    /// <summary>The fragment PGM splices into the root of <b>every</b> map, named by no map's XML
    /// (<c>MapIncludeProcessorImpl.getGlobalInclude</c>). Absent from the library is fine and common.</summary>
    public const string GlobalInclude = "global";

    private readonly string _root;
    private readonly Dictionary<string, XElement?> _cache = new(StringComparer.OrdinalIgnoreCase);

    private IncludeLibrary(string root) => _root = root;

    /// <summary>Open the library at <paramref name="directory"/>, or <c>null</c> when the path is unset or
    /// absent — the "no library configured" state, which is the default.</summary>
    public static IncludeLibrary? Open(string? directory) =>
        string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory) ? null : new IncludeLibrary(directory);

    /// <summary>The ids the library can resolve, for reporting what a corpus's references would reach.</summary>
    public IEnumerable<string> AvailableIds =>
        Directory.EnumerateFiles(_root, "*.xml").Select(Path.GetFileNameWithoutExtension)!;

    /// <summary>A private copy of the fragment's root element, or <c>null</c> when the id names no file or the
    /// file does not parse. Malformed XML resolves to nothing for the same reason a missing id does: the
    /// document should degrade to unresolved, not fail the map.</summary>
    public XElement? Fragment(string id)
    {
        if (!_cache.TryGetValue(id, out var cached))
        {
            var path = Path.Combine(_root, id + ".xml");
            try { cached = File.Exists(path) ? XDocument.Load(path).Root : null; }
            catch (System.Xml.XmlException) { cached = null; }
            _cache[id] = cached;
        }
        return cached is null ? null : new XElement(cached);
    }

    /// <summary>
    /// Splice every <c>&lt;include&gt;</c> in <paramref name="root"/> in place, depth-first, and prepend the
    /// <see cref="GlobalInclude"/> fragment the server adds to every map. Returns the ids that resolved.
    ///
    /// <para>In place is the point: PGM replaces the element with the fragment's children where it stands, so
    /// every downstream parser sees one flat document and document order — which decides constant precedence
    /// and apply-rule order — is the order the map declared.</para>
    /// </summary>
    public IReadOnlyList<string> Splice(XElement root)
    {
        var resolved = new List<string>();
        SpliceInto(root, [], resolved);

        // The global fragment is not named by any map, and goes at the root's head so a map's own declarations
        // come after it — the same position PGM gives it, and the one that lets a map override its constants.
        if (Fragment(GlobalInclude) is { } global)
        {
            SpliceInto(global, [GlobalInclude], resolved);
            resolved.Add(GlobalInclude);
            foreach (var child in global.Elements().Reverse()) root.AddFirst(new XElement(child));
        }
        return resolved;
    }

    // `open` is the chain of ids currently being expanded — a fragment that reaches itself resolves to nothing
    // rather than recursing forever.
    private void SpliceInto(XElement element, HashSet<string> open, List<string> resolved)
    {
        foreach (var include in element.Descendants("include").ToList())
        {
            var id = Xml.Get(include, "id");
            if (id.Length == 0) { include.Remove(); continue; }

            var fragment = open.Contains(id) ? null : Fragment(id);
            if (fragment is null) continue;          // unresolvable: leave the reference for Includes to record

            SpliceInto(fragment, [.. open, id], resolved);
            resolved.Add(id);
            include.ReplaceWith(fragment.Elements().Select(child => new XElement(child)));
        }
    }
}
