using Npgsql;

namespace FYI.Demo;

internal static class DemoEditor
{
    internal static readonly Guid Id = Guid.Parse("f0820e61-db30-478b-a0b1-8870abc7e00e");

    internal static async Task<bool> EnsureExistsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, CancellationToken cancellationToken)
    {
        const string sql = """
            WITH existing_role AS (
                SELECT id FROM fyi.roles WHERE lower(name) = 'editor' ORDER BY id LIMIT 1
            ), new_role AS (
                INSERT INTO fyi.roles (name)
                SELECT 'Editor' WHERE NOT EXISTS (SELECT 1 FROM existing_role)
                RETURNING id
            )
            INSERT INTO fyi.users (id, firstname, lastname, email, password, role_id, created_at)
            SELECT @id, 'Editor', 'Demo', 'editor-demo@gyi.invalid', '!', role.id,
                   CURRENT_TIMESTAMP AT TIME ZONE 'UTC'
            FROM (SELECT id FROM existing_role UNION ALL SELECT id FROM new_role) AS role
            ON CONFLICT (id) DO UPDATE SET role_id = EXCLUDED.role_id
            WHERE users.email = EXCLUDED.email
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", Id);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }
}
