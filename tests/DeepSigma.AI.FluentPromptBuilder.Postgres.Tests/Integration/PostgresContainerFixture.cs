using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Postgres;
using DeepSigma.AI.FluentPromptBuilder.Serialization;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres.Tests.Integration;

/// <summary>
/// Boots a single PostgreSQL container for the test collection, creates the prompt schema
/// once, and exposes seed/cleanup helpers so individual tests stay isolated.
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

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await PostgresPromptSchemaInitializer.EnsureCreatedAsync(ConnectionString);
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }

    /// <summary>Truncates the prompt table between tests so each test starts from a clean slate.</summary>
    public async Task ResetAsync()
    {
        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("TRUNCATE TABLE prompt_templates", conn);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Inserts a row directly via Npgsql (bypassing the repository, so seed and read are
    /// independent). Serializes <paramref name="template"/> using the production serializer.
    /// </summary>
    public async Task SeedAsync(PromptTemplate template, PromptStatus status = PromptStatus.Published)
    {
        var json = PromptTemplateJsonSerializer.Serialize(template);

        const string sql = """
            INSERT INTO prompt_templates
                (id, namespace, name, version_major, version_minor, version_patch,
                 status_id, content_json)
            VALUES
                (@id, @ns, @name, @major, @minor, @patch, @status, @json::jsonb)
            """;

        await using var conn = new NpgsqlConnection(ConnectionString);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        cmd.Parameters.AddWithValue("id", Guid.CreateVersion7());
        cmd.Parameters.AddWithValue("ns", template.Id.Key.Namespace);
        cmd.Parameters.AddWithValue("name", template.Id.Key.Name);
        cmd.Parameters.AddWithValue("major", template.Id.Version.Major);
        cmd.Parameters.AddWithValue("minor", template.Id.Version.Minor);
        cmd.Parameters.AddWithValue("patch", template.Id.Version.Patch);
        cmd.Parameters.AddWithValue("status", (short)status);
        cmd.Parameters.AddWithValue("json", json);
        await cmd.ExecuteNonQueryAsync();
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
