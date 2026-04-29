using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;

namespace DeepSigma.AI.FluentPromptBuilder.Building;

/// <summary>
/// Builds the section list for a single role-tagged message. Constructed only by
/// <see cref="PromptBuilder"/> via the <c>System</c>/<c>User</c>/<c>Assistant</c>/<c>Message</c>
/// configure-action overloads.
/// </summary>
public sealed class PromptMessageBuilder
{
    private readonly PromptRole _role;
    private readonly List<PromptSection> _sections = new();
    private int _nextOrder;

    internal PromptMessageBuilder(PromptRole role)
    {
        _role = role;
    }

    /// <summary>Appends a text section. The most common case.</summary>
    public PromptMessageBuilder Section(string name, string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        return Section(name, new TextContent(text));
    }

    /// <summary>Appends a section with arbitrary typed content (escape hatch).</summary>
    public PromptMessageBuilder Section(string name, PromptContent content)
    {
        ArgumentNullException.ThrowIfNull(content);
        ValidateName(name);
        _sections.Add(new PromptSection(name, content, _nextOrder++));
        return this;
    }

    /// <summary>Appends an image section.</summary>
    public PromptMessageBuilder ImageSection(string name, ReadOnlyMemory<byte> data, string mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType))
        {
            throw new PromptValidationException("Image media type must not be null, empty, or whitespace.");
        }
        return Section(name, new ImageContent(data, mediaType));
    }

    /// <summary>Appends a tool-call section.</summary>
    public PromptMessageBuilder ToolCallSection(
        string name,
        string toolCallId,
        string toolName,
        string argumentsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolCallId);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);
        ArgumentNullException.ThrowIfNull(argumentsJson);
        return Section(name, new ToolCallContent(toolCallId, toolName, argumentsJson));
    }

    /// <summary>Appends a tool-result section. The output may be heterogeneous content.</summary>
    public PromptMessageBuilder ToolResultSection(
        string name,
        string toolCallId,
        IReadOnlyList<PromptContent> output,
        bool isError = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolCallId);
        ArgumentNullException.ThrowIfNull(output);
        return Section(name, new ToolResultContent(toolCallId, output, isError));
    }

    internal PromptMessage Build()
    {
        if (_sections.Count == 0)
        {
            throw new PromptValidationException("Message must contain at least one section.");
        }
        return new PromptMessage(_role, _sections.ToList());
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new PromptValidationException("Section name must not be null, empty, or whitespace.");
        }
    }
}
