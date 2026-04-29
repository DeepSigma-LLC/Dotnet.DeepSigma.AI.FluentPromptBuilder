using System.Buffers;

namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// Identifies a prompt template independently of version.
/// Both <see cref="Namespace"/> and <see cref="Name"/> are validated to disallow path-component
/// characters (<c>/</c>, <c>\</c>, <c>.</c>, <c>:</c>) and whitespace, so keys are safe to use
/// as path segments in repositories such as <c>FilePromptRepository</c>.
/// </summary>
public sealed record PromptKey
{
    /// <summary>The logical grouping the prompt belongs to (e.g. <c>"CodeReview"</c>).</summary>
    public string Namespace { get; }

    /// <summary>The unique name of the prompt within its namespace (e.g. <c>"SecurityReview"</c>).</summary>
    public string Name { get; }

    /// <summary>Constructs a <see cref="PromptKey"/>, validating both components.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown if either argument is null/whitespace or contains a disallowed character.
    /// </exception>
    public PromptKey(string @namespace, string name)
    {
        Validate(@namespace, nameof(@namespace));
        Validate(name, nameof(name));
        Namespace = @namespace;
        Name = name;
    }

    /// <inheritdoc/>
    public override string ToString() => $"{Namespace}/{Name}";

    private static readonly SearchValues<char> DisallowedChars =
        SearchValues.Create("/\\.: \t\n\r");

    private static void Validate(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Prompt key segment must not be null, empty, or whitespace.", paramName);
        }

        if (value.AsSpan().ContainsAny(DisallowedChars))
        {
            throw new ArgumentException(
                $"Prompt key segment '{value}' contains disallowed characters (/, \\, ., :, whitespace).",
                paramName);
        }
    }
}
