using CentralServer.Models;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CentralServer.Services;

public sealed class PostgreSqlSchemaInitializer : IHostedService
{
    private static readonly string[] MigrationFiles =
    [
        "001_initial_platform_schema.sql",
        "002_seed_detection_catalog.sql",
        "003_runtime_storage_adjustments.sql",
    ];

    private readonly NpgsqlDataSource _dataSource;
    private readonly IWebHostEnvironment _environment;
    private readonly PostgreSqlOptions _options;
    private readonly ILogger<PostgreSqlSchemaInitializer> _logger;

    public PostgreSqlSchemaInitializer(
        NpgsqlDataSource dataSource,
        IWebHostEnvironment environment,
        IOptions<PostgreSqlOptions> options,
        ILogger<PostgreSqlSchemaInitializer> logger)
    {
        _dataSource = dataSource;
        _environment = environment;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled || !_options.ApplySchemaOnStartup)
        {
            return;
        }

        var migrationsDirectory = Path.Combine(_environment.ContentRootPath, "Database", "PostgreSql");
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        foreach (var migrationFile in MigrationFiles)
        {
            var path = Path.Combine(migrationsDirectory, migrationFile);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"SQL-миграция не найдена: {path}", path);
            }

            var sql = await File.ReadAllTextAsync(path, cancellationToken);
            await using var command = new NpgsqlCommand(sql, connection);
            command.CommandTimeout = 120;
            await command.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("Applied PostgreSQL migration {MigrationFile}", migrationFile);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
