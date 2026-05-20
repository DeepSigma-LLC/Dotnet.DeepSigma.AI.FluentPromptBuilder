using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;

namespace DeepSigma.AI.FluentPromptBuilder.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="PromptTemplate"/> instances to and from the v1 JSON
/// wire format. Content polymorphism uses a tagged <c>type</c> discriminator
/// (<c>text</c> / <c>image</c> / <c>tool_call</c> / <c>tool_result</c>) so future content
/// variants are additive without a schema bump.
/// </summary>
public static class PromptTemplateJsonSerializer
{
    /// <summary>The current wire-format schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        // Storage backends like PostgreSQL `jsonb` do not preserve key order, so the polymorphic
        // `type` discriminator may not appear first on read. .NET 9+ supports out-of-order
        // discriminators via this flag; without it, `jsonb`-round-tripped JSON fails to deserialize.
        AllowOutOfOrderMetadataProperties = true,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serializes a template to the v1 JSON format.</summary>
    public static string Serialize(PromptTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var dto = TemplateMapper.ToDto(template);
        return JsonSerializer.Serialize(dto, Options);
    }

    /// <summary>Deserializes a template from the v1 JSON format.</summary>
    /// <exception cref="PromptSerializationException">
    /// Thrown if the JSON is malformed, the schema version is unknown, or required fields are missing.
    /// </exception>
    public static PromptTemplate Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        PromptTemplateDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<PromptTemplateDto>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new PromptSerializationException("Failed to parse prompt template JSON.", ex);
        }

        if (dto is null)
        {
            throw new PromptSerializationException("Prompt template JSON deserialized to null.");
        }

        if (dto.SchemaVersion != CurrentSchemaVersion)
        {
            throw new PromptSerializationException(
                $"Unsupported prompt template $schemaVersion: {dto.SchemaVersion}. Expected {CurrentSchemaVersion}.");
        }

        return TemplateMapper.FromDto(dto);
    }
}
