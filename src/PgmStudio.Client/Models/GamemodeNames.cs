namespace PgmStudio.Client.Models;

/// <summary>A gamemode by the name a page shows it under: PGM's own full name for it (<c>Gamemode.java</c>); an id
/// with no name here shows its own word.</summary>
public static class GamemodeNames
{
    public static string NameOf(string gamemode) => gamemode switch
    {
        "ctw" => "Capture the Wool",
        "dtm" => "Destroy the Monument",
        "dtc" => "Destroy the Core",
        "cp" => "Control the Point",
        "koth" => "King of the Hill",
        "ctf" => "Capture the Flag",
        "kotf" => "King of the Flag",
        "tdm" => "Deathmatch",
        "scorebox" => "Scorebox",
        "blitz" => "Blitz",
        "rage" => "Rage",
        _ => gamemode,
    };
}
