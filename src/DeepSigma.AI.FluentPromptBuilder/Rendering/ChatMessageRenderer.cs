using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// Renders a <see cref="BuiltPrompt"/> into a list of <see cref="ChatPromptMessage"/>s whose
/// content is itself structured into typed <see cref="ChatContentBlock"/>s. Provider adapters
/// consume this shape directly without re-parsing strings.
/// </summary>
public sealed class ChatMessageRenderer : IPromptRenderer<IReadOnlyList<ChatPromptMessage>>
{
    /// <inheritdoc/>
    /// <remarks>
    /// Sections whose content is <see cref="TextContent"/> with null/empty/whitespace text are
    /// skipped. If every section in a message is suppressed this way, the message itself is
    /// omitted from the output.
    /// </remarks>
    public IReadOnlyList<ChatPromptMessage> Render(BuiltPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var output = new List<ChatPromptMessage>(prompt.Messages.Count);
        foreach (var message in prompt.Messages)
        {
            var renderable = message.Sections
                .OrderBy(s => s.Order)
                .Where(s => s.HasRenderableContent())
                .ToList();

            if (renderable.Count == 0)
            {
                continue;
            }

            var blocks = new List<ChatContentBlock>(renderable.Count * 2);
            foreach (var section in renderable)
            {
                blocks.Add(new ChatTextBlock("# " + section.Name));
                blocks.Add(MapContent(section.Content));
            }
            output.Add(new ChatPromptMessage(message.Role.ToApiString(), blocks));
        }
        return output;
    }

    private static ChatContentBlock MapContent(PromptContent content) =>
        content switch
        {
            TextContent t => new ChatTextBlock(t.Text),
            ImageContent i => new ChatImageBlock(i.Data, i.MediaType),
            ToolCallContent c => new ChatToolCallBlock(c.ToolCallId, c.ToolName, c.ArgumentsJson),
            ToolResultContent r => new ChatToolResultBlock(
                r.ToolCallId,
                r.Output.Select(MapContent).ToList(),
                r.IsError),
            _ => throw new NotSupportedException(
                $"Unsupported PromptContent type: {content.GetType().FullName}"),
        };
}
