using DeepSigma.AI.FluentPromptBuilder.Postgres;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres.Tests;

public class PostgresSchemaTests
{
    [Fact]
    public void DefaultTableName_IsExpectedConstant()
    {
        Assert.Equal("prompt_templates", PostgresSchema.DefaultTableName);
    }

    [Fact]
    public void CreateTableSql_ContainsRequiredColumnsAndPrimaryKey()
    {
        var sql = PostgresSchema.CreateTableSql();

        Assert.Contains("CREATE TABLE IF NOT EXISTS prompt_templates", sql, StringComparison.Ordinal);
        Assert.Contains("namespace", sql, StringComparison.Ordinal);
        Assert.Contains("name", sql, StringComparison.Ordinal);
        Assert.Contains("version_major", sql, StringComparison.Ordinal);
        Assert.Contains("version_minor", sql, StringComparison.Ordinal);
        Assert.Contains("version_patch", sql, StringComparison.Ordinal);
        Assert.Contains("content_json   jsonb", sql, StringComparison.Ordinal);
        Assert.Contains("PRIMARY KEY (namespace, name, version_major, version_minor, version_patch)",
            sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateTableSql_HonorsCustomTableName()
    {
        var sql = PostgresSchema.CreateTableSql("my_prompts");
        Assert.Contains("CREATE TABLE IF NOT EXISTS my_prompts", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("prompt_templates", sql, StringComparison.Ordinal);
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
