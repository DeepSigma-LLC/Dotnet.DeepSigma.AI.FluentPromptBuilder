namespace DeepSigma.AI.FluentPromptBuilder.Postgres;

/// <summary>
/// Lifecycle status of a stored prompt template. Numeric values are stable and match the
/// <c>status_id</c> column in the <c>prompt_template_statuses</c> lookup table — do not
/// renumber existing entries.
/// </summary>
public enum PromptStatus : short
{
    /// <summary>Authored but not yet ready for production use.</summary>
    Draft = 1,

    /// <summary>Available for production use; the default for <c>GetLatestAsync</c>.</summary>
    Published = 2,

    /// <summary>Discouraged for new use but still callable for backwards compatibility.</summary>
    Deprecated = 3,

    /// <summary>Retired; should not be used.</summary>
    Archived = 4,
}
