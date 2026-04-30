using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Serialization;

namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// Renders a <see cref="BuiltPrompt"/> to a JSON string in a provider-neutral chat-message
/// shape. Each message has a lowercase <c>role</c> and a <c>content</c> array of typed blocks
/// using the same tagged-discriminator schema (<c>text</c> / <c>image</c> / <c>tool_call</c> /
/// <c>tool_result</c>) as stored prompt templates.
/// </summary>
/// <remarks>
/// Internally delegates to <see cref="ChatMessageRenderer"/> for the message-shaping pass and
/// then serialises the resulting <see cref="ChatMessage"/> list to JSON. Provider-specific
/// JSON shapes (OpenAI, Anthropic) are out of scope for this renderer.
/// </remarks>
public sealed class JsonChatPromptRenderer : IPromptRenderer<string>
{
    private static readonly JsonSerializerOptions IndentedOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly JsonSerializerOptions CompactOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly ChatMessageRenderer ChatRenderer = new();

    /// <summary>Whether to indent the JSON output (default: <c>true</c>).</summary>
    public bool Indented { get; }

    /// <summary>Constructs a renderer that emits indented JSON.</summary>
    public JsonChatPromptRenderer() : this(indented: true) { }

    /// <summary>Constructs a renderer with explicit indentation control.</summary>
    public JsonChatPromptRenderer(bool indented) => Indented = indented;

    /// <inheritdoc/>
    public string Render(BuiltPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var messages = ChatRenderer.Render(prompt)
            .Select(m => new ChatMessageJsonDto
            {
                Role = m.Role,
                Content = m.Content.Select(TemplateMapper.ContentToDto).ToList(),
            })
            .ToList();

        return JsonSerializer.Serialize(messages, Indented ? IndentedOptions : CompactOptions);
    }
}

internal sealed class ChatMessageJsonDto
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("content")] public List<PromptContentDto> Content { get; set; } = new();
}
