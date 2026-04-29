using DeepSigma.AI.FluentPromptBuilder.Rendering;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using DeepSigma.AI.FluentPromptBuilder.Templates;
using Microsoft.Extensions.DependencyInjection;

namespace DeepSigma.AI.FluentPromptBuilder.DependencyInjection;

/// <summary>
/// Registers DeepSigma.AI.FluentPromptBuilder services with an
/// <see cref="IServiceCollection"/>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers the default template renderer, prompt factory, and the built-in
    /// <see cref="MarkdownPromptRenderer"/> / <see cref="ChatMessageRenderer"/>.
    /// Does not register an <see cref="IPromptRepository"/>; call
    /// <see cref="AddFilePromptRepository(IServiceCollection, string)"/> or supply your own.
    /// </summary>
    public static IServiceCollection AddFluentPromptBuilder(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ITemplateRenderer, DefaultTemplateRenderer>();
        services.AddSingleton<IPromptFactory, PromptFactory>();
        services.AddSingleton<IPromptRenderer<string>, MarkdownPromptRenderer>();
        services.AddSingleton<IPromptRenderer<IReadOnlyList<ChatPromptMessage>>, ChatMessageRenderer>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="FilePromptRepository"/> as the singleton
    /// <see cref="IPromptRepository"/>, rooted at the given directory.
    /// </summary>
    public static IServiceCollection AddFilePromptRepository(
        this IServiceCollection services,
        string rootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentException.ThrowIfNullOrWhiteSpace(rootPath);

        services.AddSingleton<IPromptRepository>(new FilePromptRepository(rootPath));
        return services;
    }
}
