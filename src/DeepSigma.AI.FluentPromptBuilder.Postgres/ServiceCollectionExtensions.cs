using DeepSigma.AI.FluentPromptBuilder.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>DI helpers for registering the Postgres prompt repository.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="PostgresPromptRepository"/> as the singleton
    /// <see cref="IPromptRepository"/>, building its own <see cref="NpgsqlDataSource"/> from the
    /// supplied connection string. The repository (and its data source) is disposed when the DI
    /// container is disposed.
    /// </summary>
    public static IServiceCollection AddPostgresPromptRepository(
        this IServiceCollection services,
        string connectionString,
        string tableName = PostgresSchema.DefaultTableName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        PostgresSchema.ValidateIdentifier(tableName);

        services.AddSingleton<IPromptRepository>(_ =>
            new PostgresPromptRepository(connectionString, tableName));
        return services;
    }

    /// <summary>
    /// Registers <see cref="PostgresPromptRepository"/> as the singleton
    /// <see cref="IPromptRepository"/> using a caller-managed <see cref="NpgsqlDataSource"/>.
    /// The data source's lifetime is the caller's responsibility.
    /// </summary>
    public static IServiceCollection AddPostgresPromptRepository(
        this IServiceCollection services,
        NpgsqlDataSource dataSource,
        string tableName = PostgresSchema.DefaultTableName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(dataSource);
        PostgresSchema.ValidateIdentifier(tableName);

        services.AddSingleton<IPromptRepository>(_ =>
            new PostgresPromptRepository(dataSource, tableName));
        return services;
    }
}
