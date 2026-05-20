namespace DeepSigma.AI.FluentPromptBuilder.Exceptions;

/// <summary>
/// Thrown when a write operation conflicts with the prompt store's immutability rules:
/// inserting a duplicate <c>(namespace, name, version)</c>, attempting to modify content
/// on a non-<c>Draft</c> row, or making a non-forward status transition.
/// </summary>
public sealed class PromptWriteConflictException : PromptException
{
    /// <summary>Initializes a new instance with the conflict message.</summary>
    public PromptWriteConflictException(string message) : base(message) { }

    /// <summary>Initializes a new instance with the conflict message and an inner cause.</summary>
    public PromptWriteConflictException(string message, Exception inner) : base(message, inner) { }
}
