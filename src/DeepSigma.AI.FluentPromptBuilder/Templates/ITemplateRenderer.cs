using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Templates;

/// <summary>
/// Renders a <see cref="PromptTemplate"/> to its concrete <see cref="PromptMessage"/> list by
/// substituting variables into <see cref="TextContent"/> (and other text-bearing content
/// variants such as <see cref="ToolCallContent.ArgumentsJson"/>).
/// Replace the default implementation to introduce a richer template engine
/// (loops, conditionals, filters) without touching <c>PromptBuilder</c> or <c>PromptFactory</c>.
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// Substitutes <paramref name="variables"/> into a copy of the template's messages and
    /// returns the result. Does not mutate the input template.
    /// </summary>
    IReadOnlyList<PromptMessage> Render(
        PromptTemplate template,
        IReadOnlyDictionary<string, object?> variables);
}
