using System.Text.Json;
using System.Text.Json.Serialization;

namespace Kofflard.Core.Rules;

/// <summary>
/// The model's answer as it arrives, before validation: the JSON of <see cref="DiscoveryPrompt.Schema"/>.
/// </summary>
public sealed record DiscoveryAnswer(
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("artist")] DiscoveryAnswer.Person? Artist,
    [property: JsonPropertyName("suggestions")] IReadOnlyList<DiscoveryAnswer.Pick>? Suggestions,
    [property: JsonPropertyName("candidates")] IReadOnlyList<DiscoveryAnswer.Person>? Candidates)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>A musician: the one searched for, or a candidate.</summary>
    public sealed record Person(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("summary")] string? Summary);

    /// <summary>One suggestion as the model wrote it.</summary>
    public sealed record Pick(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("lane")] string? Lane,
        [property: JsonPropertyName("why")] string? Why,
        [property: JsonPropertyName("bridge")] string? Bridge,
        [property: JsonPropertyName("tags")] IReadOnlyList<string>? Tags,
        [property: JsonPropertyName("knownFor")] string? KnownFor);

    /// <summary>
    /// Reads the model's text. Tolerates a Markdown code fence around the JSON, which models that
    /// ignore the response format tend to add.
    /// </summary>
    /// <exception cref="DiscoveryAnswerException">The text is not the expected JSON.</exception>
    public static DiscoveryAnswer Parse(string? text)
    {
        var json = StripFence(text);
        if (json.Length == 0)
        {
            throw new DiscoveryAnswerException("The answer was empty.");
        }

        DiscoveryAnswer? answer;
        try
        {
            answer = JsonSerializer.Deserialize<DiscoveryAnswer>(json, Options);
        }
        catch (JsonException e)
        {
            throw new DiscoveryAnswerException($"The answer was not valid JSON ({e.Message}).");
        }

        if (answer is null)
        {
            throw new DiscoveryAnswerException("The answer was JSON null.");
        }

        if (answer.Status is not ("ok" or "ambiguous" or "unknown"))
        {
            throw new DiscoveryAnswerException($"\"status\" must be \"ok\", \"ambiguous\" or \"unknown\", not \"{answer.Status}\".");
        }

        return answer;
    }

    private static string StripFence(string? text)
    {
        var trimmed = text?.Trim() ?? string.Empty;
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineEnd = trimmed.IndexOf('\n');
        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        return firstLineEnd >= 0 && lastFence > firstLineEnd
            ? trimmed[(firstLineEnd + 1)..lastFence].Trim()
            : trimmed.Trim('`').Trim();
    }
}

/// <summary>The model's answer could not be used; the message says why, in words the model can act on.</summary>
public sealed class DiscoveryAnswerException(string message) : Exception(message);
