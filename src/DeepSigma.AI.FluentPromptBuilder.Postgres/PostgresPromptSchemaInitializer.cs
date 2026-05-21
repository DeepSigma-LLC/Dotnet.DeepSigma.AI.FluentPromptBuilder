using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>
/// Idempotent schema setup for the Postgres prompt store. Drives
/// <see cref="MigrationRunner"/> with the migration list from
/// <see cref="PostgresSchema.GetMigrations"/> — safe to call repeatedly, safe to call on a
/// database that pre-dates the migration runner (the initial migration's DDL is idempotent and
/// no-ops cleanly on existing tables).
/// </summary>
/// <remarks>
/// <para>
/// Production deployments are encouraged to use their own migration tool (Flyway, dbup,
/// EF migrations, etc.) and feed it <see cref="PostgresSchema.CreateSchemaSql(string, string)"/>
/// for the initial DDL. The migrations exposed by <see cref="PostgresSchema.GetMigrations"/>
/// can also be consumed directly via the DI-registered <see cref="MigrationRunner"/>.
/// </para>
/// </remarks>
public static class PostgresPromptSchemaInitializer
{
    // Mirrors the DDL inlined by AddDeepSigmaPostgres' MigrationRunner registration — we have to
    // duplicate the constant here because the upstream copy is internal, and this static helper
    // does not go through DI.
    private const string CreateMigrationsTableSql =
        "CREATE TABLE IF NOT EXISTS _migrations (Id TEXT NOT NULL PRIMARY KEY, AppliedAtUtc TIMESTAMPTZ NOT NULL);";

    /// <summary>
    /// Applies all pending prompt-store migrations against the database at
    /// <paramref name="connectionString"/>. Already-applied migrations are skipped.
    /// </summary>
    /// <param name="connectionString">A standard Postgres connection string.</param>
    /// <param name="tableName">Optional override for the main-table name.</param>
    /// <param name="statusTableName">Optional override for the status lookup-table name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ids of migrations that were newly applied during this call.</returns>
    public static async Task<IReadOnlyList<string>> EnsureCreatedAsync(
        string connectionString,
        string tableName = PostgresSchema.DefaultTableName,
        string statusTableName = PostgresSchema.DefaultStatusTableName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var db = new RelationalDatabaseApi(new PostgresConnectionFactory(connectionString));
        var runner = new MigrationRunner(db, CreateMigrationsTableSql);
        var migrations = PostgresSchema.GetMigrations(tableName, statusTableName);

        return await runner.ApplyAsync(migrations, cancellationToken).ConfigureAwait(false);
    }
}
