namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// A provider-neutral chat message produced by <c>ChatMessageRenderer</c>. The message content
/// is itself a list of typed blocks (<see cref="ChatContentBlock"/>) so the structure round-trips
/// to multimodal provider APIs without re-parsing strings.
/// </summary>
/// <param name="Role">The role string in lowercase form (<c>"system"</c>, <c>"user"</c>,
/// <c>"assistant"</c>, <c>"tool"</c>).</param>
/// <param name="Content">The ordered content blocks.</param>
public sealed record ChatPromptMessage(string Role, IReadOnlyList<ChatContentBlock> Content);

/// <summary>
/// A typed block inside a <see cref="ChatPromptMessage"/>. Sealed hierarchy: switch on the
/// concrete subtypes when consuming.
/// </summary>
public abstract record ChatContentBlock;

/// <summary>A plain-text block.</summary>
/// <param name="Text">The text payload.</param>
public sealed record ChatTextBlock(string Text) : ChatContentBlock;

/// <summary>An image block.</summary>
/// <param name="Data">The raw image bytes.</param>
/// <param name="MediaType">The IANA media type (e.g. <c>image/png</c>).</param>
public sealed record ChatImageBlock(ReadOnlyMemory<byte> Data, string MediaType) : ChatContentBlock;

/// <summary>An assistant tool-call block.</summary>
/// <param name="ToolCallId">Identifier correlating this call with a later tool result.</param>
/// <param name="ToolName">Name of the tool being invoked.</param>
/// <param name="ArgumentsJson">JSON-encoded arguments.</param>
public sealed record ChatToolCallBlock(string ToolCallId, string ToolName, string ArgumentsJson) : ChatContentBlock;

/// <summary>A tool-result block.</summary>
/// <param name="ToolCallId">The id of the originating call.</param>
/// <param name="Output">The nested content blocks produced by the tool.</param>
/// <param name="IsError">Whether the tool reported an error.</param>
public sealed record ChatToolResultBlock(
    string ToolCallId,
    IReadOnlyList<ChatContentBlock> Output,
    bool IsError) : ChatContentBlock;
