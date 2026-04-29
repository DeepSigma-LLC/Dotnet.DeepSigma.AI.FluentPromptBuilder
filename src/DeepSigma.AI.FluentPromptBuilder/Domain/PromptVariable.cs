namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// Declares a variable that a <see cref="PromptTemplate"/> expects to receive at render time.
/// Used by validators to fail fast when required variables are missing.
/// </summary>
/// <param name="Name">The variable name (matches <c>{{Name}}</c> placeholders in templates).</param>
/// <param name="Required">When <c>true</c>, the renderer fails if the variable is not supplied.</param>
/// <param name="Description">Optional human-readable description for documentation tooling.</param>
/// <param name="DefaultValue">Optional default applied when the variable is not supplied.</param>
public sealed record PromptVariable(
    string Name,
    bool Required = true,
    string? Description = null,
    string? DefaultValue = null);
