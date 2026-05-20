using DeepSigma.AI.FluentPromptBuilder.Repositories;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.DependencyInjection;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>DI helpers for registering the Postgres prompt repository.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DeepSigma.DataAccess.Postgres provider (connection factory,
    /// <see cref="RelationalDatabaseApi"/>, schema service, bulk copier) and registers
    /// <see cref="PostgresPromptRepository"/> as the singleton <see cref="IPromptRepository"/>.
    /// </summary>
    public static IServiceCollection AddPostgresPromptRepository(
        this IServiceCollection services,
        string connectionString,
        string tableName = PostgresSchema.DefaultTableName)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        PostgresSchema.ValidateIdentifier(tableName);

        services.AddDeepSigmaPostgres(connectionString);
        services.AddSingleton<IPromptRepository>(sp =>
            new PostgresPromptRepository(sp.GetRequiredService<RelationalDatabaseApi>(), tableName));
        return services;
    }
}
