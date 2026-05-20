using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Postgres;
using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres.Tests.Integration;

[Collection(PostgresContainerCollection.Name)]
public class PostgresPromptRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly PostgresContainerFixture _fixture;
    private readonly PostgresPromptRepository _repo;

    public PostgresPromptRepositoryIntegrationTests(PostgresContainerFixture fixture)
    {
        _fixture = fixture;
        var db = new RelationalDatabaseApi(new PostgresConnectionFactory(fixture.ConnectionString));
        _repo = new PostgresPromptRepository(db);
    }

    public ValueTask InitializeAsync() => new(_fixture.ResetAsync());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task GetTemplateAsync_ExistingRow_ReturnsTemplate()
    {
        var key = new PromptKey("CodeReview", "SecurityReview");
        var version = new PromptVersion(1, 2, 3);
        await _fixture.SeedTemplateAsync(key, version);

        var result = await _repo.GetTemplateAsync(key, version);

        Assert.NotNull(result);
        Assert.Equal(key, result!.Id.Key);
        Assert.Equal(version, result.Id.Version);
    }

    [Fact]
    public async Task GetTemplateAsync_MissingRow_ReturnsNull()
    {
        var result = await _repo.GetTemplateAsync(
            new PromptKey("Missing", "Thing"),
            new PromptVersion(1));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTemplateAsync_VersionMismatch_ReturnsNull()
    {
        var key = new PromptKey("CodeReview", "SecurityReview");
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 0, 0));

        // Same key, different version — must not match.
        var result = await _repo.GetTemplateAsync(key, new PromptVersion(2, 0, 0));

        Assert.Null(result);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsHighestPublishedVersion()
    {
        var key = new PromptKey("CodeReview", "SecurityReview");
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 0, 0));
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 2, 0));
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 1, 9));
        await _fixture.SeedTemplateAsync(key, new PromptVersion(2, 0, 0));

        var result = await _repo.GetLatestAsync(key);

        Assert.NotNull(result);
        Assert.Equal(new PromptVersion(2, 0, 0), result!.Id.Version);
    }

    [Fact]
    public async Task GetLatestAsync_DefaultIgnoresNonPublishedRows()
    {
        var key = new PromptKey("CodeReview", "SecurityReview");
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 0, 0), PromptStatus.Published);
        // A higher-versioned Draft must not be returned by the default Published query.
        await _fixture.SeedTemplateAsync(key, new PromptVersion(9, 0, 0), PromptStatus.Draft);

        var result = await _repo.GetLatestAsync(key);

        Assert.NotNull(result);
        Assert.Equal(new PromptVersion(1, 0, 0), result!.Id.Version);
    }

    [Fact]
    public async Task GetLatestAsync_WithStatus_FiltersToThatStatus()
    {
        var key = new PromptKey("CodeReview", "SecurityReview");
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 0, 0), PromptStatus.Published);
        await _fixture.SeedTemplateAsync(key, new PromptVersion(2, 0, 0), PromptStatus.Draft);
        await _fixture.SeedTemplateAsync(key, new PromptVersion(3, 0, 0), PromptStatus.Draft);
        await _fixture.SeedTemplateAsync(key, new PromptVersion(4, 0, 0), PromptStatus.Archived);

        var draft = await _repo.GetLatestAsync(key, PromptStatus.Draft);

        Assert.NotNull(draft);
        Assert.Equal(new PromptVersion(3, 0, 0), draft!.Id.Version);
    }

    [Fact]
    public async Task GetLatestAsync_NoMatchingStatus_ReturnsNull()
    {
        var key = new PromptKey("CodeReview", "SecurityReview");
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 0, 0), PromptStatus.Published);

        var result = await _repo.GetLatestAsync(key, PromptStatus.Archived);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetVersionsAsync_ReturnsAllVersionsInAscendingOrder()
    {
        var key = new PromptKey("CodeReview", "SecurityReview");
        await _fixture.SeedTemplateAsync(key, new PromptVersion(2, 0, 0));
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 2, 0));
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 0, 0));
        await _fixture.SeedTemplateAsync(key, new PromptVersion(1, 1, 9));

        var versions = await _repo.GetVersionsAsync(key);

        Assert.Equal(
            new[]
            {
                new PromptVersion(1, 0, 0),
                new PromptVersion(1, 1, 9),
                new PromptVersion(1, 2, 0),
                new PromptVersion(2, 0, 0),
            },
            versions);
    }

    [Fact]
    public async Task GetVersionsAsync_NoRows_ReturnsEmpty()
    {
        var versions = await _repo.GetVersionsAsync(new PromptKey("Empty", "Key"));

        Assert.Empty(versions);
    }

    [Fact]
    public async Task GetVersionsAsync_IsScopedByKey()
    {
        var keyA = new PromptKey("CodeReview", "SecurityReview");
        var keyB = new PromptKey("CodeReview", "StyleReview");
        await _fixture.SeedTemplateAsync(keyA, new PromptVersion(1, 0, 0));
        await _fixture.SeedTemplateAsync(keyB, new PromptVersion(9, 9, 9));

        var versions = await _repo.GetVersionsAsync(keyA);

        Assert.Single(versions);
        Assert.Equal(new PromptVersion(1, 0, 0), versions[0]);
    }
}
