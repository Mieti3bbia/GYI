using FYI.DataModels.Entities;
using GYI.src.enumerations;
using Npgsql;
using NpgsqlTypes;
using System.Globalization;
using DraftChanges = GYI.src.dataModels.DraftChanges;
using DraftStopChange = GYI.src.dataModels.DraftStopChange;
using DraftStopDetails = GYI.src.dataModels.DraftStopDetails;

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
    private const string ValidFixedStop = """
        s.is_fixed AND s.status = @accepted AND s.coordinatey BETWEEN -90 AND 90
        AND s.coordinatex BETWEEN -180 AND 180 AND btrim(s.name) <> '' AND btrim(s.description) <> ''
        """;

    public async Task<Stop?> CreateCatalogStopAsync(Guid editor, DraftStopDetails details, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        try
        {
            return await InsertStopAsync(connection, null, null, editor, details, ct);
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return null;
        }
    }

    public Task<IReadOnlyList<Stop>> GetStopsAsync(CancellationToken ct) =>
        ReadStopsAsync(fixedOnly: false, ids: null, ct);

    public Task<IReadOnlyList<Stop>> GetFixedStopsAsync(Guid[]? ids, CancellationToken ct) =>
        ReadStopsAsync(fixedOnly: true, ids, ct);

    private async Task<IReadOnlyList<Stop>> ReadStopsAsync(bool fixedOnly, Guid[]? ids, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand($"""
            SELECT {StopColumns} FROM fyi.stop s
            WHERE s.draft_itinerary_id IS NULL
              AND (NOT @fixedOnly OR ({ValidFixedStop}))
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
        Guid? editor, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand("""
            SELECT id, title, status FROM fyi.itinerary
            WHERE (status = @confirmed AND @editor IS NULL)
               OR (created_by = @editor AND status IN (@pending, @confirmed))
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

    public async Task<StoredItinerary?> GetItineraryAsync(Guid? editor, Guid id, CancellationToken ct)
    {
        await using var command = dataSource.CreateCommand($"""
            SELECT i.title, i.theme, i.description, i.geographic_area, i.is_customizable, i.created_at,
                   i.updated_by, i.updated_at, i.status, i.created_by,
                   i.accepts_economic_stops, i.average_trip_price, {StopColumns}
            FROM fyi.itinerary i
            LEFT JOIN fyi.itinerary_stops links ON links.itinerary_id = i.id
            LEFT JOIN fyi.stop s ON s.id = links.stop_id
            WHERE i.id = @id AND ((i.status = @confirmed AND @editor IS NULL)
               OR (i.created_by = @editor AND i.status IN (@pending, @confirmed)))
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
            CreatedBy = ReadNullable<Guid>(reader, 9),
            AcceptsEconomicStops = reader.GetBoolean(10),
            AverageTripPrice = ReadNullable<decimal>(reader, 11)
        };
        var stops = new List<Stop>();
        do
        {
            if (!reader.IsDBNull(12))
            {
                stops.Add(ReadStop(reader, 12));
            }
        } while (await reader.ReadAsync(ct));

        return new StoredItinerary(metadata, stops);
    }

    public async Task<ItineraryWriteResult> SaveItineraryAsync(
        Itinerary itinerary, DraftStopChange[] stops, CancellationToken ct)
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

        var selection = await WriteStopsAsync(connection, transaction, itinerary.Id, editor, stops, ct);
        if (selection != ItineraryWriteResult.Saved) return selection;
        await transaction.CommitAsync(ct);
        return ItineraryWriteResult.Saved;
    }

    public Task<ItineraryWriteResult> UpdateItineraryStopsAsync(
        Guid editor, Guid id, Guid[] stopIds, CancellationToken ct) =>
        UpdateDraftAsync(editor, id, stopIds.Select(stopId => new DraftStopChange(stopId, null)).ToArray(), null, ct);

    public Task<ItineraryWriteResult> UpdateItineraryAsync(
        Guid editor, Guid id, DraftChanges changes, CancellationToken ct) =>
        UpdateDraftAsync(editor, id, changes.Stops, changes, ct);

    private static async Task<Stop> InsertStopAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid? id, Guid editor,
        DraftStopDetails details, CancellationToken ct)
    {
        var stopId = Guid.NewGuid();
        await using var command = new NpgsqlCommand($"""
            INSERT INTO fyi.stop AS s
                (id, draft_itinerary_id, name, description, coordinatex, coordinatey,
                 municipality, province, source_url,
                 status, is_fixed, cap, price, proposed_by, approved_by, created_at, approved_at)
            VALUES (@stopId, @id, @name, @description, @longitude, @latitude,
                    @municipality, @province, @sourceUrl,
                    @accepted, true, @cap, @price, @editor, @editor,
                    CURRENT_TIMESTAMP AT TIME ZONE 'UTC', CURRENT_TIMESTAMP AT TIME ZONE 'UTC')
            RETURNING {StopColumns}
            """, connection, transaction);
        command.Parameters.AddWithValue("stopId", stopId);
        command.Parameters.AddWithValue("id", NpgsqlDbType.Uuid, (object?)id ?? DBNull.Value);
        command.Parameters.AddWithValue("name", details.Name!.Trim());
        command.Parameters.AddWithValue("description", details.Description!.Trim());
        command.Parameters.AddWithValue("longitude", details.Longitude!.Value);
        command.Parameters.AddWithValue("latitude", details.Latitude!.Value);
        command.Parameters.AddWithValue("cap", decimal.Parse(details.Cap!, CultureInfo.InvariantCulture));
        command.Parameters.AddWithValue("price", details.Price!.Value);
        command.Parameters.AddWithValue("municipality", details.Municipality!.Trim());
        command.Parameters.AddWithValue("province", details.Province!.Trim());
        command.Parameters.AddWithValue("sourceUrl", NpgsqlDbType.Varchar,
            string.IsNullOrWhiteSpace(details.SourceUrl) ? DBNull.Value : details.SourceUrl.Trim());
        command.Parameters.AddWithValue("accepted", Accepted);
        command.Parameters.AddWithValue("editor", editor);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return ReadStop(reader, 0);
    }

    private async Task<ItineraryWriteResult> UpdateDraftAsync(
        Guid editor, Guid id, DraftStopChange[] stops, DraftChanges? changes, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        const string fields = "title = @title, theme = @theme, description = @description, geographic_area = @area,";
        await using var update = new NpgsqlCommand($"""
            UPDATE fyi.itinerary
            SET {(changes is null ? "" : fields)}
                updated_by = @editor, updated_at = CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            WHERE id = @id AND created_by = @editor AND status = @pending
            """, connection, transaction);
        AddEditorScope(update, editor);
        update.Parameters.AddWithValue("id", id);
        if (changes is not null)
        {
            update.Parameters.AddWithValue("title", changes.Title);
            update.Parameters.AddWithValue("theme", changes.Theme);
            update.Parameters.AddWithValue("description", changes.Description);
            update.Parameters.AddWithValue("area", changes.GeographicArea);
        }
        try
        {
            if (await update.ExecuteNonQueryAsync(ct) == 0)
            {
                return ItineraryWriteResult.NotFound;
            }
        }
        catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return ItineraryWriteResult.TitleConflict;
        }

        var selection = await WriteStopsAsync(connection, transaction, id, editor, stops, ct);
        if (selection != ItineraryWriteResult.Saved) return selection;
        await transaction.CommitAsync(ct);
        return ItineraryWriteResult.Saved;
    }

    private static async Task<ItineraryWriteResult> WriteStopsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid id, Guid editor,
        DraftStopChange[] stops, CancellationToken ct)
    {
        var existingIds = stops.Where(stop => stop.StopId.HasValue).Select(stop => stop.StopId!.Value).ToArray();
        if (!await LockFixedStopsAsync(connection, transaction, id, existingIds, ct))
        {
            return ItineraryWriteResult.UnavailableStops;
        }
        var stopIds = new Guid[stops.Length];
        for (var index = 0; index < stops.Length; index++)
        {
            var stop = stops[index];
            stopIds[index] = stop.Details is null
                ? stop.StopId!.Value
                : (await InsertStopAsync(connection, transaction, id, editor, stop.Details, ct)).Id;
        }
        if (!await HasDistinctStopsAsync(connection, transaction, stopIds, ct))
            return ItineraryWriteResult.DuplicateStops;

        await using var delete = new NpgsqlCommand("""
            DELETE FROM fyi.itinerary_stops WHERE itinerary_id = @id
            """, connection, transaction);
        delete.Parameters.AddWithValue("id", id);
        await delete.ExecuteNonQueryAsync(ct);
        await InsertStopsAsync(connection, transaction, id, editor, stopIds, ct);
        await using var discard = new NpgsqlCommand("""
            DELETE FROM fyi.stop s
            WHERE s.draft_itinerary_id = @id
              AND NOT EXISTS (SELECT 1 FROM fyi.itinerary_stops links WHERE links.stop_id = s.id)
            """, connection, transaction);
        discard.Parameters.AddWithValue("id", id);
        await discard.ExecuteNonQueryAsync(ct);
        return ItineraryWriteResult.Saved;
    }

    public async Task<ItineraryWriteResult> ConfirmItineraryAsync(Guid editor, Guid id, CancellationToken ct)
    {
        await using var connection = await dataSource.OpenConnectionAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        await using var draft = new NpgsqlCommand("""
            SELECT status, btrim(title) <> '' AND btrim(coalesce(theme, '')) <> '' AND btrim(description) <> ''
            FROM fyi.itinerary
            WHERE id = @id AND created_by = @editor AND status IN (@pending, @confirmed)
            FOR UPDATE
            """, connection, transaction);
        AddEditorScope(draft, editor);
        draft.Parameters.AddWithValue("id", id);
        draft.Parameters.AddWithValue("confirmed", Confirmed);
        await using (var reader = await draft.ExecuteReaderAsync(ct))
        {
            if (!await reader.ReadAsync(ct)) return ItineraryWriteResult.NotFound;
            if (ReadItineraryStatus(reader, 0) == ItineraryStatus.Confirmed) return ItineraryWriteResult.Saved;
            if (!reader.GetBoolean(1)) return ItineraryWriteResult.IncompleteItinerary;
        }

        await using var selection = new NpgsqlCommand("""
            SELECT array_agg(stop_id) FROM fyi.itinerary_stops WHERE itinerary_id = @id
            """, connection, transaction);
        selection.Parameters.AddWithValue("id", id);
        var ids = await selection.ExecuteScalarAsync(ct) as Guid[];
        if (ids is null || ids.Distinct().Count() < 2)
            return ItineraryWriteResult.IncompleteItinerary;
        if (!await LockFixedStopsAsync(connection, transaction, id, ids, ct))
            return ItineraryWriteResult.UnavailableStops;
        if (!await HasDistinctStopsAsync(connection, transaction, ids, ct))
            return ItineraryWriteResult.IncompleteItinerary;

        await using var confirm = new NpgsqlCommand("""
            UPDATE fyi.itinerary
            SET status = @confirmed, updated_by = @editor, updated_at = CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            WHERE id = @id
            """, connection, transaction);
        confirm.Parameters.AddWithValue("id", id);
        confirm.Parameters.AddWithValue("editor", editor);
        confirm.Parameters.AddWithValue("confirmed", Confirmed);
        await confirm.ExecuteNonQueryAsync(ct);
        await transaction.CommitAsync(ct);
        return ItineraryWriteResult.Saved;
    }

    public async Task<bool> UpdateParticipationAsync(
        Guid editor, Guid id, bool accepts, decimal? price, CancellationToken ct)
    {
        await using var update = dataSource.CreateCommand("""
            UPDATE fyi.itinerary
            SET accepts_economic_stops = @accepts, average_trip_price = @price,
                updated_by = @editor, updated_at = CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            WHERE id = @id AND created_by = @editor AND status = @confirmed
              AND (NOT @accepts OR (SELECT count(DISTINCT stop_id)
                  FROM fyi.itinerary_stops WHERE itinerary_id = @id) >= 2)
            """);
        update.Parameters.AddWithValue("id", id);
        update.Parameters.AddWithValue("editor", editor);
        update.Parameters.AddWithValue("confirmed", Confirmed);
        update.Parameters.AddWithValue("accepts", accepts);
        update.Parameters.AddWithValue("price", NpgsqlDbType.Numeric, (object?)price ?? DBNull.Value);
        return await update.ExecuteNonQueryAsync(ct) == 1;
    }

    private static async Task<bool> LockFixedStopsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid itineraryId, Guid[] stopIds, CancellationToken ct)
    {
        // Keep the selected catalog rows eligible until the itinerary transaction commits.
        await using var command = new NpgsqlCommand($"""
            SELECT count(*) FROM (
                SELECT s.id FROM fyi.stop s
                WHERE s.id = ANY(@stops) AND ({ValidFixedStop})
                  AND (draft_itinerary_id IS NULL OR draft_itinerary_id = @id)
                ORDER BY id FOR SHARE
            ) AS selected
            """, connection, transaction);
        command.Parameters.AddWithValue("stops", stopIds);
        command.Parameters.AddWithValue("id", itineraryId);
        command.Parameters.AddWithValue("accepted", Accepted);
        return (long)(await command.ExecuteScalarAsync(ct))! == stopIds.Length;
    }

    private static async Task<bool> HasDistinctStopsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid[] stopIds, CancellationToken ct)
    {
        await using var command = new NpgsqlCommand("""
            SELECT count(DISTINCT (lower(btrim(name)), coordinatex, coordinatey))
            FROM fyi.stop WHERE id = ANY(@stops)
            """, connection, transaction);
        command.Parameters.AddWithValue("stops", stopIds);
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

    private static void AddEditorScope(NpgsqlCommand command, Guid? editor)
    {
        command.Parameters.AddWithValue("editor", NpgsqlDbType.Uuid, (object?)editor ?? DBNull.Value);
        command.Parameters.AddWithValue("pending", Pending);
    }

    private static ItineraryStatus ReadItineraryStatus(NpgsqlDataReader reader, int column) =>
        Enum.Parse<ItineraryStatus>(reader.GetString(column), ignoreCase: true);

    private static string ToDatabaseStatus(ItineraryStatus status) => status.ToString().ToLowerInvariant();
}
