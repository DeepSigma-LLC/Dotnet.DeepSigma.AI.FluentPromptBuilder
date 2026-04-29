using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// Converts a <see cref="BuiltPrompt"/> into a target representation. Implementations cover
/// markdown strings, structured chat messages, and provider-specific shapes.
/// </summary>
/// <typeparam name="TOutput">The target output type produced by the renderer.</typeparam>
public interface IPromptRenderer<out TOutput>
{
    /// <summary>Renders the supplied built prompt into <typeparamref name="TOutput"/>.</summary>
    TOutput Render(BuiltPrompt prompt);
}
