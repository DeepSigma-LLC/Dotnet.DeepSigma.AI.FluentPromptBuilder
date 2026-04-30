using System.Globalization;
using System.Text;
using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// Renders a <see cref="BuiltPrompt"/> to a plain-text string in one of three styles. Plain
/// text by definition cannot embed binary content, so images are rendered as a placeholder
/// line and tool calls/results as compact text representations.
/// </summary>
/// <remarks>
/// Sections whose content is <see cref="TextContent"/> with empty/whitespace text are skipped
/// (per <see cref="PromptSectionExtensions.HasRenderableContent"/>); messages that end up with
/// no renderable sections are omitted entirely.
/// </remarks>
public sealed class PlainTextPromptRenderer : IPromptRenderer<string>
{
    /// <summary>The layout style this renderer produces.</summary>
    public PlainTextStyle Style { get; }

    /// <summary>Defaults to <see cref="PlainTextStyle.ContentOnly"/>.</summary>
    public PlainTextPromptRenderer() : this(PlainTextStyle.ContentOnly) { }

    /// <summary>Constructs a renderer with the given style.</summary>
    public PlainTextPromptRenderer(PlainTextStyle style)
    {
        Style = style;
    }

    /// <inheritdoc/>
    public string Render(BuiltPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var sb = new StringBuilder();
        var messageWritten = false;

        foreach (var message in prompt.Messages)
        {
            var renderable = message.Sections
                .OrderBy(s => s.Order)
                .Where(s => s.HasRenderableContent())
                .ToList();

            if (renderable.Count == 0)
            {
                continue;
            }

            if (messageWritten)
            {
                sb.AppendLine();
            }
            messageWritten = true;

            switch (Style)
            {
                case PlainTextStyle.ContentOnly:
                    AppendContentOnly(sb, renderable);
                    break;
                case PlainTextStyle.Transcript:
                    AppendTranscript(sb, message.Role, renderable);
                    break;
                case PlainTextStyle.Labeled:
                    AppendLabeled(sb, message.Role, renderable);
                    break;
                default:
                    throw new NotSupportedException($"Unknown PlainTextStyle: {Style}");
            }
        }

        return sb.ToString().TrimEnd();
    }

    private static void AppendContentOnly(StringBuilder sb, List<PromptSection> sections)
    {
        for (var i = 0; i < sections.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
            }
            sb.AppendLine(FormatContent(sections[i].Content));
        }
    }

    private static void AppendTranscript(StringBuilder sb, PromptRole role, List<PromptSection> sections)
    {
        sb.Append('[').Append(role.ToString()).Append(']').AppendLine();
        for (var i = 0; i < sections.Count; i++)
        {
            if (i > 0)
            {
                sb.AppendLine();
            }
            sb.AppendLine(FormatContent(sections[i].Content));
        }
    }

    private static void AppendLabeled(StringBuilder sb, PromptRole role, List<PromptSection> sections)
    {
        sb.AppendLine(role.ToString());
        foreach (var section in sections)
        {
            sb.Append("  ").Append(section.Name).AppendLine(":");
            foreach (var line in FormatContent(section.Content).Split('\n'))
            {
                sb.Append("    ").AppendLine(line.TrimEnd('\r'));
            }
        }
    }

    private static string FormatContent(PromptContent content) =>
        content switch
        {
            TextContent t => t.Text,
            ImageContent i => string.Create(CultureInfo.InvariantCulture,
                $"[image: {i.MediaType}, {i.Data.Length} bytes]"),
            ToolCallContent c => string.Create(CultureInfo.InvariantCulture,
                $"[tool_call {c.ToolName}({c.ToolCallId}): {c.ArgumentsJson}]"),
            ToolResultContent r => FormatToolResult(r),
            _ => $"[unsupported content type: {content.GetType().Name}]",
        };

    private static string FormatToolResult(ToolResultContent r)
    {
        var nested = string.Join("\n", r.Output.Select(FormatContent));
        var marker = r.IsError ? " (error)" : "";
        return string.Create(CultureInfo.InvariantCulture,
            $"[tool_result {r.ToolCallId}{marker}]\n{nested}");
    }
}
