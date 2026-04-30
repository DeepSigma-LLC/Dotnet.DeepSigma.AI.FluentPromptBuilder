using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// Renders a <see cref="BuiltPrompt"/> into a flat list of <see cref="ChatMessage"/>s. The
/// content payload reuses the domain <see cref="PromptContent"/> hierarchy directly so
/// provider adapters can switch on the content variants without an intermediate type hop.
/// </summary>
/// <remarks>
/// Section names are not emitted (they are metadata, not content). Sections whose text is
/// empty/whitespace are skipped via <see cref="PromptSectionExtensions.RenderableSections"/>.
/// If every section in a message is suppressed, the message itself is omitted.
/// </remarks>
public sealed class ChatMessageRenderer : IPromptRenderer<IReadOnlyList<ChatMessage>>
{
    /// <inheritdoc/>
    public IReadOnlyList<ChatMessage> Render(BuiltPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var output = new List<ChatMessage>(prompt.Messages.Count);
        foreach (var message in prompt.Messages)
        {
            var renderable = message.RenderableSections();
            if (renderable.Count == 0)
            {
                continue;
            }

            var content = renderable.Select(s => s.Content).ToList();
            output.Add(new ChatMessage(message.Role.ToApiString(), content));
        }
        return output;
    }
}
