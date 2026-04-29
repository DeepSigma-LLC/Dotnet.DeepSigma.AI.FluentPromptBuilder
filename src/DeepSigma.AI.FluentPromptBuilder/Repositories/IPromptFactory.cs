using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Repositories;

/// <summary>
/// Convenience entry point for repository-backed prompt construction. Resolves a template from
/// the configured <see cref="IPromptRepository"/>, runs the builder pipeline, and returns the
/// resulting <see cref="BuiltPrompt"/>.
/// </summary>
public interface IPromptFactory
{
    /// <summary>Builds a prompt from an exact template version.</summary>
    Task<BuiltPrompt> BuildFromTemplateAsync(
        PromptKey key,
        PromptVersion version,
        object? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>Builds a prompt from the latest available version of a template.</summary>
    Task<BuiltPrompt> BuildLatestAsync(
        PromptKey key,
        object? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>Creates a fresh <see cref="PromptBuilder"/> for manual construction.</summary>
    PromptBuilder CreateBuilder();
}
