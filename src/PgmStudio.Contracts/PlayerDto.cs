namespace PgmStudio.Contracts;

/// <summary>A Minecraft account, resolved either way round from a name or a uuid.</summary>
/// <param name="Uuid">The account's canonical uuid, which is what an author entry is stored under.</param>
/// <param name="Name">The username it currently answers to. It can change; the uuid cannot.</param>
public sealed record PlayerDto(string Uuid, string Name);
