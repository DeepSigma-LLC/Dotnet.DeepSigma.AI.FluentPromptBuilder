namespace DeepSigma.AI.FluentPromptBuilder.Exceptions;

/// <summary>
/// Base type for all domain exceptions thrown by DeepSigma.AI.FluentPromptBuilder.
/// Catch this type to handle any prompt-builder failure generically.
/// </summary>
public abstract class PromptException : Exception
{
    /// <summary>Initializes a new instance with no message.</summary>
    protected PromptException() { }

    /// <summary>Initializes a new instance with a message.</summary>
    protected PromptException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a message and inner exception.</summary>
    protected PromptException(string message, Exception inner) : base(message, inner) { }
}
