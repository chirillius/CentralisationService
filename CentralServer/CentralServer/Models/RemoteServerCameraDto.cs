namespace CentralServer.Models;

public sealed class RemoteServerCameraDto
{
    public int? Id { get; init; }

    public string Key { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
