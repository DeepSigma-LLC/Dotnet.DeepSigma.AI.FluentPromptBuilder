using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using DeepSigma.AI.FluentPromptBuilder.Serialization;
using DeepSigma.DataAccess.RelationalDatabase;
using Npgsql;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>
/// A Postgres-backed <see cref="IPromptRepository"/> using
/// <see cref="RelationalDatabaseApi"/> from DeepSigma.DataAccess.Postgres. Templates are
/// stored as <c>jsonb</c> in the format produced by
/// <see cref="PromptTemplateJsonSerializer"/>, keyed by <c>(namespace, name, major, minor, patch)</c>.
/// </summary>
/// <remarks>
/// <para>Write surface is intentionally minimal and immutability-preserving:
/// <see cref="InsertAsync"/> creates new rows, <see cref="UpdateContentAsync"/> mutates
/// content only while a row is still in <see cref="PromptStatus.Draft"/>, and
/// <see cref="SetStatusAsync"/> performs forward-only status transitions
/// (Draft → Published → Deprecated → Archived). Hard deletes are intentionally not
/// supported — archive instead.</para>
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

    /// <summary>
    /// Inserts <paramref name="template"/> as a new row at status <paramref name="status"/>
    /// (default <see cref="PromptStatus.Draft"/>). A UUIDv7 id is generated client-side.
    /// </summary>
    /// <param name="template">The template to insert.</param>
    /// <param name="status">The initial lifecycle status. Defaults to <see cref="PromptStatus.Draft"/>.</param>
    /// <param name="createdBy">Optional audit value written to the <c>created_by</c> column.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="PromptWriteConflictException">
    /// Thrown if a row already exists at the template's <c>(namespace, name, version)</c>.
    /// </exception>
    public async Task InsertAsync(
        PromptTemplate template,
        PromptStatus status = PromptStatus.Draft,
        string? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var json = PromptTemplateJsonSerializer.Serialize(template);

        var sql = $"""
            INSERT INTO {_tableName}
                (id, namespace, name, version_major, version_minor, version_patch,
                 status_id, content_json, created_by)
            VALUES
                (@Id, @Namespace, @Name, @Major, @Minor, @Patch,
                 @StatusId, @Json::jsonb, @CreatedBy)
            """;

        var parameters = new
        {
            Id = Guid.CreateVersion7(),
            Namespace = template.Id.Key.Namespace,
            Name = template.Id.Key.Name,
            Major = template.Id.Version.Major,
            Minor = template.Id.Version.Minor,
            Patch = template.Id.Version.Patch,
            StatusId = (short)status,
            Json = json,
            CreatedBy = createdBy,
        };

        try
        {
            await _db.UpdateAsync(sql, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (PostgresException ex) when (ex.SqlState == "23505")
        {
            // unique_violation on the (namespace, name, version_*) natural key.
            throw new PromptWriteConflictException(
                $"A prompt template already exists at {template.Id.Key}@{template.Id.Version}.", ex);
        }
    }

    /// <summary>
    /// Updates the stored content for an existing row at
    /// <c>template.Id.(Key, Version)</c>. Only permitted while the row is in
    /// <see cref="PromptStatus.Draft"/> — once published, content is immutable; bump the version.
    /// </summary>
    /// <exception cref="PromptNotFoundException">If no row exists at the template's key+version.</exception>
    /// <exception cref="PromptWriteConflictException">If the row exists but is not in <see cref="PromptStatus.Draft"/>.</exception>
    public async Task UpdateContentAsync(
        PromptTemplate template,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(template);

        var key = template.Id.Key;
        var version = template.Id.Version;
        var currentStatus = await GetStatusOrThrowAsync(key, version, cancellationToken).ConfigureAwait(false);

        if (currentStatus != PromptStatus.Draft)
        {
            throw new PromptWriteConflictException(
                $"Cannot update content for {key}@{version}: row is {currentStatus}, content is only mutable while Draft. " +
                "Bump the version and InsertAsync a new row instead.");
        }

        var sql = $"""
            UPDATE {_tableName}
            SET    content_json = @Json::jsonb
            WHERE  namespace      = @Namespace
              AND  name           = @Name
              AND  version_major  = @Major
              AND  version_minor  = @Minor
              AND  version_patch  = @Patch
              AND  status_id      = @DraftStatusId
            """;

        var parameters = new
        {
            Json = PromptTemplateJsonSerializer.Serialize(template),
            Namespace = key.Namespace,
            Name = key.Name,
            Major = version.Major,
            Minor = version.Minor,
            Patch = version.Patch,
            DraftStatusId = (short)PromptStatus.Draft,
        };

        await _db.UpdateAsync(sql, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Transitions the row at <paramref name="key"/>+<paramref name="version"/> to
    /// <paramref name="newStatus"/>. Only forward transitions are allowed
    /// (Draft → Published → Deprecated → Archived). When transitioning to Deprecated,
    /// <c>deprecated_at</c> is set to <c>now()</c>.
    /// </summary>
    /// <exception cref="PromptNotFoundException">If no row exists at <paramref name="key"/>+<paramref name="version"/>.</exception>
    /// <exception cref="PromptWriteConflictException">If <paramref name="newStatus"/> would be a non-forward transition.</exception>
    public async Task SetStatusAsync(
        PromptKey key,
        PromptVersion version,
        PromptStatus newStatus,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var currentStatus = await GetStatusOrThrowAsync(key, version, cancellationToken).ConfigureAwait(false);

        if ((short)newStatus <= (short)currentStatus)
        {
            throw new PromptWriteConflictException(
                $"Invalid status transition for {key}@{version}: {currentStatus} → {newStatus}. " +
                "Transitions are forward-only (Draft → Published → Deprecated → Archived).");
        }

        var sql = $"""
            UPDATE {_tableName}
            SET    status_id     = @NewStatusId,
                   deprecated_at = CASE WHEN @NewStatusId = @DeprecatedStatusId THEN now() ELSE deprecated_at END
            WHERE  namespace      = @Namespace
              AND  name           = @Name
              AND  version_major  = @Major
              AND  version_minor  = @Minor
              AND  version_patch  = @Patch
            """;

        var parameters = new
        {
            NewStatusId = (short)newStatus,
            DeprecatedStatusId = (short)PromptStatus.Deprecated,
            Namespace = key.Namespace,
            Name = key.Name,
            Major = version.Major,
            Minor = version.Minor,
            Patch = version.Patch,
        };

        await _db.UpdateAsync(sql, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    private async Task<PromptStatus> GetStatusOrThrowAsync(
        PromptKey key,
        PromptVersion version,
        CancellationToken cancellationToken)
    {
        var sql = $"""
            SELECT status_id
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

        var statusId = await _db.QuerySingleOrDefaultAsync<object, short?>(
            sql, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);

        if (statusId is null)
        {
            throw new PromptNotFoundException(key, version);
        }

        return (PromptStatus)statusId.Value;
    }

    private async Task<PromptTemplate?> LoadSingleTemplateAsync(
        string sql,
        object parameters,
        PromptKey key,
        PromptVersion? version,
        CancellationToken cancellationToken)
    {
        var json = await _db.QuerySingleOrDefaultAsync<object, string?>(
            sql, parameters, cancellationToken: cancellationToken).ConfigureAwait(false);

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
