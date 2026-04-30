using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Serialization;

namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// Renders a <see cref="BuiltPrompt"/> as JSON in the round-trip-friendly v1 schema produced by
/// <see cref="BuiltPromptJsonSerializer"/> — preserves <see cref="BuiltPrompt.Source"/>,
/// section names, and section ordering. Useful for caching, audit logs, or transporting a
/// built prompt between processes.
/// </summary>
/// <remarks>
/// Use <see cref="JsonChatPromptRenderer"/> instead when you want a flatter chat-message shape
/// suitable for forwarding to an LLM.
/// </remarks>
public sealed class JsonBuiltPromptRenderer : IPromptRenderer<string>
{
    /// <inheritdoc/>
    public string Render(BuiltPrompt prompt) => BuiltPromptJsonSerializer.Serialize(prompt);
}
