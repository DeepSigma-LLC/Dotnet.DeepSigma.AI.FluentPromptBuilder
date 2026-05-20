using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>
/// Idempotent schema setup for the Postgres prompt store. Intended for local/dev scenarios;
/// production deployments should use a real migration tool (Flyway, dbup, EF migrations, etc.)
/// and feed it the DDL from <see cref="PostgresSchema.CreateSchemaSql(string, string)"/>.
/// </summary>
public static class PostgresPromptSchemaInitializer
{
    /// <summary>
    /// Idempotently creates the lookup table, the main table, the helper index, and seeds the
    /// four <see cref="PromptStatus"/> rows.
    /// </summary>
    /// <param name="connectionString">A standard Postgres connection string.</param>
    /// <param name="tableName">Optional override for the main-table name.</param>
    /// <param name="statusTableName">Optional override for the status lookup-table name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task EnsureCreatedAsync(
        string connectionString,
        string tableName = PostgresSchema.DefaultTableName,
        string statusTableName = PostgresSchema.DefaultStatusTableName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        PostgresSchema.ValidateIdentifier(tableName);
        PostgresSchema.ValidateIdentifier(statusTableName);

        var factory = new PostgresConnectionFactory(connectionString);
        var db = new RelationalDatabaseApi(factory);
        await db.UpdateAsync<object?>(
            PostgresSchema.CreateSchemaSql(tableName, statusTableName),
            null,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
