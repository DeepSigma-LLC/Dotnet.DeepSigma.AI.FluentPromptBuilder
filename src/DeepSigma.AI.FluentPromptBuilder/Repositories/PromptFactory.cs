using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Templates;

namespace DeepSigma.AI.FluentPromptBuilder.Repositories;

/// <summary>
/// Default <see cref="IPromptFactory"/> implementation. Composes an <see cref="IPromptRepository"/>
/// with an <see cref="ITemplateRenderer"/> so different storage backends and template engines can
/// be swapped independently.
/// </summary>
public sealed class PromptFactory : IPromptFactory
{
    private readonly IPromptRepository _repository;
    private readonly ITemplateRenderer _templateRenderer;

    /// <summary>Constructs a factory using the default template renderer.</summary>
    public PromptFactory(IPromptRepository repository) : this(repository, new DefaultTemplateRenderer()) { }

    /// <summary>Constructs a factory using a caller-supplied template renderer.</summary>
    public PromptFactory(IPromptRepository repository, ITemplateRenderer templateRenderer)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(templateRenderer);
        _repository = repository;
        _templateRenderer = templateRenderer;
    }

    /// <inheritdoc/>
    public PromptBuilder CreateBuilder() => PromptBuilder.Create(_templateRenderer);

    /// <inheritdoc/>
    public async Task<BuiltPrompt> BuildFromTemplateAsync(
        PromptKey key,
        PromptVersion version,
        object? variables = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var template = await _repository.GetTemplateAsync(key, version, cancellationToken).ConfigureAwait(false)
            ?? throw new PromptNotFoundException(key, version);

        return PromptBuilder.Create(_templateRenderer)
            .UseTemplate(template, variables)
            .Build();
    }

    /// <inheritdoc/>
    public async Task<BuiltPrompt> BuildLatestAsync(
        PromptKey key,
        object? variables = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);

        var template = await _repository.GetLatestAsync(key, cancellationToken).ConfigureAwait(false)
            ?? throw new PromptNotFoundException(key);

        return PromptBuilder.Create(_templateRenderer)
            .UseTemplate(template, variables)
            .Build();
    }
}
