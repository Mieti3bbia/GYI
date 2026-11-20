using Npgsql;
using FYI.Demo;

namespace FYI.Managers.Database;

public sealed class DatabaseInitializer(
    NpgsqlDataSource dataSource,
    IWebHostEnvironment environment,
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
            // Serialize schema initialization and demo provisioning across application instances.
            await using var migrationLock = new NpgsqlCommand(
                "SELECT pg_advisory_xact_lock(1330661705, 1)", connection, transaction);
            await migrationLock.ExecuteNonQueryAsync(cancellationToken);

            await using var initialization = new NpgsqlCommand(sql, connection, transaction);
            await initialization.ExecuteNonQueryAsync(cancellationToken);

            if (environment.IsDevelopment())
            {
                if (!await DemoEditor.EnsureExistsAsync(connection, transaction, cancellationToken))
                {
                    logger.LogError("The demo editor UUID is already assigned to a different user.");
                    return false;
                }

                var seed = await File.ReadAllTextAsync(
                    Path.Combine(AppContext.BaseDirectory, "Database", "seed-marche.sql"), cancellationToken);
                await using var catalog = new NpgsqlCommand(seed, connection, transaction);
                catalog.Parameters.AddWithValue("editor", DemoEditor.Id);
                await catalog.ExecuteNonQueryAsync(cancellationToken);
            }

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
