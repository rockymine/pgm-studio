using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Domain;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/objectives/vocabulary — the destroyable designs (<see cref="DestroyableStyles"/>) and
/// what each optional field is worth unauthored (<see cref="ObjectiveDefaults"/>), served rather than copied
/// onto the client because the client cannot reach <c>PgmStudio.Domain</c>. Shape and reasoning:
/// <see cref="ObjectiveVocabularyDto"/>.</summary>
public sealed class ObjectiveVocabularyEndpoint : EndpointWithoutRequest<ObjectiveVocabularyDto>
{
    public override void Configure() { Get("/objectives/vocabulary"); AllowAnonymous(); }

    public override Task HandleAsync(CancellationToken ct) => Send.OkAsync(new ObjectiveVocabularyDto(
        new DestroyableVocabularyDto(
            DestroyableStyles.All, DestroyableMaterials.All,
            DestroyableStyles.Slug(ObjectiveDefaults.Style), ObjectiveDefaults.Materials,
            ObjectiveDefaults.DestroyableFloat),
        new CoreVocabularyDto(
            ObjectiveDefaults.CoreSize, ObjectiveDefaults.CoreHeight, ObjectiveDefaults.CoreShell,
            ObjectiveDefaults.CoreFloat, ObjectiveDefaults.CoreLeak, OpenTop: false)), ct);
}
