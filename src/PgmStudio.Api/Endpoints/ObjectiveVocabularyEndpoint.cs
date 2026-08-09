using FastEndpoints;
using PgmStudio.Domain;

namespace PgmStudio.Api.Endpoints;

using Dict = Dictionary<string, object?>;

/// <summary>
/// GET /api/objectives/vocabulary — what an objective marker may be: the destroyable designs
/// (<see cref="DestroyableStyles"/>) and the values each optional field takes when it is left unauthored
/// (<see cref="ObjectiveDefaults"/>).
///
/// <para>Served rather than duplicated on the client. The plan tool's inspector has to show the
/// <em>effective</em> value of every knob — a marker stores only what the author changed, so an unset field
/// must render as the number the compiler and the stamper will actually use — and the Blazor client cannot
/// reach <c>PgmStudio.Domain</c>. A copy of these six numbers on the far side of the wire is precisely the
/// drift <see cref="ObjectiveDefaults"/> exists to prevent: a client offering float 4 while the stamper
/// builds at 6 shows the author a structure at a height nothing asked for.</para>
///
/// <para>Map-independent, because the plan editor opens without one (<c>/plan-editor</c>).</para>
/// </summary>
public sealed class ObjectiveVocabularyEndpoint : EndpointWithoutRequest
{
    public override void Configure() { Get("/objectives/vocabulary"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(new Dict
    {
        ["destroyable"] = new Dict
        {
            ["styles"] = DestroyableStyles.All,
            ["materialChoices"] = DestroyableMaterials.All,
            ["style"] = DestroyableStyles.Slug(ObjectiveDefaults.Style),
            ["materials"] = ObjectiveDefaults.Materials,
            ["float"] = ObjectiveDefaults.DestroyableFloat,
        },
        ["core"] = new Dict
        {
            ["size"] = ObjectiveDefaults.CoreSize,
            ["height"] = ObjectiveDefaults.CoreHeight,
            ["shell"] = ObjectiveDefaults.CoreShell,
            ["float"] = ObjectiveDefaults.CoreFloat,
            ["leak"] = ObjectiveDefaults.CoreLeak,
            ["openTop"] = false,
        },
    }, ct);
}
