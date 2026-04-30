namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>Helpers for inspecting <see cref="PromptSection"/> and <see cref="PromptMessage"/>.</summary>
public static class PromptSectionExtensions
{
    /// <summary>
    /// Returns <c>false</c> when this section's content is <see cref="TextContent"/> with
    /// null, empty, or whitespace-only text (e.g. an unfilled optional template variable);
    /// returns <c>true</c> for every other content type. Used by renderers to skip sections
    /// that have nothing meaningful to emit.
    /// </summary>
    public static bool HasRenderableContent(this PromptSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return section.Content switch
        {
            TextContent t => !string.IsNullOrWhiteSpace(t.Text),
            _ => true,
        };
    }

    /// <summary>
    /// Returns the message's sections sorted by <see cref="PromptSection.Order"/> with empty
    /// (per <see cref="HasRenderableContent"/>) sections filtered out. Renderers use this to
    /// avoid emitting bare role headings or empty content blocks.
    /// </summary>
    public static List<PromptSection> RenderableSections(this PromptMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return message.Sections
            .OrderBy(s => s.Order)
            .Where(HasRenderableContent)
            .ToList();
    }
}
