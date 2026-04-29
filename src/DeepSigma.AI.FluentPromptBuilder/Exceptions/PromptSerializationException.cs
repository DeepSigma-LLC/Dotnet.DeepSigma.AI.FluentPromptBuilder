namespace DeepSigma.AI.FluentPromptBuilder.Exceptions;

/// <summary>
/// Thrown when a stored prompt template fails to deserialize: unknown <c>$schemaVersion</c>,
/// malformed JSON, missing required fields, or unrecognised content type discriminators.
/// </summary>
public sealed class PromptSerializationException : PromptException
{
    /// <summary>Initializes a new instance with a serialization failure message.</summary>
    public PromptSerializationException(string message) : base(message) { }

    /// <summary>Initializes a new instance with a message and inner cause.</summary>
    public PromptSerializationException(string message, Exception inner) : base(message, inner) { }
}
