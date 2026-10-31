using System.ClientModel;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Microsoft.Extensions.AI;

namespace Kofflard.Infrastructure.Ai;

/// <summary>
/// Asks the connected AI for musicians: the prompt, structured output where the provider takes it, the
/// answer streamed so cards show as they are written, one retry on an unusable answer, then validation
/// (design §7, .my/design/streaming-answers.md).
/// </summary>
/// <remarks>
/// Some models refuse a temperature or a reasoning effort, and some providers a JSON schema or streaming.
/// A 400 that names one is retried without it, so a model the catalogue does not know still works.
/// </remarks>
public sealed class DiscoveryEngine(ChatClientFactory clients) : IDiscoveryEngine
{
    private static readonly JsonElement Schema = JsonDocument.Parse(DiscoveryPrompt.Schema).RootElement.Clone();

    private static readonly ConcurrentDictionary<string, bool> Unstreamable = new();

    /// <inheritdoc />
    public async IAsyncEnumerable<DiscoveryUpdate> DiscoverAsync(
        AiConnection connection,
        string? secret,
        string artist,
        IReadOnlyCollection<string> exclude,
        LaneMix? mix = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        mix ??= LaneMix.Even;
        using var client = clients.Create(connection, secret);
        var messages = new List<ChatMessage>
        {
            new(ChatRole.System, connection.Provider.StructuredOutput == AiStructuredOutput.JsonSchema
                ? DiscoveryPrompt.System
                : DiscoveryPrompt.System + "\n\nThe JSON schema of your answer:\n" + DiscoveryPrompt.Schema),
            new(ChatRole.User, DiscoveryPrompt.User(artist, exclude, mix)),
        };

        var options = new ChatOptions
        {
            Temperature = DiscoveryPrompt.Temperature,
            MaxOutputTokens = 4000,

            // A reasoning model thinks before it writes the first card; twelve musicians with a line each
            // need little of it (.my/design/streaming-answers.md). Models that don't reason refuse it, and
            // are asked again without.
            Reasoning = new ReasoningOptions { Effort = ReasoningEffort.Low },
            ResponseFormat = connection.Provider.StructuredOutput == AiStructuredOutput.JsonSchema
                ? ChatResponseFormat.ForJsonSchema(Schema, DiscoveryPrompt.SchemaName)
                : ChatResponseFormat.Json,
        };

        string? streamed = null;
        if (!Unstreamable.ContainsKey(StreamKey(connection)))
        {
            var answer = new StreamedAnswer(artist, exclude, mix);
            var text = new StringBuilder();
            for (var attempt = 0; attempt < 5; attempt++)
            {
                var updates = client.GetStreamingResponseAsync(messages, options, cancellationToken).GetAsyncEnumerator(cancellationToken);
                Exception? failure = null;
                try
                {
                    while (true)
                    {
                        var step = await NextAsync(updates, cancellationToken);
                        if (step.Failure is not null || step.Ended)
                        {
                            failure = step.Failure;
                            break;
                        }

                        text.Append(step.Text);
                        foreach (var update in answer.Add(step.Text))
                        {
                            yield return update;
                        }
                    }
                }
                finally
                {
                    await updates.DisposeAsync();
                }

                if (failure is null)
                {
                    streamed = text.ToString();
                    break;
                }

                // Refused before anything came: without streaming, or without what the model refused.
                if (text.Length == 0 && failure is ClientResultException { Status: 400 } refused)
                {
                    var body = Body(refused);
                    if (body.Contains("stream", StringComparison.OrdinalIgnoreCase))
                    {
                        Unstreamable[StreamKey(connection)] = true;
                        break;
                    }

                    if (Drop(options, body))
                    {
                        continue;
                    }
                }

                // Broke off after cards were shown: they stay, and the answer is asked for once more, whole.
                if (answer.HasShown)
                {
                    break;
                }

                throw Fail(failure, connection);
            }
        }

        yield return new DiscoveryUpdate.Done(await ValidateAsync(client, connection, messages, options, artist, exclude, mix, streamed, cancellationToken));
    }

    // The whole answer, validated; one retry, without streaming, when it can't be used. With no streamed
    // answer (streaming refused, or broken off), it is asked for without streaming first.
    private static async Task<DiscoveryResult> ValidateAsync(
        IChatClient client,
        AiConnection connection,
        List<ChatMessage> messages,
        ChatOptions options,
        string artist,
        IReadOnlyCollection<string> exclude,
        LaneMix mix,
        string? streamed,
        CancellationToken cancellationToken)
    {
        var text = streamed ?? await AskAsync(client, connection, messages, options, cancellationToken);
        try
        {
            return SuggestionValidator.Validate(DiscoveryAnswer.Parse(text), artist, exclude, mix);
        }
        catch (DiscoveryAnswerException problem)
        {
            messages.Add(new ChatMessage(ChatRole.Assistant, text));
            messages.Add(new ChatMessage(ChatRole.User, DiscoveryPrompt.Retry(problem.Message)));
        }

        var retried = await AskAsync(client, connection, messages, options, cancellationToken);
        try
        {
            return SuggestionValidator.Validate(DiscoveryAnswer.Parse(retried), artist, exclude, mix);
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
            catch (ClientResultException failure) when (attempt < 4 && failure.Status == 400 && Drop(options, Body(failure)))
            {
                // Asked again without what the model refused.
            }
            catch (Exception failure) when (IsRequestFailure(failure, cancellationToken))
            {
                throw Fail(failure, connection);
            }
        }
    }

    // One piece of the streamed answer, or why the stream stopped; a failure is returned, not thrown, so the
    // caller can still yield what it had.
    private static async Task<StreamStep> NextAsync(IAsyncEnumerator<ChatResponseUpdate> updates, CancellationToken cancellationToken)
    {
        try
        {
            return await updates.MoveNextAsync() ? new StreamStep(updates.Current.Text, false, null) : new StreamStep(null, true, null);
        }
        catch (Exception failure) when (IsRequestFailure(failure, cancellationToken))
        {
            return new StreamStep(null, false, failure);
        }
    }

    private static bool IsRequestFailure(Exception failure, CancellationToken cancellationToken)
        => failure is ClientResultException or HttpRequestException
           || (failure is TaskCanceledException && !cancellationToken.IsCancellationRequested);

    // What a failed request means to the user.
    private static AiException Fail(Exception failure, AiConnection connection) => failure switch
    {
        ClientResultException refused => Translate(refused, connection),
        HttpRequestException => new AiException(AiFailure.Unreachable, $"{connection.Provider.Name} could not be reached. Check the connection and the endpoint.", failure),
        _ => new AiException(AiFailure.Unavailable, $"{connection.Provider.Name} took too long to answer. Try again.", failure),
    };

    // A connection whose provider refused to stream is asked without streaming until the app restarts.
    private static string StreamKey(AiConnection connection) => $"{connection.ProviderId}|{connection.Endpoint}|{connection.Model}";

    private sealed record StreamStep(string? Text, bool Ended, Exception? Failure);

    // Removes the option a 400 complains about; false when it complains about something else.
    private static bool Drop(ChatOptions options, string body)
    {
        if (options.Temperature is not null && body.Contains("temperature", StringComparison.OrdinalIgnoreCase))
        {
            options.Temperature = null;
            return true;
        }

        // After the temperature: a reasoning model refusing one tends to mention "reasoning" too.
        if (options.Reasoning is not null && body.Contains("reasoning", StringComparison.OrdinalIgnoreCase))
        {
            options.Reasoning = null;
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
