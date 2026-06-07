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

        var builder = new NpgsqlDataSourceBuilder(connectionString);
        builder.EnableDynamicJson();
        return builder.Build();
    }
}
