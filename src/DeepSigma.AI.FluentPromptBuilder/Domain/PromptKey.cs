using System.Buffers;

namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// Identifies a prompt template independently of version.
/// </summary>
/// <remarks>
/// <para>
/// Both <see cref="Namespace"/> and <see cref="Name"/> reject path separators (<c>/</c>,
/// <c>\</c>) and whitespace at construction. Dots and colons are permitted, so hierarchical
/// names such as <c>"team.feature"</c> or <c>"my.company:CodeReview"</c> are valid.
/// </para>
/// <para>
/// Repositories that map keys onto file paths (e.g. <c>FilePromptRepository</c>) are
/// responsible for defending against traversal at the I/O layer — this type guarantees only
/// that path-component separators cannot be smuggled in.
/// </para>
/// </remarks>
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

    // Disallow only path separators and whitespace. Dots and colons are permitted to support
    // hierarchical naming (`team.feature`, `my.company:CodeReview`). Path-traversal defence
    // is the responsibility of repositories that map keys onto file paths.
    private static readonly SearchValues<char> DisallowedChars =
        SearchValues.Create("/\\ \t\n\r");

    private static void Validate(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Prompt key segment must not be null, empty, or whitespace.", paramName);
        }

        if (value.AsSpan().ContainsAny(DisallowedChars))
        {
            throw new ArgumentException(
                $"Prompt key segment '{value}' contains disallowed characters (path separators or whitespace).",
                paramName);
        }
    }
}
