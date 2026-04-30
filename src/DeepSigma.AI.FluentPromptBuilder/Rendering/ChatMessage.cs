using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// A provider-neutral chat message produced by <see cref="ChatMessageRenderer"/>. Reuses the
/// existing <see cref="PromptContent"/> hierarchy directly — no parallel "chat block" type
/// hierarchy. Provider adapters consume this shape directly without re-parsing strings.
/// </summary>
/// <param name="Role">The role string in lowercase form (<c>"system"</c>, <c>"user"</c>,
/// <c>"assistant"</c>, <c>"tool"</c>).</param>
/// <param name="Content">The ordered content payload, one entry per renderable section.</param>
public sealed record ChatMessage(string Role, IReadOnlyList<PromptContent> Content);
