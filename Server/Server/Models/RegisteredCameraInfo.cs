namespace Server.Models;

public sealed class RegisteredCameraInfo
{
    public int? Id { get; init; }

    public required string Key { get; init; }

    public required string Name { get; init; }
}
