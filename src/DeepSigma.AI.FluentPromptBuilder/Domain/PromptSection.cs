namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// A named, ordered unit of prompt content within a <see cref="PromptMessage"/>. Sections are
/// preserved as discrete units through building and rendering — they are not flattened into a
/// single string until a renderer chooses to do so.
/// </summary>
/// <param name="Name">A human-readable label (e.g. <c>"Role"</c>, <c>"Task"</c>, <c>"Code"</c>).</param>
/// <param name="Content">The typed content carried by this section.</param>
/// <param name="Order">Sort order within the message; lower values render first.</param>
public sealed record PromptSection(string Name, PromptContent Content, int Order = 0);
