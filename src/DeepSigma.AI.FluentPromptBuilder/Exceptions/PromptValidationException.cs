namespace DeepSigma.AI.FluentPromptBuilder.Exceptions;

/// <summary>
/// Thrown when domain rules are violated: missing required variables, empty messages,
/// invalid section structure, etc.
/// </summary>
public sealed class PromptValidationException : PromptException
{
    /// <summary>Initializes a new instance with the validation failure message.</summary>
    public PromptValidationException(string message) : base(message) { }

    /// <summary>Initializes a new instance with the validation failure message and an inner cause.</summary>
    public PromptValidationException(string message, Exception inner) : base(message, inner) { }
}
