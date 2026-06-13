namespace PgmStudio.Pgm.Editing;

/// <summary>An editor validation/conflict error; <see cref="Status"/> maps to the HTTP response code.</summary>
public sealed class EditException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;

    public static EditException BadRequest(string m) => new(400, m);
    public static EditException NotFound(string m) => new(404, m);
    public static EditException Conflict(string m) => new(409, m);
}
