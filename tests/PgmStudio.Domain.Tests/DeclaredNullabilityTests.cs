using System.Reflection;

namespace PgmStudio.Domain.Tests;

/// <summary>
/// The answer, and that asking for it concurrently is safe. The concurrency assertion is the one that
/// matters: a <c>NullabilityInfoContext</c> shared between threads corrupts its own cache, and the caller
/// that loses gets an <c>ArgumentException</c> out of a question with no failure mode.
/// </summary>
public sealed class DeclaredNullabilityTests
{
    private sealed record Shape(string Named, string? Unnamed)
    {
        public IReadOnlyList<int> Items { get; init; } = [];
        public IReadOnlyList<int>? Maybe { get; init; }
    }

    [Test]
    public async Task A_declared_nullable_property_is_told_from_one_that_is_not()
    {
        await Assert.That(DeclaredNullability.IsNonNullable(Property(nameof(Shape.Named)))).IsTrue();
        await Assert.That(DeclaredNullability.IsNonNullable(Property(nameof(Shape.Items)))).IsTrue();
        await Assert.That(DeclaredNullability.IsNonNullable(Property(nameof(Shape.Unnamed)))).IsFalse();
        await Assert.That(DeclaredNullability.IsNonNullable(Property(nameof(Shape.Maybe)))).IsFalse();
    }

    /// <summary>Every property of a graph deep enough to fill a context's cache, asked from many threads at
    /// once. It fails on a shared context — not always, which is the point of the repeats.</summary>
    [Test]
    [Repeat(20)]
    public async Task Asking_from_many_threads_at_once_answers_and_does_not_throw()
    {
        var properties = typeof(DeclaredNullability).Assembly.GetTypes()
            .Where(type => type is { IsPublic: true, IsClass: true } or { IsPublic: true, IsValueType: true })
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .Where(property => property.GetIndexParameters().Length == 0)
            .ToList();

        var answers = new bool[properties.Count];
        Parallel.For(0, properties.Count, at => answers[at] = DeclaredNullability.IsNonNullable(properties[at]));

        for (var at = 0; at < properties.Count; at++)
            await Assert.That(answers[at]).IsEqualTo(DeclaredNullability.IsNonNullable(properties[at]));
    }

    private static PropertyInfo Property(string name) => typeof(Shape).GetProperty(name)!;
}
