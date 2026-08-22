namespace PgmStudio.Contracts;

/// <summary>A Minecraft account, resolved either way round from a name or a uuid.</summary>
public sealed record PlayerDto(string Uuid, string Name);
