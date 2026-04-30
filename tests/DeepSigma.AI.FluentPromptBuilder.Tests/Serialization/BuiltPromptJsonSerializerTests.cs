using DeepSigma.AI.FluentPromptBuilder.Building;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Rendering;
using DeepSigma.AI.FluentPromptBuilder.Serialization;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Serialization;

public class BuiltPromptJsonSerializerTests
{
    private static BuiltPrompt SamplePrompt(VersionedPromptKey? source = null)
    {
        var template = new PromptTemplate(
            source ?? new VersionedPromptKey(new PromptKey("CodeReview", "SecurityReview"), new PromptVersion(2)),
            [
                new PromptMessage(PromptRole.System,
                    [new PromptSection("Role", new TextContent("You are helpful."))]),
                new PromptMessage(PromptRole.User,
                [
                    new PromptSection("Task", new TextContent("Review this."), 0),
                    new PromptSection("Photo", new ImageContent(new byte[] { 1, 2, 3 }, "image/png"), 1),
                ]),
                new PromptMessage(PromptRole.Assistant,
                    [new PromptSection("Call", new ToolCallContent("c1", "lookup", "{\"id\":42}"))]),
                new PromptMessage(PromptRole.Tool,
                    [new PromptSection("Result", new ToolResultContent("c1", [new TextContent("found")]))]),
            ],
            [],
            new PromptMetadata());

        return PromptBuilder.Create().UseTemplate(template).Build();
    }

    [Fact]
    public void RoundTrip_PreservesEverything()
    {
        var original = SamplePrompt();
        var json = BuiltPromptJsonSerializer.Serialize(original);
        var deserialized = BuiltPromptJsonSerializer.Deserialize(json);

        Assert.Equal(original.Source, deserialized.Source);
        Assert.Equal(original.Messages.Count, deserialized.Messages.Count);

        var image = Assert.IsType<ImageContent>(deserialized.Messages[1].Sections[1].Content);
        Assert.Equal("image/png", image.MediaType);
        Assert.Equal(new byte[] { 1, 2, 3 }, image.Data.ToArray());

        var call = Assert.IsType<ToolCallContent>(deserialized.Messages[2].Sections[0].Content);
        Assert.Equal("c1", call.ToolCallId);

        var result = Assert.IsType<ToolResultContent>(deserialized.Messages[3].Sections[0].Content);
        Assert.Equal("found", Assert.IsType<TextContent>(result.Output[0]).Text);
    }

    [Fact]
    public void Serialize_EmitsSchemaVersion()
    {
        var json = BuiltPromptJsonSerializer.Serialize(SamplePrompt());
        Assert.Contains("\"$schemaVersion\": 1", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_PreservesSectionNames()
    {
        // Unlike JsonChatPromptRenderer, this round-trip-friendly format keeps section names
        // so the BuiltPrompt can be reconstructed verbatim.
        var json = BuiltPromptJsonSerializer.Serialize(SamplePrompt());
        Assert.Contains("\"name\": \"Role\"", json, StringComparison.Ordinal);
        Assert.Contains("\"name\": \"Task\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Serialize_ManualPrompt_ProducesNullSource()
    {
        var manual = PromptBuilder.Create().System("hi").Build();
        var json = BuiltPromptJsonSerializer.Serialize(manual);

        var deserialized = BuiltPromptJsonSerializer.Deserialize(json);
        Assert.Null(deserialized.Source);
    }

    [Fact]
    public void Deserialize_UnknownSchemaVersion_Throws()
    {
        const string json = """
            { "$schemaVersion": 999, "messages": [] }
            """;
        var ex = Assert.Throws<PromptSerializationException>(() => BuiltPromptJsonSerializer.Deserialize(json));
        Assert.Contains("schemaVersion", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Deserialize_MalformedJson_Throws()
    {
        Assert.Throws<PromptSerializationException>(() => BuiltPromptJsonSerializer.Deserialize("{ broken"));
    }

    [Fact]
    public void JsonBuiltPromptRenderer_DelegatesToSerializer()
    {
        var prompt = SamplePrompt();
        var rendererOutput = new JsonBuiltPromptRenderer().Render(prompt);
        var serializerOutput = BuiltPromptJsonSerializer.Serialize(prompt);
        Assert.Equal(serializerOutput, rendererOutput);
    }
}
