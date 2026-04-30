namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// Selects the layout produced by <see cref="PlainTextPromptRenderer"/>.
/// </summary>
public enum PlainTextStyle
{
    /// <summary>
    /// Just the section content, blank-line separated. No role or section labels.
    /// Best for feeding a single-string completion API.
    /// </summary>
    ContentOnly,

    /// <summary>
    /// Each message is prefixed with <c>[Role]</c>; sections are concatenated underneath.
    /// Best for human-readable transcripts and logs.
    /// </summary>
    Transcript,

    /// <summary>
    /// Hierarchical layout: role on its own line, each section labelled
    /// <c>  Name:</c> with the content following on subsequent lines.
    /// Best when section names carry meaning that should be preserved.
    /// </summary>
    Labeled,
}
