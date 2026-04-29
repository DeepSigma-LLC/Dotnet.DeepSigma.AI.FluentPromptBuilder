namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// The output of <c>PromptBuilder.Build()</c> — a fully-resolved set of role-tagged messages
/// ready to be handed to a renderer or provider adapter.
/// </summary>
/// <param name="Source">The versioned template the prompt was built from, or <c>null</c> for
/// purely manual prompts.</param>
/// <param name="Messages">The resolved messages, with all template variables substituted.</param>
/// <param name="Variables">The variable map that was applied.</param>
public sealed record BuiltPrompt(
    VersionedPromptKey? Source,
    IReadOnlyList<PromptMessage> Messages,
    IReadOnlyDictionary<string, object?> Variables);
