using FYI.DataModels.Entities;
using GYI.src.enumerations;
using Npgsql;
using NpgsqlTypes;

namespace FYI.Managers.Database;

public sealed class PostgresDBService(NpgsqlDataSource dataSource) : IDBService
{
    private const string Accepted = nameof(PublicationStatusEnum.Accepted);
    private static readonly string Pending = ToDatabaseStatus(ItineraryStatus.Pending);
    private static readonly string Confirmed = ToDatabaseStatus(ItineraryStatus.Confirmed);
    private const string StopColumns = """
        s.id, s.name, s.description, s.coordinatey, s.coordinatex, s.municipality, s.province,
        s.source_url, s.status, s.is_fixed, s.cap, s.price, s.proposed_by, s.created_at,
        s.updated_at, s.approved_by, s.approved_at
        """;

    public Task<IReadOnlyList<Stop>> GetStopsAsync(CancellationToken ct) =>
        ReadStopsAsync(fixedOnly: false, ids: null, ct);

    public Task<IReadOnlyList<Stop>> GetFixedStopsAsync(Guid[]? ids, CancellationToken ct) =>
        ReadStopsAsync(fixedOnly: true, ids, ct);

    private async Task<IReadOnlyList<Stop>> ReadStopsAsync(bool fixedOnly, Guid[]? ids, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand($"""
            SELECT {StopColumns} FROM fyi.stop s
            WHERE (NOT @fixedOnly OR (s.is_fixed AND s.status = @accepted))
              AND (@ids IS NULL OR s.id = ANY(@ids))
            ORDER BY s.province, s.municipality, s.name, s.id
            """);
        command.Parameters.AddWithValue("accepted", Accepted);
        command.Parameters.AddWithValue("fixedOnly", fixedOnly);
        command.Parameters.AddWithValue("ids", NpgsqlDbType.Array | NpgsqlDbType.Uuid, (object?)ids ?? DBNull.Value);

        await using var reader = await command.ExecuteReaderAsync(ct);
        var stops = new List<Stop>();
        while (await reader.ReadAsync(ct))
        {
            stops.Add(ReadStop(reader, 0));
        }
        return stops;
    }

