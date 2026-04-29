namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>
/// SQL fragments used by the Postgres adapter. Exposed so callers can hand the schema DDL to
/// their migration tool of choice (Flyway, dbup, EF migrations, manual SQL, etc.) instead of
/// relying on <c>EnsureSchemaCreatedAsync</c>.
/// </summary>
public static class PostgresSchema
{
    /// <summary>The default table name used by <see cref="PostgresPromptRepository"/>.</summary>
    public const string DefaultTableName = "prompt_templates";

    /// <summary>Returns idempotent <c>CREATE TABLE IF NOT EXISTS</c> DDL for the prompt-templates table.</summary>
    /// <param name="tableName">Optional table name override. Must be a simple identifier.</param>
    public static string CreateTableSql(string tableName = DefaultTableName)
    {
        ValidateIdentifier(tableName);
        return $"""
            CREATE TABLE IF NOT EXISTS {tableName} (
                namespace      text        NOT NULL,
                name           text        NOT NULL,
                version_major  int         NOT NULL,
                version_minor  int         NOT NULL,
                version_patch  int         NOT NULL,
                content_json   jsonb       NOT NULL,
                created_at     timestamptz NOT NULL DEFAULT now(),
                PRIMARY KEY (namespace, name, version_major, version_minor, version_patch)
            );
            """;
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
