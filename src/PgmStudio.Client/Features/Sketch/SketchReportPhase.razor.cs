using System.Net.Http.Json;
using Microsoft.AspNetCore.Components;
using PgmStudio.Contracts;

namespace PgmStudio.Client.Features.Sketch;

/// <summary>The Report phase: the map's report as the studio answers it, read when the phase is entered and
/// again on request, its pictures shown one at a time so a board's renders wait their turn one after another
/// rather than all at once.</summary>
public partial class SketchReportPhase
{
    [Parameter, EditorRequired] public string Slug { get; set; } = "";

    [Parameter] public EventCallback OnBack { get; set; }

    /// <summary>The report; null while it is read.</summary>
    private MapReportDto? Report { get; set; }

    /// <summary>Why the report could not be read.</summary>
    private string? Error { get; set; }

    private MapReportPictureDto? Shown { get; set; }
    private bool loaded;
    private bool failed;

    protected override Task OnInitializedAsync() => ReadAsync();

    /// <summary>Ask for the report again, off the board as it is stored now.</summary>
    private async Task ReadAsync()
    {
        Report = null;
        Error = null;
        Show(null);
        StateHasChanged();
        try
        {
            var answer = await Http.GetAsync($"api/map/{Slug}/report");
            if (answer.IsSuccessStatusCode) Report = await answer.Content.ReadFromJsonAsync<MapReportDto>();
            else Error = (await answer.Content.ReadFromJsonAsync<RefusalDto>())?.Message is { Length: > 0 } why
                ? why : "Couldn't load the report.";
        }
        catch { Error = "Couldn't load the report. Check your connection and try again."; }
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
