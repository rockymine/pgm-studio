namespace PgmStudio.Client.Models;

/// <summary>A gamemode by the name a page shows it under, in the glossary's words; an id with no name here shows
/// its own word.</summary>
public static class GamemodeNames
{
    public static string NameOf(string gamemode) => gamemode switch
    {
        "ctw" => "Capture the Wool",
        "dtm" => "Destroy monuments",
        "dtc" => "Destroy cores",
        "cp" => "Capture points",
        "koth" => "King of the Hill",
        "ctf" => "Capture the Flag",
        "kotf" => "King of the Flag",
        "tdm" => "Team deathmatch",
        "scorebox" => "Score boxes",
        "blitz" => "Blitz",
        "rage" => "Rage",
        _ => gamemode,
    };
}
