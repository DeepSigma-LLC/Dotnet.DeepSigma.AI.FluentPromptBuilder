using System.Data.Common;
using Dapper;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using DeepSigma.AI.FluentPromptBuilder.Serialization;
using Npgsql;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>
/// A Postgres-backed <see cref="IPromptRepository"/> using Dapper + Npgsql. Templates are
/// stored as <c>jsonb</c> in the format produced by
/// <see cref="PromptTemplateJsonSerializer"/>, keyed by <c>(namespace, name, major, minor, patch)</c>.
/// </summary>
/// <remarks>
/// <para>v1 is read-only; populate the table from your own migration tool, seed script, or
/// admin UI. Future versions may add a separate write-side concrete API.</para>
/// </remarks>
public sealed class PostgresPromptRepository : IPromptRepository, IDisposable, IAsyncDisposable
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly bool _ownsDataSource;
    private readonly string _tableName;

    /// <summary>The table name this repository reads from.</summary>
    public string TableName => _tableName;

    /// <summary>
    /// Constructs a repository that owns its own <see cref="NpgsqlDataSource"/> built from the
    /// supplied connection string.
    /// </summary>
    /// <param name="connectionString">A standard Postgres connection string.</param>
    /// <param name="tableName">Optional table-name override (default: <c>prompt_templates</c>).</param>
    public PostgresPromptRepository(string connectionString, string tableName = PostgresSchema.DefaultTableName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        PostgresSchema.ValidateIdentifier(tableName);

        _dataSource = NpgsqlDataSource.Create(connectionString);
        _ownsDataSource = true;
        _tableName = tableName;
    }

    /// <summary>
    /// Constructs a repository that uses a caller-managed <see cref="NpgsqlDataSource"/>. The
    /// caller is responsible for the data source lifetime.
    /// </summary>
    public PostgresPromptRepository(NpgsqlDataSource dataSource, string tableName = PostgresSchema.DefaultTableName)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        PostgresSchema.ValidateIdentifier(tableName);

        _dataSource = dataSource;
        _ownsDataSource = false;
        _tableName = tableName;
    }

    /// <inheritdoc/>
    public async Task<PromptTemplate?> GetTemplateAsync(
        PromptKey key,
        PromptVersion version,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var sql = $"""
            SELECT content_json::text
            FROM   {_tableName}
            WHERE  namespace      = @Namespace
              AND  name           = @Name
              AND  version_major  = @Major
              AND  version_minor  = @Minor
              AND  version_patch  = @Patch
            LIMIT 1
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var json = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                sql,
                new
                {
                    Namespace = key.Namespace,
                    Name = key.Name,
                    Major = version.Major,
                    Minor = version.Minor,
                    Patch = version.Patch,
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return json is null ? null : DeserializeOrThrow(json, key, version);
    }

    /// <inheritdoc/>
    public async Task<PromptTemplate?> GetLatestAsync(
        PromptKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var sql = $"""
            SELECT content_json::text
            FROM   {_tableName}
            WHERE  namespace = @Namespace
              AND  name      = @Name
            ORDER BY version_major DESC, version_minor DESC, version_patch DESC
            LIMIT 1
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var json = await connection.QuerySingleOrDefaultAsync<string?>(
            new CommandDefinition(
                sql,
                new { Namespace = key.Namespace, Name = key.Name },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return json is null ? null : DeserializeOrThrow(json, key, version: null);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(
        PromptKey key,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var sql = $"""
            SELECT version_major AS Major, version_minor AS Minor, version_patch AS Patch
            FROM   {_tableName}
            WHERE  namespace = @Namespace
              AND  name      = @Name
            ORDER BY version_major ASC, version_minor ASC, version_patch ASC
            """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var rows = await connection.QueryAsync<VersionRow>(
            new CommandDefinition(
                sql,
                new { Namespace = key.Namespace, Name = key.Name },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return rows.Select(r => new PromptVersion(r.Major, r.Minor, r.Patch)).ToList();
    }

    /// <summary>
    /// Idempotently creates the schema for this repository's table. Intended for local/dev
    /// scenarios. Production deployments should use a real migration tool.
    /// </summary>
    public static async Task EnsureSchemaCreatedAsync(
        string connectionString,
        string tableName = PostgresSchema.DefaultTableName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        PostgresSchema.ValidateIdentifier(tableName);

        await using var dataSource = NpgsqlDataSource.Create(connectionString);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await connection.ExecuteAsync(
            new CommandDefinition(
                PostgresSchema.CreateTableSql(tableName),
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_ownsDataSource)
        {
            _dataSource.Dispose();
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_ownsDataSource)
        {
            await _dataSource.DisposeAsync().ConfigureAwait(false);
        }
    }

    private static PromptTemplate DeserializeOrThrow(string json, PromptKey key, PromptVersion? version)
    {
        try
        {
            return PromptTemplateJsonSerializer.Deserialize(json);
        }
        catch (PromptSerializationException ex)
        {
            var location = version is null ? key.ToString() : $"{key}@{version}";
            throw new PromptSerializationException(
                $"Failed to deserialize stored template for {location}: {ex.Message}", ex);
        }
    }

    private sealed class VersionRow
    {
        public int Major { get; set; }
        public int Minor { get; set; }
        public int Patch { get; set; }
    }
}
