using System.Text.RegularExpressions;
using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Templates;

/// <summary>
/// The built-in template renderer. Substitutes <c>{{Name}}</c> placeholders inside text-bearing
/// content using a single regex pass per string. Supports the <c>{{{{Name}}}}</c> escape, which
/// renders as a literal <c>{{Name}}</c>.
/// Unresolved placeholders (no matching variable in the map and no default) are left in place;
/// <see cref="PromptTemplateValidator"/> is responsible for failing fast on missing required
/// variables before this renderer runs.
/// </summary>
public sealed partial class DefaultTemplateRenderer : ITemplateRenderer
{
    [GeneratedRegex(@"\{\{\s*([A-Za-z_][A-Za-z0-9_]*)\s*\}\}", RegexOptions.CultureInvariant)]
    private static partial Regex PlaceholderRegex();

    // Use Private-Use-Area code points as escape sentinels so they cannot collide with any
    // legitimate text the user might place in a variable value or template body.
    private const string EscOpen = "";
    private const string EscClose = "";

    /// <inheritdoc/>
    public IReadOnlyList<PromptMessage> Render(
        PromptTemplate template,
        IReadOnlyDictionary<string, object?> variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variables);

        var effective = MergeDefaults(template, variables);

        var rendered = new List<PromptMessage>(template.Messages.Count);
        foreach (var message in template.Messages)
        {
            var newSections = new List<PromptSection>(message.Sections.Count);
            foreach (var section in message.Sections)
            {
                newSections.Add(section with { Content = SubstituteContent(section.Content, effective) });
            }
            rendered.Add(new PromptMessage(message.Role, newSections));
        }
        return rendered;
    }

    private static IReadOnlyDictionary<string, object?> MergeDefaults(
        PromptTemplate template,
        IReadOnlyDictionary<string, object?> supplied)
    {
        if (template.Variables.Count == 0)
        {
            return supplied;
        }

        var merged = new Dictionary<string, object?>(supplied, StringComparer.Ordinal);
        foreach (var variable in template.Variables)
        {
            if (!merged.ContainsKey(variable.Name) && variable.DefaultValue is not null)
            {
                merged[variable.Name] = variable.DefaultValue;
            }
        }
        return merged;
    }

    private static PromptContent SubstituteContent(
        PromptContent content,
        IReadOnlyDictionary<string, object?> variables) =>
        content switch
        {
            TextContent t => new TextContent(Substitute(t.Text, variables)),
            ToolCallContent c => c with { ArgumentsJson = Substitute(c.ArgumentsJson, variables) },
            ToolResultContent r => r with
            {
                Output = r.Output.Select(o => SubstituteContent(o, variables)).ToList(),
            },
            _ => content,
        };

    internal static string Substitute(
        string input,
        IReadOnlyDictionary<string, object?> variables)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var protectedInput = input
            .Replace("{{{{", EscOpen, StringComparison.Ordinal)
            .Replace("}}}}", EscClose, StringComparison.Ordinal);

        var substituted = PlaceholderRegex().Replace(protectedInput, match =>
        {
            var name = match.Groups[1].Value;
            return variables.TryGetValue(name, out var value)
                ? value?.ToString() ?? string.Empty
                : match.Value;
        });

        return substituted
            .Replace(EscOpen, "{{", StringComparison.Ordinal)
            .Replace(EscClose, "}}", StringComparison.Ordinal);
    }
}
