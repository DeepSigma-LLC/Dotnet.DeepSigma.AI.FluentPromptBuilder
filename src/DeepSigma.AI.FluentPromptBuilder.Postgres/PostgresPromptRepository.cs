using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using DeepSigma.AI.FluentPromptBuilder.Serialization;
using DeepSigma.DataAccess.RelationalDatabase;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>
/// A Postgres-backed <see cref="IPromptRepository"/> using
/// <see cref="RelationalDatabaseApi"/> from DeepSigma.DataAccess.Postgres. Templates are
/// stored as <c>jsonb</c> in the format produced by
/// <see cref="PromptTemplateJsonSerializer"/>, keyed by <c>(namespace, name, major, minor, patch)</c>.
/// </summary>
/// <remarks>
/// <para>v1 is read-only; populate the table from your own migration tool, seed script, or
/// admin UI. Future versions may add a separate write-side concrete API.</para>
/// </remarks>
public sealed class PostgresPromptRepository : IPromptRepository
{
    private readonly RelationalDatabaseApi _db;
    private readonly string _tableName;

    /// <summary>The table name this repository reads from.</summary>
    public string TableName => _tableName;

    /// <summary>
    /// Constructs a repository over the supplied <see cref="RelationalDatabaseApi"/>.
    /// </summary>
    /// <param name="db">The relational-database API (typically resolved from DI).</param>
    /// <param name="tableName">Optional table-name override (default: <c>prompt_templates</c>).</param>
    public PostgresPromptRepository(RelationalDatabaseApi db, string tableName = PostgresSchema.DefaultTableName)
    {
        ArgumentNullException.ThrowIfNull(db);
        PostgresSchema.ValidateIdentifier(tableName);

        _db = db;
        _tableName = tableName;
    }

    /// <inheritdoc/>
    public Task<PromptTemplate?> GetTemplateAsync(
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

        var parameters = new
        {
            Namespace = key.Namespace,
            Name = key.Name,
            Major = version.Major,
            Minor = version.Minor,
            Patch = version.Patch,
        };

        return LoadSingleTemplateAsync(sql, parameters, key, version, cancellationToken);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Returns the latest <see cref="PromptStatus.Published"/> version. Use the
    /// <see cref="GetLatestAsync(PromptKey, PromptStatus, CancellationToken)"/> overload to
    /// query for other statuses (Draft / Deprecated / Archived).
    /// </remarks>
    public Task<PromptTemplate?> GetLatestAsync(
        PromptKey key,
        CancellationToken cancellationToken = default)
        => GetLatestAsync(key, PromptStatus.Published, cancellationToken);

    /// <summary>
    /// Returns the highest-versioned template for <paramref name="key"/> whose status equals
    /// <paramref name="status"/>, or <c>null</c> if no such row exists.
    /// </summary>
    public Task<PromptTemplate?> GetLatestAsync(
        PromptKey key,
        PromptStatus status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var sql = $"""
            SELECT content_json::text
            FROM   {_tableName}
            WHERE  namespace = @Namespace
              AND  name      = @Name
              AND  status_id = @StatusId
            ORDER BY version_major DESC, version_minor DESC, version_patch DESC
            LIMIT 1
            """;

        var parameters = new
        {
            Namespace = key.Namespace,
            Name = key.Name,
            StatusId = (short)status,
        };

        return LoadSingleTemplateAsync(sql, parameters, key, version: null, cancellationToken);
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

        var rows = await _db.GetAllAsync<object, VersionRow>(
            sql,
            new { Namespace = key.Namespace, Name = key.Name },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        return rows.Select(r => new PromptVersion(r.Major, r.Minor, r.Patch)).ToList();
    }

    private async Task<PromptTemplate?> LoadSingleTemplateAsync(
        string sql,
        object parameters,
        PromptKey key,
        PromptVersion? version,
        CancellationToken cancellationToken)
    {
        var rows = await _db.GetAllAsync<object, string?>(
            sql, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);

        var json = rows.FirstOrDefault();
        return json is null ? null : DeserializeOrThrow(json, key, version);
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
