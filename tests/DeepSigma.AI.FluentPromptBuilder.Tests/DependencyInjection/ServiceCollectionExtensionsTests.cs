using DeepSigma.AI.FluentPromptBuilder.DependencyInjection;
using DeepSigma.AI.FluentPromptBuilder.Rendering;
using DeepSigma.AI.FluentPromptBuilder.Repositories;
using DeepSigma.AI.FluentPromptBuilder.Templates;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace DeepSigma.AI.FluentPromptBuilder.Tests.DependencyInjection;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddFluentPromptBuilder_RegistersAllAdvertisedServices()
    {
        var services = new ServiceCollection();
        services.AddFluentPromptBuilder()
                .AddFilePromptRepository(Path.GetTempPath());

        using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<ITemplateRenderer>());
        Assert.NotNull(provider.GetRequiredService<IPromptFactory>());
        Assert.NotNull(provider.GetRequiredService<IPromptRepository>());
        Assert.NotNull(provider.GetRequiredService<IPromptRenderer<string>>());
        Assert.NotNull(provider.GetRequiredService<IPromptRenderer<IReadOnlyList<ChatPromptMessage>>>());
    }

    [Fact]
    public void AddFluentPromptBuilder_FactoryUsesRegisteredRepository()
    {
        var services = new ServiceCollection();
        services.AddFluentPromptBuilder()
                .AddFilePromptRepository(Path.GetTempPath());

        using var provider = services.BuildServiceProvider();

        var factory = provider.GetRequiredService<IPromptFactory>();
        var prompt = factory.CreateBuilder().System("hi").Build();
        Assert.Single(prompt.Messages);
    }
}
