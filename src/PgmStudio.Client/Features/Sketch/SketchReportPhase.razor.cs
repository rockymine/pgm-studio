using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The Report phase: the map's report as the studio answers it, its pictures shown one at a time so a
/// board's renders wait their turn one after another rather than all at once.</summary>
public partial class SketchReportPhase
{
    [Parameter, EditorRequired] public string Slug { get; set; } = "";

    /// <summary>The report; null while it is read.</summary>
    [Parameter] public MapReportDto? Report { get; set; }

    /// <summary>Why the report could not be read.</summary>
    [Parameter] public string? Error { get; set; }

    [Parameter] public EventCallback OnBack { get; set; }

    /// <summary>Asks for the report again, off the board as it is stored now.</summary>
    [Parameter] public EventCallback OnRead { get; set; }

    private MapReportPictureDto? Shown { get; set; }
    private MapReportDto? read;
    private bool loaded;
    private bool failed;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(Report, read)) return;
        read = Report;
        Show(Report?.Pictures.FirstOrDefault(picture => picture.Missing is null));
    }

    private void Show(MapReportPictureDto? picture)
    {
        Shown = picture;
        loaded = false;
        failed = false;
    }

    /// <summary>The map's own path, which a picture's route — the report's, relative to the map — hangs off.</summary>
    private string MapPath => $"api/map/{Slug}/";

    /// <summary>The picture's route, carrying the change it was named for, so a board that has changed since is
    /// drawn again rather than shown as the browser kept it.</summary>
    private string Source(MapReportPictureDto picture) =>
        MapPath + picture.Route + (picture.Route.Contains('?') ? "&" : "?") + $"change={Report?.Change}";
}
