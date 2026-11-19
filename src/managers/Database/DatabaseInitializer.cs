using Npgsql;

namespace FYI.Managers.Database;

public sealed class DatabaseInitializer(
    NpgsqlDataSource dataSource,
    ILogger<DatabaseInitializer> logger)
{
    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var scriptPath = Path.Combine(AppContext.BaseDirectory, "Database", "init.sql");
            var sql = await File.ReadAllTextAsync(scriptPath, cancellationToken);

            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            await using var initialization = new NpgsqlCommand(sql, connection, transaction);
            await initialization.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            logger.LogInformation("Schema fyi initialized.");
            return true;
        }
        catch (NpgsqlException exception)
        {
            logger.LogError(
                "PostgreSQL connection, query, or initialization failed. " +
                "Error Type: {ErrorType}. SQLSTATE: {SqlState}. Cause: {CauseType}.",
                exception.GetType().Name,
                exception.SqlState,
                exception.InnerException?.GetType().Name);
            return false;
        }
    }
}
