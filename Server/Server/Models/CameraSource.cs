using System.Globalization;
using System.Text;

namespace Server.Models;

public sealed class CameraSource
{
    public int? Id { get; set; }

    public string? Key { get; set; }

    public required string Name { get; set; }

    public string Address { get; set; } = string.Empty;

    public string? StreamAddress { get; set; }

    public string? Host { get; set; }

    public string HighQualityPath { get; set; } = "/Streaming/Channels/101";

    public string LowQualityPath { get; set; } = "/Streaming/Channels/102";

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

    public string ResolveHost()
    {
        if (!string.IsNullOrWhiteSpace(Host))
        {
            return Host.Trim();
        }

        return TryExtractHost(StreamAddress) ?? TryExtractHost(Address) ?? string.Empty;
    }

    private static string? TryExtractHost(string? address)
    {
        if (string.IsNullOrWhiteSpace(address) || !Uri.TryCreate(address, UriKind.Absolute, out var uri))
        {
            return null;
        }

        return uri.Host;
    }
}
