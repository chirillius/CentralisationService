using Npgsql;

namespace CentralServer.Services;

public static class PostgreSqlDataSourceFactory
{
    public static NpgsqlDataSource Create(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Строка подключения PostgreSQL не настроена.");
        }

        connectionString = AddPasswordIfNeeded(connectionString, configuration);
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        return builder.Build();
    }

    private static string AddPasswordIfNeeded(string connectionString, IConfiguration configuration)
    {
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(builder.Password))
        {
            return builder.ConnectionString;
        }

        var password = configuration["PostgreSql:Password"];
        if (string.IsNullOrWhiteSpace(password))
        {
            password = ReadPasswordFile(configuration["PostgreSql:PasswordFile"]);
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            return builder.ConnectionString;
        }

        builder.Password = password.Trim();
        return builder.ConnectionString;
    }

    private static string? ReadPasswordFile(string? passwordFile)
    {
        if (string.IsNullOrWhiteSpace(passwordFile))
        {
            return null;
        }

        var path = Environment.ExpandEnvironmentVariables(passwordFile.Trim());
        return File.Exists(path)
            ? File.ReadAllText(path).Trim()
            : null;
    }
}
