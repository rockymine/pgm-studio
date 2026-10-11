namespace PgmStudio.Contracts;

/// <summary>Mark a map as made by an agent, or not.</summary>
/// <param name="AgentMade">Whether an agent made the map. An agent-made map is seen only by the admins and its
/// owner; to anyone else every route naming it answers as if it were not stored.</param>
public sealed record MapAgentMadeRequest(bool AgentMade);
