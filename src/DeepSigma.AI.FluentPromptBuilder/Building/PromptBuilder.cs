using System.Reflection;
using DeepSigma.AI.FluentPromptBuilder.Domain;
using DeepSigma.AI.FluentPromptBuilder.Exceptions;
using DeepSigma.AI.FluentPromptBuilder.Templates;

namespace DeepSigma.AI.FluentPromptBuilder.Building;

/// <summary>
/// The fluent entry point for building a <see cref="BuiltPrompt"/>. A builder instance is
/// stateful and intended for single-use construction; it is <b>not thread-safe</b>.
/// Create a fresh builder per prompt with <see cref="Create()"/> or
/// <c>IPromptFactory.CreateBuilder()</c>.
/// </summary>
public sealed class PromptBuilder
{
    private readonly List<PromptMessage> _messages = new();
    private readonly ITemplateRenderer _templateRenderer;
    private VersionedPromptKey? _source;

    private PromptBuilder(ITemplateRenderer templateRenderer)
    {
        _templateRenderer = templateRenderer;
    }

    /// <summary>Creates a builder backed by the default template renderer.</summary>
    public static PromptBuilder Create() => new(new DefaultTemplateRenderer());

    /// <summary>Creates a builder backed by a caller-supplied template renderer.</summary>
    public static PromptBuilder Create(ITemplateRenderer templateRenderer)
    {
        ArgumentNullException.ThrowIfNull(templateRenderer);
        return new PromptBuilder(templateRenderer);
    }

    /// <summary>Appends a system message containing a single text section named <c>"System"</c>.</summary>
    public PromptBuilder System(string content) => SimpleMessage(PromptRole.System, "System", content);

    /// <summary>Appends a user message containing a single text section named <c>"User"</c>.</summary>
    public PromptBuilder User(string content) => SimpleMessage(PromptRole.User, "User", content);

    /// <summary>Appends an assistant message containing a single text section named <c>"Assistant"</c>.</summary>
    public PromptBuilder Assistant(string content) => SimpleMessage(PromptRole.Assistant, "Assistant", content);

    /// <summary>Appends a system message composed from a configure action.</summary>
    public PromptBuilder System(Action<PromptMessageBuilder> configure) => Message(PromptRole.System, configure);

    /// <summary>Appends a user message composed from a configure action.</summary>
    public PromptBuilder User(Action<PromptMessageBuilder> configure) => Message(PromptRole.User, configure);

    /// <summary>Appends an assistant message composed from a configure action.</summary>
    public PromptBuilder Assistant(Action<PromptMessageBuilder> configure) => Message(PromptRole.Assistant, configure);

    /// <summary>Appends a tool-result message composed from a configure action.</summary>
    public PromptBuilder Tool(Action<PromptMessageBuilder> configure) => Message(PromptRole.Tool, configure);

    /// <summary>Appends a message with the given role, composed from a configure action.</summary>
    public PromptBuilder Message(PromptRole role, Action<PromptMessageBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var messageBuilder = new PromptMessageBuilder(role);
        configure(messageBuilder);
        _messages.Add(messageBuilder.Build());
        return this;
    }

    /// <summary>
    /// Renders <paramref name="template"/> with the supplied variables and appends the resulting
    /// messages to this builder. The template's identity is recorded as the prompt's
    /// <see cref="BuiltPrompt.Source"/>.
    /// </summary>
    /// <param name="template">The template to render.</param>
    /// <param name="variables">
    /// An anonymous object whose properties become the variable map (e.g.
    /// <c>new { Code = "...", Language = "C#" }</c>), or any
    /// <see cref="IReadOnlyDictionary{TKey,TValue}"/>/<see cref="IDictionary{TKey,TValue}"/>
    /// of <c>string</c> to <c>object?</c>. Pass <c>null</c> when the template has no variables.
    /// </param>
    public PromptBuilder UseTemplate(PromptTemplate template, object? variables = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        var variableMap = ToVariableDictionary(variables);
        PromptTemplateValidator.Validate(template, variableMap);

        var renderedMessages = _templateRenderer.Render(template, variableMap);

        _messages.AddRange(renderedMessages);
        _source = template.Id;
        return this;
    }

    /// <summary>Finalises the builder and returns the resulting <see cref="BuiltPrompt"/>.</summary>
    /// <exception cref="PromptValidationException">
    /// Thrown if the builder has no messages.
    /// </exception>
    public BuiltPrompt Build()
    {
        if (_messages.Count == 0)
        {
            throw new PromptValidationException("Prompt must contain at least one message.");
        }
        return new BuiltPrompt(_source, _messages.ToList());
    }

    private PromptBuilder SimpleMessage(PromptRole role, string sectionName, string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        var messageBuilder = new PromptMessageBuilder(role);
        messageBuilder.Section(sectionName, content);
        _messages.Add(messageBuilder.Build());
        return this;
    }

    private static IReadOnlyDictionary<string, object?> ToVariableDictionary(object? variables)
    {
        if (variables is null)
        {
            return new Dictionary<string, object?>(StringComparer.Ordinal);
        }

        if (variables is IReadOnlyDictionary<string, object?> ro)
        {
            return ro;
        }

        if (variables is IDictionary<string, object?> rw)
        {
            return new Dictionary<string, object?>(rw, StringComparer.Ordinal);
        }

        // Reflect over public instance properties of an anonymous (or POCO) object.
        var dict = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var prop in variables.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (!prop.CanRead || prop.GetIndexParameters().Length > 0)
            {
                continue;
            }
            dict[prop.Name] = prop.GetValue(variables);
        }
        return dict;
    }
}
