using DeepSigma.AI.FluentPromptBuilder.Postgres;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres.Tests;

public class PostgresSchemaTests
{
    [Fact]
    public void DefaultTableNames_AreExpectedConstants()
    {
        Assert.Equal("prompt_templates", PostgresSchema.DefaultTableName);
        Assert.Equal("prompt_template_statuses", PostgresSchema.DefaultStatusTableName);
    }

    [Fact]
    public void CreateSchemaSql_CreatesBothTables()
    {
        var sql = PostgresSchema.CreateSchemaSql();

        Assert.Contains("CREATE TABLE IF NOT EXISTS prompt_template_statuses", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS prompt_templates", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSchemaSql_SeedsAllFourStatuses()
    {
        var sql = PostgresSchema.CreateSchemaSql();

        Assert.Contains("(1, 'Draft')", sql, StringComparison.Ordinal);
        Assert.Contains("(2, 'Published')", sql, StringComparison.Ordinal);
        Assert.Contains("(3, 'Deprecated')", sql, StringComparison.Ordinal);
        Assert.Contains("(4, 'Archived')", sql, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT (status_id) DO NOTHING", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSchemaSql_DeclaresUuidPrimaryKeyWithoutDefault()
    {
        var sql = PostgresSchema.CreateSchemaSql();

        // id column is uuid PRIMARY KEY with no DEFAULT — callers supply a UUIDv7.
        Assert.Contains("id             uuid         PRIMARY KEY,", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DEFAULT gen_random_uuid()", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DEFAULT uuidv7()", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSchemaSql_HasAuditColumns()
    {
        var sql = PostgresSchema.CreateSchemaSql();

        Assert.Contains("created_at     timestamptz  NOT NULL DEFAULT now()", sql, StringComparison.Ordinal);
        Assert.Contains("created_by     text         NULL", sql, StringComparison.Ordinal);
        Assert.Contains("deprecated_at  timestamptz  NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSchemaSql_HasForeignKeyToStatusTable()
    {
        var sql = PostgresSchema.CreateSchemaSql();

        Assert.Contains("status_id      smallint     NOT NULL REFERENCES prompt_template_statuses(status_id)",
            sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSchemaSql_HasUniqueConstraintOnNaturalKey()
    {
        var sql = PostgresSchema.CreateSchemaSql();
        Assert.Contains("UNIQUE (namespace, name, version_major, version_minor, version_patch)",
            sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSchemaSql_CreatesLookupIndex()
    {
        var sql = PostgresSchema.CreateSchemaSql();
        Assert.Contains("CREATE INDEX IF NOT EXISTS idx_prompt_templates_key_lookup", sql, StringComparison.Ordinal);
        Assert.Contains("ON prompt_templates (namespace, name, status_id,", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSchemaSql_HonorsCustomTableNames()
    {
        var sql = PostgresSchema.CreateSchemaSql("my_prompts", "my_statuses");

        Assert.Contains("CREATE TABLE IF NOT EXISTS my_statuses", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS my_prompts", sql, StringComparison.Ordinal);
        Assert.Contains("REFERENCES my_statuses(status_id)", sql, StringComparison.Ordinal);
        Assert.Contains("idx_my_prompts_key_lookup", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt_templates", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateSchemaSql_IsIdempotent()
    {
        var sql = PostgresSchema.CreateSchemaSql();
        // Every CREATE / INSERT must be guarded so re-running the DDL is safe.
        Assert.Contains("CREATE TABLE IF NOT EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX IF NOT EXISTS", sql, StringComparison.Ordinal);
        Assert.Contains("ON CONFLICT", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("good_table")]
    [InlineData("ALSO_GOOD")]
    [InlineData("table9")]
    [InlineData("a")]
    public void ValidateIdentifier_AcceptsValidNames(string name)
    {
        PostgresSchema.ValidateIdentifier(name);  // should not throw
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("9_starts_with_digit")]
    [InlineData("has-hyphen")]
    [InlineData("has space")]
    [InlineData("has;semicolon")]
    [InlineData("drop\"quote")]
    [InlineData("schema.table")]
    public void ValidateIdentifier_RejectsInvalidNames(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => PostgresSchema.ValidateIdentifier(name!));
    }
}

public class PromptStatusTests
{
    [Theory]
    [InlineData(PromptStatus.Draft, 1)]
    [InlineData(PromptStatus.Published, 2)]
    [InlineData(PromptStatus.Deprecated, 3)]
    [InlineData(PromptStatus.Archived, 4)]
    public void EnumValues_MatchStatusIdSeedData(PromptStatus status, int expectedId)
    {
        // Stable numeric values: do not renumber. The schema seed (status_id, status_name)
        // pairs must match these.
        Assert.Equal(expectedId, (short)status);
    }
}
