using System.Text.Json;
using System.Text.Json.Serialization;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;

namespace DeepSigma.AI.FluentPromptBuilder.Serialization;

/// <summary>
/// Serializes and deserializes <see cref="BuiltPrompt"/> instances to and from a JSON wire
/// format that mirrors the v1 stored-template schema (same content polymorphism, same
/// <c>$schemaVersion</c> guard). Useful for caching, logging, or transporting a built prompt
/// without re-building it.
/// </summary>
public static class BuiltPromptJsonSerializer
{
    /// <summary>The current wire-format schema version.</summary>
    public const int CurrentSchemaVersion = 1;

    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Serializes a built prompt to JSON.</summary>
    public static string Serialize(BuiltPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        return JsonSerializer.Serialize(ToDto(prompt), Options);
    }

    /// <summary>Deserializes a built prompt from JSON.</summary>
    /// <exception cref="PromptSerializationException">
    /// Thrown if the JSON is malformed, the schema version is unknown, or required fields are missing.
    /// </exception>
    public static BuiltPrompt Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        BuiltPromptDto? dto;
        try
        {
            dto = JsonSerializer.Deserialize<BuiltPromptDto>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new PromptSerializationException("Failed to parse built-prompt JSON.", ex);
        }

        if (dto is null)
        {
            throw new PromptSerializationException("Built-prompt JSON deserialized to null.");
        }

        if (dto.SchemaVersion != CurrentSchemaVersion)
        {
            throw new PromptSerializationException(
                $"Unsupported built-prompt $schemaVersion: {dto.SchemaVersion}. Expected {CurrentSchemaVersion}.");
        }

        return FromDto(dto);
    }

    private static BuiltPromptDto ToDto(BuiltPrompt prompt) =>
        new()
        {
            SchemaVersion = CurrentSchemaVersion,
            Source = prompt.Source is null ? null : SourceToDto(prompt.Source),
            Messages = prompt.Messages.Select(TemplateMapper.MessageToDto).ToList(),
        };

    private static BuiltPrompt FromDto(BuiltPromptDto dto)
    {
        VersionedPromptKey? source = null;
        if (dto.Source is not null)
        {
            if (dto.Source.Key is null || dto.Source.Version is null)
            {
                throw new PromptSerializationException("Built-prompt source must include both key and version when present.");
            }

            PromptKey key;
            try
            {
                key = new PromptKey(dto.Source.Key.Namespace, dto.Source.Key.Name);
            }
            catch (ArgumentException ex)
            {
                throw new PromptSerializationException($"Invalid prompt source key: {ex.Message}", ex);
            }

            source = new VersionedPromptKey(
                key,
                new PromptVersion(dto.Source.Version.Major, dto.Source.Version.Minor, dto.Source.Version.Patch));
        }

        return new BuiltPrompt(source, dto.Messages.Select(TemplateMapper.MessageFromDto).ToList());
    }

    private static PromptIdDto SourceToDto(VersionedPromptKey source) =>
        new()
        {
            Key = new PromptKeyDto { Namespace = source.Key.Namespace, Name = source.Key.Name },
            Version = new PromptVersionDto
            {
                Major = source.Version.Major,
                Minor = source.Version.Minor,
                Patch = source.Version.Patch,
            },
        };
}
