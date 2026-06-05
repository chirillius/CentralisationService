namespace CentralServer.Models;

public sealed class AccessOptions
{
    public string ConfigurationDirectory { get; set; } = "Configuration/access";
    public int SessionLifetimeHours { get; set; } = 24;
    public string PlatformAdminKey { get; set; } = string.Empty;
    public string PlatformAdminLogin { get; set; } = "admin";
    public string PlatformAdminPassword { get; set; } = "1234";
    public int PlatformSessionLifetimeHours { get; set; } = 8;
}
