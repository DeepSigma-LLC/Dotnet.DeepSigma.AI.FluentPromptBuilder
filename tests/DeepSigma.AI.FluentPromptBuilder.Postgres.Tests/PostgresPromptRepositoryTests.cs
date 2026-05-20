using DeepSigma.AI.FluentPromptBuilder.Postgres;
using DeepSigma.DataAccess.Postgres;
using DeepSigma.DataAccess.RelationalDatabase;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres.Tests;

public class PostgresPromptRepositoryConstructorTests
{
    private const string Conn = "Host=localhost;Database=x;Username=u;Password=p";

    private static RelationalDatabaseApi NewApi() =>
        new(new PostgresConnectionFactory(Conn));

    [Fact]
    public void Constructor_NullDb_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new PostgresPromptRepository((RelationalDatabaseApi)null!));
    }

    [Fact]
    public void Constructor_InvalidTableName_Throws()
    {
        Assert.Throws<ArgumentException>(() => new PostgresPromptRepository(NewApi(), "bad name"));
    }

    [Fact]
    public void TableName_DefaultsToPostgresSchemaDefault()
    {
        var repo = new PostgresPromptRepository(NewApi());
        Assert.Equal(PostgresSchema.DefaultTableName, repo.TableName);
    }

    [Fact]
    public void TableName_HonorsOverride()
    {
        var repo = new PostgresPromptRepository(NewApi(), "custom_table");
        Assert.Equal("custom_table", repo.TableName);
    }
}
