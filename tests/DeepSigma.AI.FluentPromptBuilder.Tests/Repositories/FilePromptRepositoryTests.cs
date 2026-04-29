using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using DeepSigma.AI.FluentPromptBuilder.Serialization;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Repositories;

public class FilePromptRepositoryTests : IDisposable
{
    private readonly string _root;
    private readonly FilePromptRepository _repo;

    public FilePromptRepositoryTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "fpb-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
        _repo = new FilePromptRepository(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    private void WriteTemplate(PromptKey key, PromptVersion version)
    {
        var template = new PromptTemplate(
            new VersionedPromptKey(key, version),
            [new PromptMessage(PromptRole.System,
                [new PromptSection("Role", new TextContent($"v{version}"))])],
            [],
            new PromptMetadata());

        var dir = Path.Combine(_root, key.Namespace, key.Name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, $"{version}.prompt.json"),
            PromptTemplateJsonSerializer.Serialize(template));
    }

    [Fact]
    public async Task GetTemplateAsync_ExistingFile_ReturnsParsedTemplate()
    {
        var key = new PromptKey("Common", "Greeting");
        WriteTemplate(key, new PromptVersion(1, 0, 0));

        var template = await _repo.GetTemplateAsync(key, new PromptVersion(1, 0, 0));
        Assert.NotNull(template);
        Assert.Equal(key, template!.Id.Key);
    }

    [Fact]
    public async Task GetTemplateAsync_MissingFile_ReturnsNull()
    {
        var key = new PromptKey("Missing", "Nothing");
        var result = await _repo.GetTemplateAsync(key, new PromptVersion(1));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetVersionsAsync_ReturnsSortedAscending()
    {
        var key = new PromptKey("Common", "Greeting");
        WriteTemplate(key, new PromptVersion(2, 0, 0));
        WriteTemplate(key, new PromptVersion(1, 0, 0));
        WriteTemplate(key, new PromptVersion(1, 5, 3));

        var versions = await _repo.GetVersionsAsync(key);
        Assert.Equal(
            new[] { new PromptVersion(1, 0, 0), new PromptVersion(1, 5, 3), new PromptVersion(2, 0, 0) },
            versions);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsHighestVersion()
    {
        var key = new PromptKey("Common", "Greeting");
        WriteTemplate(key, new PromptVersion(1));
        WriteTemplate(key, new PromptVersion(2));
        WriteTemplate(key, new PromptVersion(1, 5, 0));

        var latest = await _repo.GetLatestAsync(key);
        Assert.NotNull(latest);
        Assert.Equal(new PromptVersion(2), latest!.Id.Version);
    }

    [Fact]
    public async Task GetLatestAsync_NoFiles_ReturnsNull()
    {
        var result = await _repo.GetLatestAsync(new PromptKey("Empty", "Nothing"));
        Assert.Null(result);
    }

    [Fact]
    public async Task GetTemplateAsync_BadSchemaVersion_Throws()
    {
        var key = new PromptKey("Bad", "Schema");
        var dir = Path.Combine(_root, key.Namespace, key.Name);
        Directory.CreateDirectory(dir);
        File.WriteAllText(
            Path.Combine(dir, "1.0.0.prompt.json"),
            """{ "$schemaVersion": 999, "id": { "key": { "namespace": "Bad", "name": "Schema" }, "version": { "major": 1, "minor": 0, "patch": 0 } }, "messages": [] }""");

        await Assert.ThrowsAsync<PromptSerializationException>(() =>
            _repo.GetTemplateAsync(key, new PromptVersion(1, 0, 0)));
    }

    [Fact]
    public async Task GetTemplateAsync_RejectsPathTraversalAtIO()
    {
        // PromptKey now permits "." and ".." at construction (so hierarchical names like
        // "team.feature" work). FilePromptRepository must defend itself: a key whose
        // canonicalised path resolves outside the configured root throws.
        var traversal = new PromptKey("..", "evil");
        await Assert.ThrowsAsync<PromptValidationException>(() =>
            _repo.GetTemplateAsync(traversal, new PromptVersion(1)));
    }
}
