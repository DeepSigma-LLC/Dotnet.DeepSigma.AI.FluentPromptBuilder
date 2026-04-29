namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// Helpers for converting <see cref="PromptRole"/> to the lowercase string form used by every
/// major chat-completion API (<c>"system"</c>, <c>"user"</c>, <c>"assistant"</c>, <c>"tool"</c>).
/// Centralised so renderers and future provider adapters share one mapping.
/// </summary>
public static class PromptRoleExtensions
{
    /// <summary>Returns the lowercase wire-format string for a role.</summary>
    public static string ToApiString(this PromptRole role) =>
        role switch
        {
            PromptRole.System => "system",
            PromptRole.User => "user",
            PromptRole.Assistant => "assistant",
            PromptRole.Tool => "tool",
            _ => role.ToString().ToLowerInvariant(),
        };
}
