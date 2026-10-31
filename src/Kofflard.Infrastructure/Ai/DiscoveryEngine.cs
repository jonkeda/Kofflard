using System.ClientModel;
using System.Text.Json;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Microsoft.Extensions.AI;

namespace Kofflard.Infrastructure.Ai;

/// <summary>
/// Asks the connected AI for musicians: the prompt, structured output where the provider takes it, one
/// retry on an unusable answer, then validation (design §7).
/// </summary>
/// <remarks>
/// Some models refuse a temperature, and some providers a JSON schema. A 400 that names either is
/// retried once without it, so a model the catalogue does not know still works.
/// </remarks>
public sealed class DiscoveryEngine(ChatClientFactory clients) : IDiscoveryEngine
{
    private static readonly JsonElement Schema = JsonDocument.Parse(DiscoveryPrompt.Schema).RootElement.Clone();

    /// <inheritdoc />
    public async Task<DiscoveryResult> DiscoverAsync(
        AiConnection connection,
        string? secret,
        string artist,
        Adventure adventure,
        IReadOnlyCollection<string> exclude,
        CancellationToken cancellationToken = default)
    {
        using var client = clients.Create(connection, secret);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, connection.Provider.StructuredOutput == AiStructuredOutput.JsonSchema
                ? DiscoveryPrompt.System
                : DiscoveryPrompt.System + "\n\nThe JSON schema of your answer:\n" + DiscoveryPrompt.Schema),
            new(ChatRole.User, DiscoveryPrompt.User(artist, adventure, exclude)),
        };

        var options = new ChatOptions
        {
            Temperature = DiscoveryPrompt.Temperature(adventure),
            MaxOutputTokens = 4000,
            ResponseFormat = connection.Provider.StructuredOutput == AiStructuredOutput.JsonSchema
                ? ChatResponseFormat.ForJsonSchema(Schema, DiscoveryPrompt.SchemaName)
                : ChatResponseFormat.Json,
        };

        var text = await AskAsync(client, connection, messages, options, cancellationToken);
        try
        {
            return SuggestionValidator.Validate(DiscoveryAnswer.Parse(text), artist, adventure, exclude);
        }
        catch (DiscoveryAnswerException problem)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant, text));
            messages.Add(new ChatMessage(ChatRole.User, DiscoveryPrompt.Retry(problem.Message)));
        }

        var retried = await AskAsync(client, connection, messages, options, cancellationToken);
        try
        {
            return SuggestionValidator.Validate(DiscoveryAnswer.Parse(retried), artist, adventure, exclude);
        }
        catch (DiscoveryAnswerException problem)
        {
            throw new AiException(
                AiFailure.BadAnswer,
                $"The AI gave an answer Kofflard can't use ({problem.Message}). Try again, or pick another model.",
                problem);
        }
    }

    /// <inheritdoc />
    public async Task TestAsync(AiConnection connection, string? secret, CancellationToken cancellationToken = default)
    {
        using var client = clients.Create(connection, secret);
        // Room for a reasoning model's thinking before its "OK": with a tight cap it answers with nothing.
        var options = new ChatOptions { MaxOutputTokens = 1024 };
        var text = await AskAsync(client, connection, [new ChatMessage(ChatRole.User, "Reply with OK.")], options, cancellationToken);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new AiException(AiFailure.BadAnswer, $"{connection.Provider.Name} answered, but with nothing. Check the {ModelWord(connection)}.");
        }
    }

    private static async Task<string> AskAsync(
        IChatClient client,
        AiConnection connection,
        IList<ChatMessage> messages,
        ChatOptions options,
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                var response = await client.GetResponseAsync(messages, options, cancellationToken);
                return response.Text;
            }
            catch (ClientResultException failure) when (attempt < 2 && failure.Status == 400 && Drop(options, Body(failure)))
            {
                // Asked again without what the model refused.
            }
            catch (ClientResultException failure)
            {
                throw Translate(failure, connection);
            }
            catch (HttpRequestException failure)
            {
                throw new AiException(AiFailure.Unreachable, $"{connection.Provider.Name} could not be reached. Check the connection and the endpoint.", failure);
            }
            catch (TaskCanceledException failure) when (!cancellationToken.IsCancellationRequested)
            {
                throw new AiException(AiFailure.Unavailable, $"{connection.Provider.Name} took too long to answer. Try again.", failure);
            }
        }
    }

    // Removes the option a 400 complains about; false when it complains about something else.
    private static bool Drop(ChatOptions options, string body)
    {
        if (options.Temperature is not null && body.Contains("temperature", StringComparison.OrdinalIgnoreCase))
        {
            options.Temperature = null;
            return true;
        }

        if (options.ResponseFormat is ChatResponseFormatJson { Schema: not null } && body.Contains("response_format", StringComparison.OrdinalIgnoreCase))
        {
            options.ResponseFormat = ChatResponseFormat.Json;
            return true;
        }

        if (options.ResponseFormat is not null && body.Contains("response_format", StringComparison.OrdinalIgnoreCase))
        {
            options.ResponseFormat = null;
            return true;
        }

        return false;
    }

    private static AiException Translate(ClientResultException failure, AiConnection connection)
    {
        var name = connection.Provider.Name;
        return failure.Status switch
        {
            401 or 403 => new AiException(AiFailure.InvalidKey, $"This key was not accepted by {name}.", failure),
            402 => new AiException(AiFailure.NoCredit, $"Your {name} account has no credit left.", failure),
            404 => new AiException(AiFailure.ModelNotFound, $"{name} does not know the {ModelWord(connection)} \"{connection.Model}\".", failure),
            429 => Body(failure).Contains("quota", StringComparison.OrdinalIgnoreCase)
                ? new AiException(AiFailure.NoCredit, $"Your {name} account is out of quota.", failure)
                : new AiException(AiFailure.RateLimited, $"{name} is busy. Wait a moment and try again.", failure),
            0 => new AiException(AiFailure.Unreachable, $"{name} could not be reached. Check the connection and the endpoint.", failure),
            >= 500 => new AiException(AiFailure.Unavailable, $"{name} had a problem on its side. Try again.", failure),
            _ => new AiException(AiFailure.Unavailable, $"{name} refused the request ({failure.Status}).", failure),
        };
    }

    private static string ModelWord(AiConnection connection) => connection.ProviderId == AiProviders.Azure.Id ? "deployment" : "model";

    private static string Body(ClientResultException failure)
    {
        try
        {
            return failure.GetRawResponse()?.Content?.ToString() ?? failure.Message;
        }
        catch (InvalidOperationException)
        {
            return failure.Message;
        }
    }
}
