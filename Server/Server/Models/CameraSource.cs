using System.Globalization;
using System.Text;

namespace Server.Models;

public sealed class CameraSource
{
    public int? Id { get; init; }

    public string? Key { get; init; }

    public required string Name { get; init; }

    public required string Address { get; init; }

    public string? StreamAddress { get; init; }

    public string ResolveKey()
    {
        if (!string.IsNullOrWhiteSpace(Key))
        {
            return Key.Trim();
        }

        if (Id.HasValue)
        {
            return Id.Value.ToString(CultureInfo.InvariantCulture);
        }

        var builder = new StringBuilder();
        foreach (var character in Name.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
            else if (builder.Length > 0 && builder[^1] != '-')
            {
                builder.Append('-');
            }
        }

        return builder.Length == 0 ? "camera" : builder.ToString().Trim('-');
    }

    public string ResolveCaptureAddress() =>
        string.IsNullOrWhiteSpace(StreamAddress) ? Address : StreamAddress;
}
