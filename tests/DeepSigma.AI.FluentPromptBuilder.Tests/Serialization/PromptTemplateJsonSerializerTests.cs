using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Serialization;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Serialization;

public class PromptTemplateJsonSerializerTests
{
    private static PromptTemplate SampleTemplate(byte[]? imageBytes = null) =>
        new(
            new VersionedPromptKey(new PromptKey("CodeReview", "SecurityReview"), new PromptVersion(2, 1, 0)),
            [
                new PromptMessage(PromptRole.System,
                    [new PromptSection("Role", new TextContent("You are a senior application security engineer."))]),
                new PromptMessage(PromptRole.User,
                [
                    new PromptSection("Task", new TextContent("Review the following {{Language}} code for security issues."), 0),
                    new PromptSection("Code", new TextContent("{{Code}}"), 1),
                    new PromptSection("Logo", new ImageContent(imageBytes ?? [1, 2, 3], "image/png"), 2),
                ]),
                new PromptMessage(PromptRole.Assistant,
                    [new PromptSection("Call", new ToolCallContent("call_1", "lookup", "{\"id\":42}"))]),
                new PromptMessage(PromptRole.Tool,
                    [new PromptSection("Result",
                        new ToolResultContent("call_1", [new TextContent("found")]))]),
            ],
            [
                new PromptVariable("Language", Required: true),
                new PromptVariable("Code", Required: true),
            ],
            new PromptMetadata(
                Description: "Security-focused code review prompt.",
                Owner: "Platform",
                Tags: ["code-review", "security"]));

    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var original = SampleTemplate();
        var json = PromptTemplateJsonSerializer.Serialize(original);
        var deserialized = PromptTemplateJsonSerializer.Deserialize(json);

        Assert.Equal(original.Id, deserialized.Id);
        Assert.Equal(original.Variables.Count, deserialized.Variables.Count);
        Assert.Equal(original.Metadata.Description, deserialized.Metadata.Description);
        Assert.Equal(original.Metadata.Owner, deserialized.Metadata.Owner);
        Assert.Equal(original.Metadata.Tags, deserialized.Metadata.Tags);
        Assert.Equal(original.Metadata.Deprecated, deserialized.Metadata.Deprecated);
        Assert.Equal(original.Messages.Count, deserialized.Messages.Count);

        // Walk content variants explicitly
        var systemSection = deserialized.Messages[0].Sections[0];
        Assert.Equal("You are a senior application security engineer.",
            Assert.IsType<TextContent>(systemSection.Content).Text);

        var imageSection = deserialized.Messages[1].Sections[2];
        var image = Assert.IsType<ImageContent>(imageSection.Content);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.Data.ToArray());

        var callSection = deserialized.Messages[2].Sections[0];
        var call = Assert.IsType<ToolCallContent>(callSection.Content);
        Assert.Equal("call_1", call.ToolCallId);
        Assert.Equal("lookup", call.ToolName);

        var resultSection = deserialized.Messages[3].Sections[0];
        var result = Assert.IsType<ToolResultContent>(resultSection.Content);
        Assert.Equal("found", Assert.IsType<TextContent>(result.Output[0]).Text);
    }

    [Fact]
    public void Serialize_EmitsSchemaVersion()
    {
        var json = PromptTemplateJsonSerializer.Serialize(SampleTemplate());
        Assert.Contains("\"$schemaVersion\": 1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_UnknownSchemaVersion_Throws()
    {
        const string json = """
            {
              "$schemaVersion": 999,
              "id": { "key": { "namespace": "A", "name": "B" }, "version": { "major": 1, "minor": 0, "patch": 0 } },
              "messages": []
            }
            """;
        var ex = Assert.Throws<PromptSerializationException>(() => PromptTemplateJsonSerializer.Deserialize(json));
        Assert.Contains("schemaVersion", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_InvalidPromptKey_Throws()
    {
        const string json = """
            {
              "$schemaVersion": 1,
              "id": { "key": { "namespace": "../etc", "name": "B" }, "version": { "major": 1, "minor": 0, "patch": 0 } },
              "messages": []
            }
            """;
        Assert.Throws<PromptSerializationException>(() => PromptTemplateJsonSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_UnknownContentType_Throws()
    {
        const string json = """
            {
              "$schemaVersion": 1,
              "id": { "key": { "namespace": "A", "name": "B" }, "version": { "major": 1, "minor": 0, "patch": 0 } },
              "messages": [
                { "role": "User", "sections": [
                  { "name": "X", "order": 0, "content": { "type": "video", "url": "..." } }
                ] }
              ]
            }
            """;
        Assert.Throws<PromptSerializationException>(() => PromptTemplateJsonSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_MalformedJson_Throws()
    {
        Assert.Throws<PromptSerializationException>(() => PromptTemplateJsonSerializer.Deserialize("{ not json"));
    }
}
