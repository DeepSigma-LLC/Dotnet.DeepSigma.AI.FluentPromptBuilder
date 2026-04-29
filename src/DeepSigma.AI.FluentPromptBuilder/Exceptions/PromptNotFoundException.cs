using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Exceptions;

/// <summary>
/// Thrown by <c>IPromptRepository</c>-backed factories when a requested prompt template
/// (by key and optional version) cannot be located.
/// </summary>
public sealed class PromptNotFoundException : PromptException
{
    /// <summary>The key that could not be located.</summary>
    public PromptKey Key { get; }

    /// <summary>The version that was requested, or <c>null</c> if "latest" was requested.</summary>
    public PromptVersion? Version { get; }

    /// <summary>Initializes the exception for a specific key + version miss.</summary>
    public PromptNotFoundException(PromptKey key, PromptVersion version)
        : base($"Prompt template not found: {key}@{version}.")
    {
        Key = key;
        Version = version;
    }

    /// <summary>Initializes the exception for a "latest" miss against a key.</summary>
    public PromptNotFoundException(PromptKey key)
        : base($"No versions found for prompt template: {key}.")
    {
        Key = key;
        Version = null;
    }
}
