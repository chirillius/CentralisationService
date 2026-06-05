using Microsoft.Extensions.Options;
using CentralServer.Models;

namespace CentralServer.Services;

public sealed class StoreCatalogSyncBackgroundService : BackgroundService
{
    private readonly ServerRegistryService _registryService;
    private readonly StoreCatalogOptions _options;
    private readonly ILogger<StoreCatalogSyncBackgroundService> _logger;

    public StoreCatalogSyncBackgroundService(
        ServerRegistryService registryService,
        IOptions<StoreCatalogOptions> options,
        ILogger<StoreCatalogSyncBackgroundService> logger)
    {
        _registryService = registryService;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await _registryService.RefreshAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Store catalog refresh failed");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.RefreshIntervalSeconds)), stoppingToken);
        }
    }
}
