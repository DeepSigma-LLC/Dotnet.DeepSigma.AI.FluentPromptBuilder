namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// The provider-neutral role of a prompt message. Maps onto the roles used by major chat APIs.
/// </summary>
public enum PromptRole
{
    /// <summary>System-level instruction message (sets behavior, persona, constraints).</summary>
    System,

    /// <summary>End-user message.</summary>
    User,

    /// <summary>Assistant / model reply message, including tool-call invocations.</summary>
    Assistant,

    /// <summary>Tool-result message returning the output of an invoked tool to the model.</summary>
    Tool,
}
