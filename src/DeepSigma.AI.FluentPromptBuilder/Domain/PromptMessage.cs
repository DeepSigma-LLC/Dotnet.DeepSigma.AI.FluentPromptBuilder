namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// A single role-tagged message composed of one or more <see cref="PromptSection"/>s. Messages
/// are kept segregated by role through the build pipeline so that downstream renderers can
/// emit provider-native shapes without re-parsing strings.
/// </summary>
/// <param name="Role">The role of the speaker for this message.</param>
/// <param name="Sections">The ordered sections that make up this message.</param>
public sealed record PromptMessage(PromptRole Role, IReadOnlyList<PromptSection> Sections);
