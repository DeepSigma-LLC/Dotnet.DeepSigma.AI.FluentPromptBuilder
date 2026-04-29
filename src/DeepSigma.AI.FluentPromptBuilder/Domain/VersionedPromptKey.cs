namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// Combines a <see cref="PromptKey"/> with a specific <see cref="PromptVersion"/>. Used as the
/// stable identifier for an exact prompt template revision.
/// </summary>
/// <param name="Key">The prompt's logical identity.</param>
/// <param name="Version">The exact version of the template.</param>
public sealed record VersionedPromptKey(PromptKey Key, PromptVersion Version)
{
    /// <inheritdoc/>
    public override string ToString() => $"{Key}@{Version}";
}
