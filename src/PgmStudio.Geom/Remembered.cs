using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace PgmStudio.Geom;

/// <summary>
/// The answers of a pure function, kept for the few documents most recently asked about and keyed on the
/// documents' own bytes — so an edited document is a different key, and no stale answer can be served.
/// <para>Concurrent askers of one key wait on one computation. A computation that throws is forgotten, so the
/// next asker meets the same exception afresh. A kept answer is shared by every caller that asks for it, so it
/// is read and never written: a caller that changes what it is handed copies it first.</para>
/// </summary>
public sealed class Remembered<T>(int capacity)
{
    private readonly ConcurrentDictionary<string, Entry> _held = new();
    private long _clock;

    private sealed class Entry(Lazy<T> answer)
    {
        public Lazy<T> Answer { get; } = answer;
        public long Used;
    }

    /// <summary>The answer for <paramref name="document"/> — <paramref name="compute"/>'s, the first time it is
    /// asked.</summary>
    public T Of(ReadOnlySpan<byte> document, Func<T> compute)
    {
        var key = Convert.ToHexString(SHA256.HashData(document));
        var entry = _held.GetOrAdd(key, _ => new Entry(new Lazy<T>(compute, LazyThreadSafetyMode.ExecutionAndPublication)));
        Interlocked.Exchange(ref entry.Used, Interlocked.Increment(ref _clock));

        T answer;
        try { answer = entry.Answer.Value; }
        catch
        {
            _held.TryRemove(new KeyValuePair<string, Entry>(key, entry));
            throw;
        }

        if (_held.Count > capacity)
            foreach (var stale in _held.OrderBy(pair => pair.Value.Used).Take(_held.Count - capacity).ToList())
                _held.TryRemove(stale);
        return answer;
    }
}
