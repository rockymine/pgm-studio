using PgmStudio.Pgm.Authoring;
using PgmStudio.Vocabulary;

namespace PgmStudio.Pgm.Sketch;

/// <summary>
/// What a drawn board and the intent it is played by break, asked of the layout with its rooms drawn in
/// (<see cref="RoomShapes.Project"/>): the intent's own pieces (<see cref="IntentCheck"/>), the board's structure
/// and the walks a match needs (<see cref="SketchBoardCheck"/>), and where the pieces meet the ground
/// (<see cref="SketchSeamCheck"/>). <c>GET /map/{slug}/findings</c> answers it and the export refuses on its
/// refusals, so what the sketch shows and what stops the download are one reading.
/// </summary>
public static class SketchPlayCheck
{
    public static Findings Check(string layoutJson, MapIntent intent)
    {
        var board = SketchLayout.Parse(layoutJson)?.Setup?.Board;
        // A wool stating no door is asked with the one its build gives it.
        intent = WoolDoors.Settled(intent, layoutJson);
        return new Findings([
            .. IntentCheck.Check(intent, board),
            .. SketchBoardCheck.Check(layoutJson, intent, board),
            .. SketchSeamCheck.Check(layoutJson, intent, board),
        ]);
    }
}
