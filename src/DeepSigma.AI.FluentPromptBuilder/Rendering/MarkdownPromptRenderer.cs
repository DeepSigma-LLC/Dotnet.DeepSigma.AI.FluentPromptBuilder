using System.Globalization;
using System.Text;
using DeepSigma.AI.FluentPromptBuilder.Domain;

namespace DeepSigma.AI.FluentPromptBuilder.Rendering;

/// <summary>
/// Renders a <see cref="BuiltPrompt"/> to a single markdown string suitable for human review,
/// logging, or feeding to text-only consumers. Image content above
/// <see cref="LargeImageThreshold"/> is rendered as a placeholder line rather than an inline
/// data URI, to keep rendered output readable for large attachments.
/// </summary>
public sealed class MarkdownPromptRenderer : IPromptRenderer<string>
{
    /// <summary>Default size threshold (in bytes) above which images render as a placeholder.</summary>
    public const int DefaultLargeImageThreshold = 64 * 1024;

    /// <summary>Constructs a renderer with the default large-image threshold.</summary>
    public MarkdownPromptRenderer() : this(DefaultLargeImageThreshold) { }

    /// <summary>Constructs a renderer with a custom large-image threshold.</summary>
    public MarkdownPromptRenderer(int largeImageThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(largeImageThreshold);
        LargeImageThreshold = largeImageThreshold;
    }

    /// <summary>The byte-size threshold above which images are emitted as a placeholder line.</summary>
    public int LargeImageThreshold { get; }

    /// <inheritdoc/>
    public string Render(BuiltPrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        var sb = new StringBuilder();
        foreach (var message in prompt.Messages)
        {
            sb.Append("## ").AppendLine(message.Role.ToString());
            foreach (var section in message.Sections.OrderBy(s => s.Order))
            {
                sb.AppendLine();
                sb.Append("### ").AppendLine(section.Name);
                AppendContent(sb, section.Content);
            }
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    private void AppendContent(StringBuilder sb, PromptContent content)
    {
        switch (content)
        {
            case TextContent t:
                sb.AppendLine(t.Text);
                break;

            case ImageContent i:
                AppendImage(sb, i);
                break;

            case ToolCallContent c:
                sb.AppendLine("```json");
                sb.Append("{ \"tool_call_id\": \"").Append(c.ToolCallId)
                  .Append("\", \"name\": \"").Append(c.ToolName)
                  .Append("\", \"arguments\": ").Append(c.ArgumentsJson).AppendLine(" }");
                sb.AppendLine("```");
                break;

            case ToolResultContent r:
                sb.Append("```").AppendLine(r.IsError ? "tool-error" : "tool-result");
                sb.Append("tool_call_id: ").AppendLine(r.ToolCallId);
                foreach (var nested in r.Output)
                {
                    AppendContent(sb, nested);
                }
                sb.AppendLine("```");
                break;

            default:
                sb.Append("_[unsupported content type: ").Append(content.GetType().Name).AppendLine("]_");
                break;
        }
    }

    private void AppendImage(StringBuilder sb, ImageContent image)
    {
        if (image.Data.Length > LargeImageThreshold)
        {
            sb.Append(CultureInfo.InvariantCulture, $"_[image: {image.MediaType}, {image.Data.Length} bytes]_");
            sb.AppendLine();
            return;
        }

        var base64 = Convert.ToBase64String(image.Data.Span);
        sb.Append(CultureInfo.InvariantCulture, $"![image](data:{image.MediaType};base64,{base64})");
        sb.AppendLine();
    }
}
