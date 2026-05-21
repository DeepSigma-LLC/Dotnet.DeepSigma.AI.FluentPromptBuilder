using DeepSigma.AI.FluentPromptBuilder.Repositories;
using DeepSigma.DataAccess.RelationalDatabase;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>DI helpers for registering the Postgres prompt repository.</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the DeepSigma.DataAccess.Postgres provider (connection factory,
    /// <see cref="RelationalDatabaseApi"/>, schema service, bulk copier, migration runner) and
    /// registers <see cref="PostgresPromptRepository"/> as a singleton. The same instance is
    /// also resolvable as <see cref="IPromptRepository"/> for read-only consumers; resolve the
    /// concrete type when you need access to write methods (<c>InsertAsync</c>,
    /// <c>UpdateContentAsync</c>, <c>SetStatusAsync</c>).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="tableName">Optional override for the prompt-templates table name.</param>
    /// <param name="configureDataSource">
    /// Optional callback to customize the underlying <see cref="NpgsqlDataSource"/> via
    /// <see cref="NpgsqlDataSourceBuilder"/>. Use for custom type handlers, enum / composite mapping,
    /// password providers, per-source logging, etc. Forwarded to <c>AddDeepSigmaPostgres</c>.
    /// </param>
    /// <param name="onConnectionOpened">
    /// Optional callback invoked every time a connection transitions to <c>Open</c>. Use for
    /// per-connection <c>SET</c> statements (<c>SET search_path</c>, <c>SET statement_timeout</c>, etc.).
    /// Forwarded to <c>AddDeepSigmaPostgres</c>.
    /// </param>
    public static IServiceCollection AddPostgresPromptRepository(
        this IServiceCollection services,
        string connectionString,
        string tableName = PostgresSchema.DefaultTableName,
        Action<NpgsqlDataSourceBuilder>? configureDataSource = null,
        Action<NpgsqlConnection>? onConnectionOpened = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        PostgresSchema.ValidateIdentifier(tableName);

        services.AddDeepSigmaPostgres(connectionString, configureDataSource, onConnectionOpened);
        services.AddSingleton(sp =>
            new PostgresPromptRepository(sp.GetRequiredService<RelationalDatabaseApi>(), tableName));
        services.AddSingleton<IPromptRepository>(sp => sp.GetRequiredService<PostgresPromptRepository>());
        return services;
    }
}
