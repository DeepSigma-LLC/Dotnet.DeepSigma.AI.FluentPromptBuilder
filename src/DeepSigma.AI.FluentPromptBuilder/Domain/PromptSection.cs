namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// A named, ordered unit of prompt content within a <see cref="PromptMessage"/>. Sections are
/// preserved as discrete units through building and rendering — they are not flattened into a
/// single string until a renderer chooses to do so.
/// </summary>
/// <param name="Name">A human-readable label (e.g. <c>"Role"</c>, <c>"Task"</c>, <c>"Code"</c>).</param>
/// <param name="Content">The typed content carried by this section.</param>
/// <param name="Order">
/// Sort order within the message; lower values render first. When two sections share the same
/// <see cref="Order"/>, ties are broken by their position in the source list (LINQ stable sort);
/// in practice this matches the order in which sections were appended to the
/// <c>PromptMessageBuilder</c>. <c>PromptMessageBuilder</c> auto-increments this value, so
/// duplicate orders only arise when constructing <see cref="PromptSection"/> directly.
/// </param>
public sealed record PromptSection(string Name, PromptContent Content, int Order = 0);
