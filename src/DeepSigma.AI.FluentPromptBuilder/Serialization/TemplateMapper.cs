using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;

namespace DeepSigma.AI.FluentPromptBuilder.Serialization;

internal static class TemplateMapper
{
    public static PromptTemplateDto ToDto(PromptTemplate template) =>
        new()
        {
            SchemaVersion = PromptTemplateJsonSerializer.CurrentSchemaVersion,
            Id = new PromptIdDto
            {
                Key = new PromptKeyDto { Namespace = template.Id.Key.Namespace, Name = template.Id.Key.Name },
                Version = new PromptVersionDto
                {
                    Major = template.Id.Version.Major,
                    Minor = template.Id.Version.Minor,
                    Patch = template.Id.Version.Patch,
                },
            },
            Messages = template.Messages.Select(MessageToDto).ToList(),
            Variables = template.Variables.Select(VariableToDto).ToList(),
            Metadata = MetadataToDto(template.Metadata),
        };

    public static PromptTemplate FromDto(PromptTemplateDto dto)
    {
        if (dto.Id is null || dto.Id.Key is null || dto.Id.Version is null)
        {
            throw new PromptSerializationException("Prompt template id (key + version) is required.");
        }

        PromptKey key;
        try
        {
            key = new PromptKey(dto.Id.Key.Namespace, dto.Id.Key.Name);
        }
        catch (ArgumentException ex)
        {
            throw new PromptSerializationException($"Invalid prompt key: {ex.Message}", ex);
        }

        var version = new PromptVersion(dto.Id.Version.Major, dto.Id.Version.Minor, dto.Id.Version.Patch);

        return new PromptTemplate(
            new VersionedPromptKey(key, version),
            dto.Messages.Select(MessageFromDto).ToList(),
            (dto.Variables ?? new()).Select(VariableFromDto).ToList(),
            MetadataFromDto(dto.Metadata));
    }

    internal static PromptMessageDto MessageToDto(PromptMessage m) =>
        new()
        {
            Role = m.Role.ToString(),
            Sections = m.Sections.Select(SectionToDto).ToList(),
        };

    internal static PromptMessage MessageFromDto(PromptMessageDto dto)
    {
        if (!Enum.TryParse<PromptRole>(dto.Role, ignoreCase: false, out var role))
        {
            throw new PromptSerializationException($"Unknown prompt role: '{dto.Role}'.");
        }

        return new PromptMessage(role, dto.Sections.Select(SectionFromDto).ToList());
    }

    private static PromptSectionDto SectionToDto(PromptSection s) =>
        new()
        {
            Name = s.Name,
            Content = ContentToDto(s.Content),
            Order = s.Order,
        };

    private static PromptSection SectionFromDto(PromptSectionDto dto) =>
        new(dto.Name, ContentFromDto(dto.Content), dto.Order);

    internal static PromptContentDto ContentToDto(PromptContent content) =>
        content switch
        {
            TextContent t => new TextContentDto { Text = t.Text },
            ImageContent i => new ImageContentDto
            {
                MediaType = i.MediaType,
                Data = Convert.ToBase64String(i.Data.Span),
            },
            ToolCallContent c => new ToolCallContentDto
            {
                ToolCallId = c.ToolCallId,
                ToolName = c.ToolName,
                ArgumentsJson = c.ArgumentsJson,
            },
            ToolResultContent r => new ToolResultContentDto
            {
                ToolCallId = r.ToolCallId,
                IsError = r.IsError,
                Output = r.Output.Select(ContentToDto).ToList(),
            },
            _ => throw new PromptSerializationException(
                $"Cannot serialize unsupported content type: {content.GetType().FullName}"),
        };

    internal static PromptContent ContentFromDto(PromptContentDto dto) =>
        dto switch
        {
            TextContentDto t => new TextContent(t.Text),
            ImageContentDto i => new ImageContent(Convert.FromBase64String(i.Data), i.MediaType),
            ToolCallContentDto c => new ToolCallContent(c.ToolCallId, c.ToolName, c.ArgumentsJson),
            ToolResultContentDto r => new ToolResultContent(
                r.ToolCallId,
                r.Output.Select(ContentFromDto).ToList(),
                r.IsError),
            null => throw new PromptSerializationException("Section content is required."),
            _ => throw new PromptSerializationException(
                $"Unknown content discriminator (DTO type: {dto.GetType().Name})."),
        };

    private static PromptVariableDto VariableToDto(PromptVariable v) =>
        new()
        {
            Name = v.Name,
            Required = v.Required,
            Description = v.Description,
            DefaultValue = v.DefaultValue,
        };

    private static PromptVariable VariableFromDto(PromptVariableDto dto) =>
        new(dto.Name, dto.Required, dto.Description, dto.DefaultValue);

    private static PromptMetadataDto MetadataToDto(PromptMetadata m) =>
        new()
        {
            Description = m.Description,
            Owner = m.Owner,
            Tags = m.Tags?.ToList(),
            Deprecated = m.Deprecated,
        };

    private static PromptMetadata MetadataFromDto(PromptMetadataDto? dto)
    {
        if (dto is null)
        {
            return new PromptMetadata();
        }
        return new PromptMetadata(dto.Description, dto.Owner, dto.Tags, dto.Deprecated);
    }
}
