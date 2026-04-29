using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.Repositories;

public class PromptFactoryTests
{
    private sealed class StubRepository : IPromptRepository
    {
        private readonly Dictionary<(PromptKey, PromptVersion), PromptTemplate> _templates = new();

        public StubRepository Add(PromptTemplate template)
        {
            _templates[(template.Id.Key, template.Id.Version)] = template;
            return this;
        }

        public Task<PromptTemplate?> GetTemplateAsync(PromptKey key, PromptVersion version, CancellationToken ct = default) =>
            Task.FromResult(_templates.TryGetValue((key, version), out var t) ? t : null);

        public Task<PromptTemplate?> GetLatestAsync(PromptKey key, CancellationToken ct = default)
        {
            var match = _templates
                .Where(kv => kv.Key.Item1 == key)
                .OrderByDescending(kv => kv.Key.Item2)
                .Select(kv => (PromptTemplate?)kv.Value)
                .FirstOrDefault();
            return Task.FromResult(match);
        }

        public Task<IReadOnlyList<PromptVersion>> GetVersionsAsync(PromptKey key, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<PromptVersion>>(
                _templates.Keys.Where(k => k.Item1 == key).Select(k => k.Item2).Order().ToList());
    }

    private static PromptTemplate Greeting(int major) =>
        new(
            new VersionedPromptKey(new PromptKey("Common", "Greeting"), new PromptVersion(major)),
            [new PromptMessage(PromptRole.User, [new PromptSection("Greet", new TextContent("Hi {{Name}} (v" + major + ")"))])],
            [new PromptVariable("Name")],
            new PromptMetadata());

    [Fact]
    public async Task BuildFromTemplateAsync_ResolvesAndRenders()
    {
        var repo = new StubRepository().Add(Greeting(1));
        var factory = new PromptFactory(repo);

        var prompt = await factory.BuildFromTemplateAsync(
            new PromptKey("Common", "Greeting"),
            new PromptVersion(1),
            new { Name = "world" });

        Assert.Equal("Hi world (v1)", Assert.IsType<TextContent>(prompt.Messages[0].Sections[0].Content).Text);
    }

    [Fact]
    public async Task BuildFromTemplateAsync_Missing_ThrowsNotFound()
    {
        var factory = new PromptFactory(new StubRepository());

        await Assert.ThrowsAsync<PromptNotFoundException>(() =>
            factory.BuildFromTemplateAsync(new PromptKey("X", "Y"), new PromptVersion(1)));
    }

    [Fact]
    public async Task BuildLatestAsync_PicksHighestVersion()
    {
        var repo = new StubRepository().Add(Greeting(1)).Add(Greeting(3)).Add(Greeting(2));
        var factory = new PromptFactory(repo);

        var prompt = await factory.BuildLatestAsync(
            new PromptKey("Common", "Greeting"),
            new { Name = "world" });

        Assert.Equal("Hi world (v3)", Assert.IsType<TextContent>(prompt.Messages[0].Sections[0].Content).Text);
    }

    [Fact]
    public void CreateBuilder_ReturnsUsableFreshBuilder()
    {
        var factory = new PromptFactory(new StubRepository());
        var prompt = factory.CreateBuilder().System("hi").Build();
        Assert.Single(prompt.Messages);
    }
}
