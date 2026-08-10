using LinqToDB;
using LinqToDB.Async;
using PgmStudio.Data.Schema;

namespace PgmStudio.Data.Plan;

/// <summary>
/// Persistence for browse verdicts (M0015): the absolute up/down votes of the generator feedback loop, one
/// row per plan. The row is the author's <b>current</b> judgment — an upsert refines it in place (direction,
/// tags, note) while keeping the evaluator snapshot from the first vote, because the snapshot records what
/// the evaluator said about the board and the board has not changed. Duel preference pairs are a separate
/// dataset by design (G120) and never fold into these rows.
/// </summary>
public sealed class VerdictStore(PgmDb db)
{
    public Task<PlanVerdictRow?> GetByPlanAsync(long planId, CancellationToken ct = default)
        => db.PlanVerdicts.FirstOrDefaultAsync(v => v.PlanId == planId, ct);

    /// <summary>Every verdict, newest-refined first, each with its plan row (for the descriptor, structure
    /// key and content hash the export and the client's vote map need).</summary>
    public async Task<List<(PlanVerdictRow Verdict, PlanRow Plan)>> ListAsync(CancellationToken ct = default)
    {
        var joined = await db.PlanVerdicts
            .Join(db.Plans, v => v.PlanId, p => p.Id, (v, p) => new { Verdict = v, Plan = p })
            .OrderByDescending(x => x.Verdict.UpdatedAt).ThenByDescending(x => x.Verdict.Id)
            .ToListAsync(ct);
        return joined.Select(x => (x.Verdict, x.Plan)).ToList();
    }

    /// <summary>Record or refine the judgment of one plan. The first vote stores the whole row including the
    /// evaluator snapshot; a later call updates direction, tags and note in place and leaves the snapshot
    /// alone (same board, same reading). Returns the stored row.</summary>
    public async Task<PlanVerdictRow> UpsertAsync(
        long planId, string verdict, string tagsJson, string? note,
        double score, string termsJson, string evaluatorVersion, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        if (await GetByPlanAsync(planId, ct) is { } existing)
        {
            await db.PlanVerdicts.Where(v => v.Id == existing.Id)
                .Set(v => v.Verdict, verdict).Set(v => v.TagsJson, tagsJson).Set(v => v.Note, note)
                .Set(v => v.UpdatedAt, now)
                .UpdateAsync(ct);
            return (await GetByPlanAsync(planId, ct))!;
        }

        var row = new PlanVerdictRow
        {
            PlanId = planId, Verdict = verdict, TagsJson = tagsJson, Note = note,
            Score = score, TermsJson = termsJson, EvaluatorVersion = evaluatorVersion,
            CreatedAt = now, UpdatedAt = now,
        };
        row.Id = await db.InsertWithInt64IdentityAsync(row);
        return row;
    }

    /// <summary>Retract the judgment of one plan. Returns how many rows went (0 or 1).</summary>
    public Task<int> DeleteAsync(long planId, CancellationToken ct = default)
        => db.PlanVerdicts.Where(v => v.PlanId == planId).DeleteAsync(ct);
}
