using DeepSigma.AI.FluentPromptBuilder.DependencyInjection;
using DeepSigma.AI.FluentPromptBuilder.Postgres;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres.Tests;

public class ServiceCollectionExtensionsTests
{
    private const string Conn = "Host=localhost;Database=x;Username=u;Password=p";

    [Fact]
    public void AddPostgresPromptRepository_ConnectionString_RegistersIPromptRepository()
    {
        var services = new ServiceCollection()
            .AddFluentPromptBuilder()
            .AddPostgresPromptRepository(Conn);

        using var provider = services.BuildServiceProvider();
        var repo = provider.GetRequiredService<IPromptRepository>();
        Assert.IsType<PostgresPromptRepository>(repo);
    }

    [Fact]
    public void AddPostgresPromptRepository_DataSource_RegistersIPromptRepository()
    {
        using var dataSource = NpgsqlDataSource.Create(Conn);
        var services = new ServiceCollection()
            .AddFluentPromptBuilder()
            .AddPostgresPromptRepository(dataSource);

        using var provider = services.BuildServiceProvider();
        var repo = provider.GetRequiredService<IPromptRepository>();
        Assert.IsType<PostgresPromptRepository>(repo);
    }

    [Fact]
    public void AddPostgresPromptRepository_ValidatesTableNameAtRegistration()
    {
        // Identifier validation runs at AddXxx time, not lazily, so misconfiguration is caught
        // during composition rather than at first request.
        Assert.Throws<ArgumentException>(() =>
            new ServiceCollection().AddPostgresPromptRepository(Conn, "bad table"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void AddPostgresPromptRepository_RejectsInvalidConnectionString(string? conn)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            new ServiceCollection().AddPostgresPromptRepository(conn!));
    }
}
