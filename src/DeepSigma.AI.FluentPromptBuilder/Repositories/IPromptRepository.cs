using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Repositories;

/// <summary>
/// Loads <see cref="PromptTemplate"/>s by key and version. The library ships a file-system
/// implementation (<see cref="FilePromptRepository"/>); database, Redis, or other backends are
/// straightforward to add in separate packages without touching the core library.
/// </summary>
public interface IPromptRepository
{
    /// <summary>Loads an exact template version, or returns <c>null</c> if it does not exist.</summary>
    Task<PromptTemplate?> GetTemplateAsync(
        PromptKey key,
        PromptVersion version,
        CancellationToken cancellationToken = default);

    /// <summary>Loads the highest-versioned template for a key, or <c>null</c> if no versions exist.</summary>
    Task<PromptTemplate?> GetLatestAsync(
        PromptKey key,
        CancellationToken cancellationToken = default);

    /// <summary>Lists every available version for the given key, in ascending order.</summary>
    Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(
        PromptKey key,
        CancellationToken cancellationToken = default);
}
