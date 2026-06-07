namespace CentralServer.Models;

public sealed class PostgreSqlOptions
{
    public bool Enabled { get; set; } = true;

    public bool ApplySchemaOnStartup { get; set; } = true;

    public bool SeedJsonConfigurationOnEmptyDatabase { get; set; } = true;

    public string Password { get; set; } = string.Empty;

    public string PasswordFile { get; set; } = string.Empty;
}
