using Npgsql;

namespace FYI.Managers.Database;

public static class DatabaseConfiguration
{
    public static string CreateConnectionString(string? url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var databaseUri)
            || (databaseUri.Scheme != "postgresql" && databaseUri.Scheme != "postgres")
            || string.IsNullOrEmpty(databaseUri.Host)
            || !databaseUri.UserInfo.Contains(':')
            || databaseUri.AbsolutePath.Length <= 1)
        {
            throw new FormatException(
                "The 'url' must be a complete PostgreSQL URL with credentials and database.");
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

        return settings.ConnectionString;
    }
}
