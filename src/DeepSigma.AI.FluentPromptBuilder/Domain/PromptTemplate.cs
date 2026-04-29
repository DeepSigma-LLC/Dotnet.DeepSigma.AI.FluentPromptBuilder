namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// A reusable, versioned prompt definition. Templates carry placeholders (e.g. <c>{{Code}}</c>)
/// that are substituted at render time using a variable map.
/// </summary>
/// <param name="Id">The versioned identity of this template.</param>
/// <param name="Messages">The ordered messages that make up the template body.</param>
/// <param name="Variables">The set of variables this template expects.</param>
/// <param name="Metadata">Optional documentation/audit metadata.</param>
public sealed record PromptTemplate(
    VersionedPromptKey Id,
    IReadOnlyList<PromptMessage> Messages,
    IReadOnlyList<PromptVariable> Variables,
    PromptMetadata Metadata);
