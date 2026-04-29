namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>Helpers for inspecting <see cref="PromptSection"/> instances.</summary>
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
}
