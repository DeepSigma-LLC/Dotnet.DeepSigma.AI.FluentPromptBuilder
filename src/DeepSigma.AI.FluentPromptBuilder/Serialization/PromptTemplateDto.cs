using System.Text.Json.Serialization;

namespace DeepSigma.AI.FluentPromptBuilder.Serialization;

// Internal wire-format DTOs. Keep separate from domain records so JSON concerns don't leak
// into the domain layer.

internal sealed class PromptTemplateDto
{
    [JsonPropertyName("$schemaVersion")] public int SchemaVersion { get; set; }
    [JsonPropertyName("id")] public PromptIdDto Id { get; set; } = default!;
    [JsonPropertyName("messages")] public List<PromptMessageDto> Messages { get; set; } = new();
    [JsonPropertyName("variables")] public List<PromptVariableDto>? Variables { get; set; }
    [JsonPropertyName("metadata")] public PromptMetadataDto? Metadata { get; set; }
}

internal sealed class PromptIdDto
{
    [JsonPropertyName("key")] public PromptKeyDto Key { get; set; } = default!;
    [JsonPropertyName("version")] public PromptVersionDto Version { get; set; } = default!;
}

internal sealed class PromptKeyDto
{
    [JsonPropertyName("namespace")] public string Namespace { get; set; } = "";
    [JsonPropertyName("name")] public string Name { get; set; } = "";
}

internal sealed class PromptVersionDto
{
    [JsonPropertyName("major")] public int Major { get; set; }
    [JsonPropertyName("minor")] public int Minor { get; set; }
    [JsonPropertyName("patch")] public int Patch { get; set; }
}

internal sealed class PromptMessageDto
{
    [JsonPropertyName("role")] public string Role { get; set; } = "";
    [JsonPropertyName("sections")] public List<PromptSectionDto> Sections { get; set; } = new();
}

internal sealed class PromptSectionDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("content")] public PromptContentDto Content { get; set; } = default!;
    [JsonPropertyName("order")] public int Order { get; set; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TextContentDto),       "text")]
[JsonDerivedType(typeof(ImageContentDto),      "image")]
[JsonDerivedType(typeof(ToolCallContentDto),   "tool_call")]
[JsonDerivedType(typeof(ToolResultContentDto), "tool_result")]
internal abstract class PromptContentDto;

internal sealed class TextContentDto : PromptContentDto
{
    [JsonPropertyName("text")] public string Text { get; set; } = "";
}

internal sealed class ImageContentDto : PromptContentDto
{
    [JsonPropertyName("mediaType")] public string MediaType { get; set; } = "";
    /// <summary>Base64-encoded image bytes.</summary>
    [JsonPropertyName("data")] public string Data { get; set; } = "";
}

internal sealed class ToolCallContentDto : PromptContentDto
{
    [JsonPropertyName("toolCallId")] public string ToolCallId { get; set; } = "";
    [JsonPropertyName("toolName")] public string ToolName { get; set; } = "";
    [JsonPropertyName("argumentsJson")] public string ArgumentsJson { get; set; } = "";
}

internal sealed class ToolResultContentDto : PromptContentDto
{
    [JsonPropertyName("toolCallId")] public string ToolCallId { get; set; } = "";
    [JsonPropertyName("isError")] public bool IsError { get; set; }
    [JsonPropertyName("output")] public List<PromptContentDto> Output { get; set; } = new();
}

internal sealed class PromptVariableDto
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("required")] public bool Required { get; set; } = true;
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("defaultValue")] public string? DefaultValue { get; set; }
}

internal sealed class PromptMetadataDto
{
    [JsonPropertyName("description")] public string? Description { get; set; }
    [JsonPropertyName("owner")] public string? Owner { get; set; }
    [JsonPropertyName("tags")] public List<string>? Tags { get; set; }
    [JsonPropertyName("deprecated")] public bool Deprecated { get; set; }
}
