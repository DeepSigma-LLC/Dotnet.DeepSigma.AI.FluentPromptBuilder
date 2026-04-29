using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;

namespace DeepSigma.AI.FluentPromptBuilder.Templates;

/// <summary>
/// Validates that a variable map satisfies the requirements declared by a
/// <see cref="PromptTemplate"/> before the template renderer attempts substitution.
/// </summary>
public static class PromptTemplateValidator
{
    /// <summary>
    /// Throws <see cref="PromptValidationException"/> if any variable declared as required by
    /// <paramref name="template"/> is missing from <paramref name="variables"/> and has no
    /// default value.
    /// </summary>
    public static void Validate(
        PromptTemplate template,
        IReadOnlyDictionary<string, object?> variables)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(variables);

        var missing = new List<string>();
        foreach (var variable in template.Variables)
        {
            if (!variable.Required)
            {
                continue;
            }

            if (variables.ContainsKey(variable.Name))
            {
                continue;
            }

            if (variable.DefaultValue is not null)
            {
                continue;
            }

            missing.Add(variable.Name);
        }

        if (missing.Count > 0)
        {
            throw new PromptValidationException(
                $"Missing required variable(s) for template {template.Id}: {string.Join(", ", missing)}.");
        }
    }
}
