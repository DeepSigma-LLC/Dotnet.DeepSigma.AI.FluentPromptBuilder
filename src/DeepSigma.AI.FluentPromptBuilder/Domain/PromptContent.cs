namespace DeepSigma.AI.FluentPromptBuilder.Domain;

/// <summary>
/// A typed content payload carried by a <see cref="PromptSection"/>. Sealed hierarchy: switch
/// over the concrete types (<see cref="TextContent"/>, <see cref="ImageContent"/>,
/// <see cref="ToolCallContent"/>, <see cref="ToolResultContent"/>) when consuming.
/// New variants may be added in future versions; consumers should handle the unknown case.
/// </summary>
public abstract record PromptContent;

/// <summary>Plain UTF-16 text content. The most common variant.</summary>
/// <param name="Text">The literal text. May contain <c>{{Placeholder}}</c> tokens for templates.</param>
public sealed record TextContent(string Text) : PromptContent;

/// <summary>Raw image bytes with a media type (e.g. <c>image/png</c>, <c>image/jpeg</c>).</summary>
/// <param name="Data">The image bytes.</param>
/// <param name="MediaType">The IANA media type for the image.</param>
public sealed record ImageContent(ReadOnlyMemory<byte> Data, string MediaType) : PromptContent;

/// <summary>An assistant-issued invocation of a named tool.</summary>
/// <param name="ToolCallId">An identifier for the call, used to correlate with a later <see cref="ToolResultContent"/>.</param>
/// <param name="ToolName">The name of the tool being called.</param>
/// <param name="ArgumentsJson">A JSON string carrying the tool arguments.</param>
public sealed record ToolCallContent(string ToolCallId, string ToolName, string ArgumentsJson) : PromptContent;

/// <summary>The output of a previously-invoked tool, returned to the model.</summary>
/// <param name="ToolCallId">The id of the originating <see cref="ToolCallContent"/>.</param>
/// <param name="Output">Nested content produced by the tool (commonly text, possibly images).</param>
/// <param name="IsError">Whether the tool reported an error condition.</param>
public sealed record ToolResultContent(
    string ToolCallId,
    IReadOnlyList<PromptContent> Output,
    bool IsError = false) : PromptContent;
