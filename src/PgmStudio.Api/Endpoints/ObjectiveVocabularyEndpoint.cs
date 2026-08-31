using FastEndpoints;
using PgmStudio.Contracts;
using PgmStudio.Domain;
using PgmStudio.Vocabulary;

namespace PgmStudio.Api.Endpoints;

/// <summary>GET /api/objectives/vocabulary — the destroyable designs (<see cref="DestroyableStyles"/>), the
/// wool dyes (<see cref="WoolColors"/>) and what each optional field is worth unauthored
/// (<see cref="ObjectiveDefaults"/>), served rather than copied
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
            ObjectiveDefaults.CoreLava, ObjectiveDefaults.CoreLavaHeight,
            [.. Enumerable.Range(ObjectiveDefaults.MinCoreLava,
                ObjectiveDefaults.MaxCoreLava - ObjectiveDefaults.MinCoreLava + 1)],
            [.. Enumerable.Range(ObjectiveDefaults.MinCoreLavaHeight,
                ObjectiveDefaults.MaxCoreLavaHeight - ObjectiveDefaults.MinCoreLavaHeight + 1)],
            ObjectiveDefaults.CoreFloat, ObjectiveDefaults.CoreLeak, OpenTop: false),
        new WoolVocabularyDto(
            [.. WoolColors.All.Select(c => new WoolColorDto(c, WoolColors.Label(c), WoolColors.Swatch[c]))],
            Auto: "auto")), ct);
}
