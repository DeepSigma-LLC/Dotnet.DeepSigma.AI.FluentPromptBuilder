using DeepSigma.AI.FluentPromptBuilder.Postgres;
using Npgsql;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres.Tests;

public class PostgresPromptRepositoryConstructorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_NullOrEmptyConnectionString_Throws(string? connectionString)
    {
        // ArgumentException.ThrowIfNullOrWhiteSpace throws ArgumentNullException for null and
        // ArgumentException for whitespace; both derive from ArgumentException.
        Assert.ThrowsAny<ArgumentException>(() => new PostgresPromptRepository(connectionString!));
    }

    [Fact]
    public void Constructor_NullDataSource_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PostgresPromptRepository((NpgsqlDataSource)null!));
    }

    [Fact]
    public void Constructor_InvalidTableName_Throws()
    {
        // The constructor validates the table-name identifier even though no connection is opened.
        Assert.Throws<ArgumentException>(() =>
            new PostgresPromptRepository("Host=localhost;Database=x;Username=u;Password=p", "bad name"));
    }

    [Fact]
    public void TableName_DefaultsToPostgresSchemaDefault()
    {
        using var ds = NpgsqlDataSource.Create("Host=localhost;Database=x;Username=u;Password=p");
        var repo = new PostgresPromptRepository(ds);
        Assert.Equal(PostgresSchema.DefaultTableName, repo.TableName);
    }

    [Fact]
    public void TableName_HonorsOverride()
    {
        using var ds = NpgsqlDataSource.Create("Host=localhost;Database=x;Username=u;Password=p");
        var repo = new PostgresPromptRepository(ds, "custom_table");
        Assert.Equal("custom_table", repo.TableName);
    }
}
