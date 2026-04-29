namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// Optional metadata about a <see cref="PromptTemplate"/>: ownership, search tags, deprecation
/// status. Not used by the rendering pipeline; intended for documentation, search, and audit
/// tooling.
/// </summary>
/// <param name="Description">Human-readable description.</param>
/// <param name="Owner">The team or individual responsible for the prompt.</param>
/// <param name="Tags">Free-form tags for grouping or search.</param>
/// <param name="Deprecated">When <c>true</c>, signals that the template should not be used in new code.</param>
public sealed record PromptMetadata(
    string? Description = null,
    string? Owner = null,
    IReadOnlyList<string>? Tags = null,
    bool Deprecated = false);
