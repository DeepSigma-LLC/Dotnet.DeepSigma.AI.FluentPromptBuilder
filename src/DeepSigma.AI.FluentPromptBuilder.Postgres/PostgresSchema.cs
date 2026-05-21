using DeepSigma.DataAccess.RelationalDatabase;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>
/// SQL fragments used by the Postgres adapter. Exposed so callers can hand the schema DDL to
/// their migration tool of choice (Flyway, dbup, EF migrations, manual SQL, etc.) instead of
/// relying on <see cref="PostgresPromptSchemaInitializer"/>.
/// </summary>
public static class PostgresSchema
{
    /// <summary>The default main-table name used by <see cref="PostgresPromptRepository"/>.</summary>
    public const string DefaultTableName = "prompt_templates";

    /// <summary>The default lookup-table name for status values.</summary>
    public const string DefaultStatusTableName = "prompt_template_statuses";

    /// <summary>
    /// Returns idempotent DDL that creates both the status lookup table and the main
    /// templates table, seeds the four status rows, and creates the helper index. Safe to
    /// run repeatedly: every CREATE / INSERT statement is guarded with <c>IF NOT EXISTS</c>
    /// or <c>ON CONFLICT</c>.
    /// </summary>
    /// <param name="tableName">Optional override for the main-table name.</param>
    /// <param name="statusTableName">Optional override for the status lookup-table name.</param>
    public static string CreateSchemaSql(
        string tableName = DefaultTableName,
        string statusTableName = DefaultStatusTableName)
    {
        ValidateIdentifier(tableName);
        ValidateIdentifier(statusTableName);

        return $"""
            CREATE TABLE IF NOT EXISTS {statusTableName} (
                status_id    smallint  PRIMARY KEY,
                status_name  text      NOT NULL UNIQUE
            );

            INSERT INTO {statusTableName} (status_id, status_name) VALUES
                (1, 'Draft'),
                (2, 'Published'),
                (3, 'Deprecated'),
                (4, 'Archived')
            ON CONFLICT (status_id) DO NOTHING;

            -- The id column is intentionally without a DEFAULT. Callers are expected to supply
            -- a UUIDv7 (time-ordered) value, e.g. via .NET 9+ Guid.CreateVersion7(). Postgres 17+
            -- has a native uuidv7() function if a SQL-side default is preferred.
            CREATE TABLE IF NOT EXISTS {tableName} (
                id             uuid         PRIMARY KEY,
                namespace      text         NOT NULL,
                name           text         NOT NULL,
                version_major  int          NOT NULL,
                version_minor  int          NOT NULL,
                version_patch  int          NOT NULL,
                status_id      smallint     NOT NULL REFERENCES {statusTableName}(status_id),
                content_json   jsonb        NOT NULL,
                created_at     timestamptz  NOT NULL DEFAULT now(),
                created_by     text         NULL,
                deprecated_at  timestamptz  NULL,
                UNIQUE (namespace, name, version_major, version_minor, version_patch)
            );

            CREATE INDEX IF NOT EXISTS idx_{tableName}_key_lookup
                ON {tableName} (namespace, name, status_id,
                                version_major DESC, version_minor DESC, version_patch DESC);
            """;
    }

    /// <summary>
    /// Returns the ordered <see cref="Migration"/> list for the prompt store, suitable for handing
    /// directly to <see cref="MigrationRunner.ApplyAsync"/>. Migration ids include the table name
    /// so multiple prompt stores in the same database track migrations independently.
    /// </summary>
    /// <param name="tableName">Optional override for the main-table name.</param>
    /// <param name="statusTableName">Optional override for the status lookup-table name.</param>
    public static IReadOnlyList<Migration> GetMigrations(
        string tableName = DefaultTableName,
        string statusTableName = DefaultStatusTableName)
    {
        ValidateIdentifier(tableName);
        ValidateIdentifier(statusTableName);

        return
        [
            new Migration(
                Id: $"0001_init_{tableName}",
                Sql: CreateSchemaSql(tableName, statusTableName),
                Description: "Initial prompt-templates schema and status seed."),
            new Migration(
                Id: $"0002_add_archived_at_{tableName}",
                Sql: $"ALTER TABLE {tableName} ADD COLUMN IF NOT EXISTS archived_at timestamptz NULL;",
                Description: "Audit timestamp for Archived transitions."),
        ];
    }

    internal static void ValidateIdentifier(string identifier)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(identifier);

        // Conservative: allow only ASCII letters, digits, and underscores; must not start with a digit.
        // This is sufficient for the table-name parameter we accept and prevents SQL injection
        // via interpolation of the identifier into DDL/DML.
        var span = identifier.AsSpan();
        if (char.IsAsciiDigit(span[0]))
        {
            throw new ArgumentException($"Invalid SQL identifier '{identifier}': must not start with a digit.", nameof(identifier));
        }

        foreach (var c in span)
        {
            if (!(char.IsAsciiLetterOrDigit(c) || c == '_'))
            {
                throw new ArgumentException($"Invalid SQL identifier '{identifier}': must contain only ASCII letters, digits, and underscores.", nameof(identifier));
            }
        }
    }
}
