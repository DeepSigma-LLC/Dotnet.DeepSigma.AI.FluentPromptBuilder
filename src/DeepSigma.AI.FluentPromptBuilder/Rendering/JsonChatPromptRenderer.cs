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
/// Section names are not emitted (they are metadata, not content), and sections whose text is
/// empty/whitespace are dropped per <see cref="PromptSectionExtensions.HasRenderableContent"/>.
/// Provider-specific JSON shapes (OpenAI, Anthropic) are out of scope for this renderer; build
/// them in dedicated adapter packages.
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

        var messages = new List<ChatMessageJsonDto>(prompt.Messages.Count);
        foreach (var message in prompt.Messages)
        {
            var blocks = message.Sections
                .OrderBy(s => s.Order)
                .Where(s => s.HasRenderableContent())
                .Select(s => TemplateMapper.ContentToDto(s.Content))
                .ToList();

            if (blocks.Count == 0)
            {
                continue;
            }

            messages.Add(new ChatMessageJsonDto
            {
                Role = message.Role.ToApiString(),
                Content = blocks,
            });
        }

        return JsonSerializer.Serialize(messages, Indented ? IndentedOptions : CompactOptions);
    }
}

internal sealed class ChatMessageJsonDto
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("content")] public List<PromptContentDto> Content { get; set; } = new();
}
