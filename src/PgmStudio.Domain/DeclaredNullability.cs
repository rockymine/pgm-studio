using System.Collections.Concurrent;
using System.Reflection;

namespace PgmStudio.Domain;

/// <summary>
/// Whether a property is declared non-nullable — the compile-time annotation, read at runtime.
///
/// <para><b>A <see cref="NullabilityInfoContext"/> may not be shared.</b> It caches into a
/// <see cref="Dictionary{TKey,TValue}"/> of its own, so two threads asking one at the same time corrupt that
/// dictionary and the loser gets an <see cref="ArgumentException"/> out of a question that has no failure
/// mode — a 500 on one of two concurrent requests, and a test that fails about one run in six. The answer is
/// pure, so it is computed once per property against a context nothing else holds and kept here.</para>
///
/// <para>Two readers ask it: the request gate refuses a body that left out a field the DTO declares
/// non-nullable, and a document reader refuses a part written as <c>null</c> where the record cannot hold
/// one. Both want the same fact about the same kind of member, so it is one cache in the lowest project both
/// reach rather than one per caller.</para>
/// </summary>
public static class DeclaredNullability
{
    private static readonly ConcurrentDictionary<PropertyInfo, bool> NonNullableByProperty = new();

    /// <summary>Whether reading <paramref name="property"/> is declared never to answer null.</summary>
    public static bool IsNonNullable(PropertyInfo property) =>
        NonNullableByProperty.GetOrAdd(property, static candidate =>
            new NullabilityInfoContext().Create(candidate).ReadState is NullabilityState.NotNull);
}
