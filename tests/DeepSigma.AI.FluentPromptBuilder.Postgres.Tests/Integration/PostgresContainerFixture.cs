using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Postgres;
using DeepSigma.AI.FluentPromptBuilder.Serialization;
using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;
using Testcontainers.PostgreSql;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres.Tests.Integration;

/// <summary>
/// Boots a single PostgreSQL container for the test collection, creates the prompt schema
/// once, and exposes a shared <see cref="RelationalDatabaseApi"/> plus seed/cleanup helpers so
/// individual tests stay isolated.
/// </summary>
public sealed class PostgresContainerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("prompts_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    /// <summary>The shared data-access API. Available after <see cref="InitializeAsync"/>.</summary>
    public RelationalDatabaseApi Db { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        Db = new RelationalDatabaseApi(new PostgresConnectionFactory(ConnectionString));
        await PostgresPromptSchemaInitializer.EnsureCreatedAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>Truncates the prompt table between tests so each test starts from a clean slate.</summary>
    public Task ResetAsync(CancellationToken cancellationToken = default) =>
        Db.ExecuteAsync("TRUNCATE TABLE prompt_templates", cancellationToken: cancellationToken);

    /// <summary>
    /// Inserts a row through <see cref="RelationalDatabaseApi"/>, serializing
    /// <paramref name="template"/> with the production serializer.
    /// </summary>
    public Task SeedAsync(PromptTemplate template, PromptStatus status = PromptStatus.Published)
    {
        const string sql = """
            INSERT INTO prompt_templates
                (id, namespace, name, version_major, version_minor, version_patch,
                 status_id, content_json)
            VALUES
                (@Id, @Namespace, @Name, @Major, @Minor, @Patch, @StatusId, @Json::jsonb)
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
            Json = PromptTemplateJsonSerializer.Serialize(template),
        };

        return Db.ExecuteAsync(sql, parameters);
    }

    /// <summary>Convenience: build and seed a minimal template at the supplied key/version/status.</summary>
    public Task SeedTemplateAsync(PromptKey key, PromptVersion version, PromptStatus status = PromptStatus.Published)
    {
        var template = new PromptTemplate(
            new VersionedPromptKey(key, version),
            [new PromptMessage(PromptRole.System,
                [new PromptSection("Role", new TextContent($"{key}@{version} ({status})"))])],
            [],
            new PromptMetadata());
        return SeedAsync(template, status);
    }
}

/// <summary>
/// Collection definition so multiple integration test classes share one container instance
/// across the whole test run.
/// </summary>
[CollectionDefinition(Name)]
[System.Diagnostics.CodeAnalysis.SuppressMessage("Naming", "CA1711:Identifiers should not have incorrect suffix",
    Justification = "xunit ICollectionFixture marker types conventionally end in 'Collection'.")]
public sealed class PostgresContainerCollection : ICollectionFixture<PostgresContainerFixture>
{
    public const string Name = "postgres-container";
}
