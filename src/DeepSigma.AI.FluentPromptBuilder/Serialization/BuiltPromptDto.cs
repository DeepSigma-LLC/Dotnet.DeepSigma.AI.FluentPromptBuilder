using System.Text.Json.Serialization;

namespace DeepSigma.AI.FluentPromptBuilder.Serialization;

// Wire-format DTO for BuiltPrompt round-tripping. Reuses PromptIdDto + PromptMessageDto from
// PromptTemplateDto.cs so the message/content shapes are identical to stored templates.

internal sealed class BuiltPromptDto
{
    [JsonPropertyName("$schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("source")] public PromptIdDto? Source { get; set; }
    [JsonPropertyName("messages")] public List<PromptMessageDto> Messages { get; set; } = new();
}
