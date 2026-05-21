using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Postgres;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres.Tests.Integration;

[Collection(PostgresContainerCollection.Name)]
public class PostgresPromptRepositoryWriteTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private readonly PostgresPromptRepository _repo;

    public PostgresPromptRepositoryWriteTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        _repo = new PostgresPromptRepository(fixture.Db);
    }

    public ValueTask InitializeAsync() => new(_fixture.ResetAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static PromptTemplate BuildTemplate(PromptKey key, PromptVersion version, string text)
    {
        return new PromptTemplate(
            new VersionedPromptKey(key, version),
            [new PromptMessage(PromptRole.System,
                [new PromptSection("Role", new TextContent(text))])],
            [],
            new PromptMetadata());
    }

    // ----- InsertAsync ---------------------------------------------------------------------

    [Fact]
    public async Task InsertAsync_NewRow_IsRetrievableViaGetTemplate()
    {
        var key = new PromptKey("Write", "Insert");
        var version = new PromptVersion(1, 0, 0);
        var template = BuildTemplate(key, version, "hello");

        await _repo.InsertAsync(template);

        var roundTripped = await _repo.GetTemplateAsync(key, version);
        Assert.NotNull(roundTripped);
        Assert.Equal(key, roundTripped!.Id.Key);
        Assert.Equal(version, roundTripped.Id.Version);
    }

    [Fact]
    public async Task InsertAsync_DefaultsToDraftStatus()
    {
        var key = new PromptKey("Write", "DefaultStatus");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "x"));

        // Draft rows must not appear in the default GetLatestAsync (which filters to Published).
        Assert.Null(await _repo.GetLatestAsync(key));
        // But they must appear when explicitly asking for Draft.
        Assert.NotNull(await _repo.GetLatestAsync(key, PromptStatus.Draft));
    }

    [Fact]
    public async Task InsertAsync_RespectsCustomStatus()
    {
        var key = new PromptKey("Write", "PublishedAtInsert");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "x"), PromptStatus.Published);

        Assert.NotNull(await _repo.GetLatestAsync(key));
    }

    [Fact]
    public async Task InsertAsync_DuplicateKey_ThrowsWriteConflict()
    {
        var key = new PromptKey("Write", "Duplicate");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "first"));

        await Assert.ThrowsAsync<PromptWriteConflictException>(
            () => _repo.InsertAsync(BuildTemplate(key, version, "second")));
    }

    [Fact]
    public async Task InsertAsync_CreatedBy_IsPersisted()
    {
        var key = new PromptKey("Write", "CreatedBy");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "x"), createdBy: "alice@example.com");

        var createdBy = await ReadCreatedByAsync(key, version);
        Assert.Equal("alice@example.com", createdBy);
    }

    // ----- UpdateContentAsync --------------------------------------------------------------

    [Fact]
    public async Task UpdateContentAsync_DraftRow_ReplacesContent()
    {
        var key = new PromptKey("Write", "DraftUpdate");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "before"));

        await _repo.UpdateContentAsync(BuildTemplate(key, version, "after"));

        var roundTripped = await _repo.GetTemplateAsync(key, version);
        var text = ((TextContent)roundTripped!.Messages[0].Sections[0].Content).Text;
        Assert.Equal("after", text);
    }

    [Fact]
    public async Task UpdateContentAsync_PublishedRow_ThrowsWriteConflict()
    {
        var key = new PromptKey("Write", "ImmutablePublished");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "before"), PromptStatus.Published);

        await Assert.ThrowsAsync<PromptWriteConflictException>(
            () => _repo.UpdateContentAsync(BuildTemplate(key, version, "after")));
    }

    [Fact]
    public async Task UpdateContentAsync_MissingRow_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<PromptNotFoundException>(
            () => _repo.UpdateContentAsync(BuildTemplate(
                new PromptKey("Write", "Missing"), new PromptVersion(1, 0, 0), "x")));
    }

    // ----- SetStatusAsync ------------------------------------------------------------------

    [Fact]
    public async Task SetStatusAsync_DraftToPublished_Succeeds()
    {
        var key = new PromptKey("Write", "Publish");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "x"));

        await _repo.SetStatusAsync(key, version, PromptStatus.Published);

        Assert.NotNull(await _repo.GetLatestAsync(key));
    }

    [Fact]
    public async Task SetStatusAsync_PublishedToDeprecated_SetsDeprecatedAt()
    {
        var key = new PromptKey("Write", "Deprecate");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "x"), PromptStatus.Published);

        var before = DateTime.UtcNow;
        await _repo.SetStatusAsync(key, version, PromptStatus.Deprecated);
        var after = DateTime.UtcNow;

        var deprecatedAt = await ReadDeprecatedAtAsync(key, version);
        Assert.NotNull(deprecatedAt);
        Assert.InRange(deprecatedAt!.Value, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public async Task SetStatusAsync_PublishedToArchived_SetsArchivedAtButNotDeprecatedAt()
    {
        var key = new PromptKey("Write", "DirectArchive");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "x"), PromptStatus.Published);

        var before = DateTime.UtcNow;
        await _repo.SetStatusAsync(key, version, PromptStatus.Archived);
        var after = DateTime.UtcNow;

        Assert.Null(await ReadDeprecatedAtAsync(key, version));
        var archivedAt = await ReadArchivedAtAsync(key, version);
        Assert.NotNull(archivedAt);
        Assert.InRange(archivedAt!.Value, before.AddSeconds(-1), after.AddSeconds(1));
    }

    [Fact]
    public async Task SetStatusAsync_DeprecatedToArchived_PreservesDeprecatedAtAndSetsArchivedAt()
    {
        var key = new PromptKey("Write", "DeprecateThenArchive");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "x"), PromptStatus.Published);
        await _repo.SetStatusAsync(key, version, PromptStatus.Deprecated);
        var deprecatedAt = await ReadDeprecatedAtAsync(key, version);

        await _repo.SetStatusAsync(key, version, PromptStatus.Archived);

        // deprecated_at must be preserved through the Archive transition.
        var stillDeprecatedAt = await ReadDeprecatedAtAsync(key, version);
        Assert.Equal(deprecatedAt, stillDeprecatedAt);
        Assert.NotNull(await ReadArchivedAtAsync(key, version));
    }

    [Fact]
    public async Task SetStatusAsync_BackwardTransition_ThrowsWriteConflict()
    {
        var key = new PromptKey("Write", "Backward");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "x"), PromptStatus.Published);

        await Assert.ThrowsAsync<PromptWriteConflictException>(
            () => _repo.SetStatusAsync(key, version, PromptStatus.Draft));
    }

    [Fact]
    public async Task SetStatusAsync_SameStatus_ThrowsWriteConflict()
    {
        var key = new PromptKey("Write", "SameStatus");
        var version = new PromptVersion(1, 0, 0);
        await _repo.InsertAsync(BuildTemplate(key, version, "x"), PromptStatus.Published);

        await Assert.ThrowsAsync<PromptWriteConflictException>(
            () => _repo.SetStatusAsync(key, version, PromptStatus.Published));
    }

    [Fact]
    public async Task SetStatusAsync_MissingRow_ThrowsNotFound()
    {
        await Assert.ThrowsAsync<PromptNotFoundException>(
            () => _repo.SetStatusAsync(
                new PromptKey("Write", "Missing"),
                new PromptVersion(1, 0, 0),
                PromptStatus.Published));
    }

    // ----- Helpers -------------------------------------------------------------------------

    private Task<string?> ReadCreatedByAsync(PromptKey key, PromptVersion version) =>
        ReadScalarColumnAsync<string?>("created_by", key, version);

    private Task<DateTime?> ReadDeprecatedAtAsync(PromptKey key, PromptVersion version) =>
        ReadScalarColumnAsync<DateTime?>("deprecated_at", key, version);

    private Task<DateTime?> ReadArchivedAtAsync(PromptKey key, PromptVersion version) =>
        ReadScalarColumnAsync<DateTime?>("archived_at", key, version);

    private Task<T?> ReadScalarColumnAsync<T>(string column, PromptKey key, PromptVersion version)
    {
        // Column name is hard-coded by the caller, not user input — safe to interpolate.
        var sql = $"""
            SELECT {column}
            FROM   prompt_templates
            WHERE  namespace      = @Namespace
              AND  name           = @Name
              AND  version_major  = @Major
              AND  version_minor  = @Minor
              AND  version_patch  = @Patch
            """;

        return _fixture.Db.QuerySingleOrDefaultAsync<object, T?>(
            sql,
            new
            {
                Namespace = key.Namespace,
                Name = key.Name,
                Major = version.Major,
                Minor = version.Minor,
                Patch = version.Patch,
            });
    }
}