    public async Task<IReadOnlyList<(Guid Id, string Title, ItineraryStatus Status)>> GetItinerariesAsync(
        Guid editor, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, title, status FROM fyi.itinerary
            WHERE created_by = @editor AND status IN (@pending, @confirmed)
            ORDER BY created_at DESC NULLS LAST, id
            """);
        AddEditorScope(command, editor);
        command.Parameters.AddWithValue("confirmed", Confirmed);
        await using var reader = await command.ExecuteReaderAsync(ct);
        var itineraries = new List<(Guid, string, ItineraryStatus)>();
        while (await reader.ReadAsync(ct))
        {
            itineraries.Add((reader.GetGuid(0), reader.GetString(1), ReadItineraryStatus(reader, 2)));
        }
        return itineraries;
    }

    public async Task<StoredItinerary?> GetItineraryAsync(Guid editor, Guid id, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand($"""
            SELECT i.title, i.theme, i.description, i.geographic_area, i.is_customizable, i.created_at,
                   i.updated_by, i.updated_at, i.status, {StopColumns}
            FROM fyi.itinerary i
            LEFT JOIN fyi.itinerary_stops links ON links.itinerary_id = i.id
            LEFT JOIN fyi.stop s ON s.id = links.stop_id
            WHERE i.id = @id AND i.created_by = @editor AND i.status IN (@pending, @confirmed)
            ORDER BY links."order", links.id
            """);
        AddEditorScope(command, editor);
        command.Parameters.AddWithValue("confirmed", Confirmed);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
        {
            return null;
        }

        var metadata = new Itinerary
        {
            Id = id,
            Title = reader.GetString(0),
            Theme = ReadText(reader, 1),
            Description = reader.GetString(2),
            GeographicArea = ReadText(reader, 3),
            IsCustomizable = reader.GetBoolean(4),
            CreatedAt = ReadNullable<DateTime>(reader, 5),
            UpdatedBy = ReadNullable<Guid>(reader, 6),
            UpdatedAt = ReadNullable<DateTime>(reader, 7),
            Status = ReadItineraryStatus(reader, 8),
            CreatedBy = editor
        };
        var stops = new List<Stop>();
        do
        {
            if (!reader.IsDBNull(9))
            {
                stops.Add(ReadStop(reader, 9));
            }
        } while (await reader.ReadAsync(ct));

        return new StoredItinerary(metadata, stops);
    }

    public async Task<ItineraryWriteResult> SaveItineraryAsync(
        Itinerary itinerary, Guid[] stopIds, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var editor = itinerary.CreatedBy!.Value;
        await using var command = new NpgsqlCommand("""
            INSERT INTO fyi.itinerary
                (id, title, theme, description, geographic_area, status, is_customizable, created_by, created_at)
            VALUES (@id, @title, @theme, @description, @area, @status, @customizable, @editor,
                    CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
            ON CONFLICT (title) DO NOTHING
            """, connection, transaction);
        command.Parameters.AddWithValue("id", itinerary.Id);
        command.Parameters.AddWithValue("title", itinerary.Title);
        command.Parameters.AddWithValue("theme", itinerary.Theme!);
        command.Parameters.AddWithValue("description", itinerary.Description);
        command.Parameters.AddWithValue("area", itinerary.GeographicArea!);
        command.Parameters.AddWithValue("status", ToDatabaseStatus(itinerary.Status));
        command.Parameters.AddWithValue("customizable", itinerary.IsCustomizable);
        command.Parameters.AddWithValue("editor", editor);
        if (await command.ExecuteNonQueryAsync(ct) == 0)
        {
            return ItineraryWriteResult.TitleConflict;
        }

        if (!await LockFixedStopsAsync(connection, transaction, stopIds, ct))
        {
            return ItineraryWriteResult.UnavailableStops;
        }
        await InsertStopsAsync(connection, transaction, itinerary.Id, editor, stopIds, ct);
        await transaction.CommitAsync(ct);
        return ItineraryWriteResult.Saved;
    }

    public async Task<ItineraryWriteResult> UpdateItineraryStopsAsync(
        Guid editor, Guid id, Guid[] stopIds, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var update = new NpgsqlCommand("""
            UPDATE fyi.itinerary
            SET updated_by = @editor, updated_at = CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            WHERE id = @id AND created_by = @editor AND status = @pending
            """, connection, transaction);
        AddEditorScope(update, editor);
        update.Parameters.AddWithValue("id", id);
        if (await update.ExecuteNonQueryAsync(ct) == 0)
        {
            return ItineraryWriteResult.NotFound;
        }

        if (!await LockFixedStopsAsync(connection, transaction, stopIds, ct))
        {
            return ItineraryWriteResult.UnavailableStops;
        }
        await using var delete = new NpgsqlCommand("""
            DELETE FROM fyi.itinerary_stops WHERE itinerary_id = @id
            """, connection, transaction);
        delete.Parameters.AddWithValue("id", id);
        await delete.ExecuteNonQueryAsync(ct);
        await InsertStopsAsync(connection, transaction, id, editor, stopIds, ct);
        await transaction.CommitAsync(ct);
        return ItineraryWriteResult.Saved;
    }

    public async Task<bool> ConfirmItineraryAsync(Guid editor, Guid id, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            UPDATE fyi.itinerary
            SET status = @confirmed, updated_by = @editor, updated_at = CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            WHERE id = @id AND created_by = @editor AND status = @pending
            """);
        AddEditorScope(command, editor);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("confirmed", Confirmed);
        if (await command.ExecuteNonQueryAsync(ct) == 1)
        {
            return true;
        }

        // A retry after a successful confirmation leaves the original update metadata intact.
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1 FROM fyi.itinerary
                WHERE id = @id AND created_by = @editor AND status = @confirmed
            )
            """;
        return await command.ExecuteScalarAsync(ct) is true;
    }

    private static async Task<bool> LockFixedStopsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid[] stopIds, CancellationToken ct)
    {
        // Keep the selected catalog rows eligible until the itinerary transaction commits.
        await using var command = new NpgsqlCommand("""
            SELECT count(*) FROM (
                SELECT id FROM fyi.stop
                WHERE id = ANY(@stops) AND is_fixed AND status = @accepted
                ORDER BY id FOR SHARE
            ) AS selected
            """, connection, transaction);
        command.Parameters.AddWithValue("stops", stopIds);
        command.Parameters.AddWithValue("accepted", Accepted);
        return (long)(await command.ExecuteScalarAsync(ct))! == stopIds.Length;
    }

    private static async Task InsertStopsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, Guid editor,
        Guid[] stopIds, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO fyi.itinerary_stops (itinerary_id, stop_id, "order", created_by, created_at)
            SELECT @id, stop_id, position, @editor, CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            FROM unnest(@stops) WITH ORDINALITY AS links(stop_id, position)
            """, connection, transaction);
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("editor", editor);
        command.Parameters.AddWithValue("stops", stopIds);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static Stop ReadStop(NpgsqlDataReader reader, int offset) => new()
    {
        Id = reader.GetGuid(offset),
        Name = reader.GetString(offset + 1),
        Description = reader.GetString(offset + 2),
        CoordinateY = reader.GetDecimal(offset + 3),
        CoordinateX = reader.GetDecimal(offset + 4),
        Municipality = ReadText(reader, offset + 5),
        Province = ReadText(reader, offset + 6),
        SourceUrl = ReadText(reader, offset + 7),
        Status = reader.GetString(offset + 8),
        IsFixed = reader.GetBoolean(offset + 9),
        Cap = reader.GetDecimal(offset + 10),
        Price = reader.GetDecimal(offset + 11),
        ProposedBy = ReadNullable<Guid>(reader, offset + 12),
        CreatedAt = ReadNullable<DateTime>(reader, offset + 13),
        UpdatedAt = ReadNullable<DateTime>(reader, offset + 14),
        ApprovedBy = ReadNullable<Guid>(reader, offset + 15),
        ApprovedAt = ReadNullable<DateTime>(reader, offset + 16)
    };

    private static string? ReadText(NpgsqlDataReader reader, int column) =>
        reader.IsDBNull(column) ? null : reader.GetString(column);

    private static T? ReadNullable<T>(NpgsqlDataReader reader, int column) where T : struct =>
        reader.IsDBNull(column) ? null : reader.GetFieldValue<T>(column);

    private static void AddEditorScope(NpgsqlCommand command, Guid editor)
    {
        command.Parameters.AddWithValue("editor", editor);
        command.Parameters.AddWithValue("pending", Pending);
    }

    private static ItineraryStatus ReadItineraryStatus(NpgsqlDataReader reader, int column) =>
        Enum.Parse<ItineraryStatus>(reader.GetString(column), ignoreCase: true);

    private static string ToDatabaseStatus(ItineraryStatus status) => status.ToString().ToLowerInvariant();
}
