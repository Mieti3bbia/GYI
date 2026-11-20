using Npgsql;

namespace FYI.Managers.Database;

public static class DatabaseConfiguration
{
    public static string CreateConnectionString(string? connectionString, string? url)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return CreateConnectionString(url);
        }

        try
        {
            return new NpgsqlConnectionStringBuilder(connectionString).ConnectionString;
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException)
        {
            throw new FormatException("ConnectionStrings:Postgres is not a valid PostgreSQL connection string.");
        }
    }

    public static string CreateConnectionString(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var databaseUri)
            || (databaseUri.Scheme != "postgresql" && databaseUri.Scheme != "postgres")
            || string.IsNullOrEmpty(databaseUri.Host)
            || !databaseUri.UserInfo.Contains(':')
            || databaseUri.AbsolutePath.Length <= 1)
        {
            throw new FormatException(
                "Configure ConnectionStrings:Postgres or provide a complete PostgreSQL URL in 'url'.");
        }

        var credentials = databaseUri.UserInfo.Split(':', 2);
        var settings = new NpgsqlConnectionStringBuilder
        {
            Host = databaseUri.Host,
            Port = databaseUri.Port == -1 ? 5432 : databaseUri.Port,
            Database = Uri.UnescapeDataString(databaseUri.AbsolutePath[1..]),
            Username = Uri.UnescapeDataString(credentials[0]),
            Password = Uri.UnescapeDataString(credentials[1]),
            SslMode = SslMode.Require,
            Timeout = 15,
            CommandTimeout = 15
        };

        // Supabase already pools transactions on this endpoint.
        if (settings.Port == 6543 && databaseUri.Host.EndsWith(".pooler.supabase.com", StringComparison.OrdinalIgnoreCase))
        {
            settings.Pooling = false;
        }

        return settings.ConnectionString;
    }
}
