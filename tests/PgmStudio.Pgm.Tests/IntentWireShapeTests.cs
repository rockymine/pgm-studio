using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using PgmStudio.Pgm.Authoring;

namespace PgmStudio.Pgm.Tests;

/// <summary>
/// What the intent can be posted as, over the whole record graph rather than one field at a time.
///
/// <para><b>A positional record <em>struct</em> loses its string defaults on the wire.</b> Every struct has
/// an implicit parameterless constructor, and <c>System.Text.Json</c> takes that one in preference to the
/// primary constructor unless the primary is marked — so a member the body left out arrives as
/// <c>default</c>, which for a string is <b>null</b> rather than the empty string its parameter states. A
/// generator then reads <c>.Length</c> or <c>.Trim()</c> off it and the request answers <c>RQ2</c>.</para>
///
/// <para>A record <em>class</em> with init properties does not have the problem: its initializers run
/// whatever the constructor does. So the rule this holds is narrow — a positional record struct reachable
/// from <see cref="MapIntent"/> and carrying a string has to name its constructor.</para>
/// </summary>
public sealed class IntentWireShapeTests
{
    private static readonly JsonSerializerOptions Wire = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>Every type the intent can carry, followed through lists and nested records.</summary>
    private static IEnumerable<Type> Reachable(Type root)
    {
        var seen = new HashSet<Type>();
        var queue = new Queue<Type>([root]);
        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            if (type.IsPrimitive || type == typeof(string) || !seen.Add(type)) continue;
            if (type.Namespace?.StartsWith("PgmStudio") is true) yield return type;

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                var held = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
                if (held.IsGenericType && typeof(IEnumerable).IsAssignableFrom(held))
                    foreach (var argument in held.GetGenericArguments()) queue.Enqueue(argument);
                else queue.Enqueue(held);
            }
        }
    }

    /// <summary>The rule itself: a struct on the wire whose primary constructor carries a string has to be
    /// the constructor the serializer uses.</summary>
    [Test]
    public async Task Every_record_struct_the_intent_carries_names_the_constructor_that_fills_its_strings()
    {
        var unmarked = Reachable(typeof(MapIntent))
            .Where(type => type is { IsValueType: true, IsEnum: false })
            .Where(type => type.GetConstructors()
                .Any(made => made.GetParameters().Any(parameter => parameter.ParameterType == typeof(string))))
            .Where(type => !type.GetConstructors()
                .Any(made => made.GetCustomAttribute<JsonConstructorAttribute>() is not null))
            .Select(type => type.Name)
            .ToList();

        await Assert.That(unmarked).IsEmpty();
    }

    /// <summary>And the consequence, on the one shape that showed it: a mode with no name is what nearly every
    /// corpus map writes, and it has to survive the wire as an unnamed mode rather than as a null.</summary>
    [Test]
    public async Task A_mode_with_no_name_arrives_unnamed_rather_than_null()
    {
        var mode = JsonSerializer.Deserialize<ModeIntent>(
            """{"after":"15m","material":"gold block"}""", Wire);

        await Assert.That(mode.Name).IsEqualTo("");
    }

    /// <summary>A payment with no colour is the ordinary one — 581 of the corpus's 907 icons state none — and
    /// the generator reads the field to decide whether to write it.</summary>
    [Test]
    public async Task A_payment_with_no_colour_arrives_colourless_rather_than_null()
    {
        var payment = JsonSerializer.Deserialize<ShopPaymentIntent>(
            """{"price":1,"currency":"gold nugget"}""", Wire);

        await Assert.That(payment.Color).IsEqualTo("");
    }

    /// <summary>The whole path the fault took: an intent posted as JSON, projected into a document. A mode
    /// with no name and an icon with no payment colour both reach the generators, which read their
    /// strings.</summary>
    [Test]
    public async Task An_intent_posted_without_its_optional_words_projects()
    {
        var intent = JsonSerializer.Deserialize<MapIntent>(
            """
            {"spawns":[{"team":"red","point":{"x":0,"y":64,"z":0},"yaw":0}],
             "destroyables":[{"owner":"red","name":"m","anchor":{"x":0,"y":64,"z":0}}],
             "modes":[{"after":"15m","material":"gold block"}],
             "spawners":[{"id":"mid","at":{"x":0,"y":12,"z":0},"drops":[{"material":"emerald"}]}],
             "shops":[{"id":"item-shop","categories":[{"id":"blocks","material":"hard clay",
               "items":[{"material":"wood","payments":[{"price":1,"currency":"gold nugget"}]}]}]}]}
            """, Wire)!;

        var doc = new Dictionary<string, object?>();
        IntentGenerator.Apply(doc, intent);   // must not throw

        await Assert.That(doc.ContainsKey("shops")).IsTrue();
        await Assert.That(doc.ContainsKey("spawners")).IsTrue();
        await Assert.That(doc.ContainsKey("modes")).IsTrue();
    }
}
