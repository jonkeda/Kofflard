using System.Reflection;
using System.Text;
using System.Text.Json;
using WireMock.Matchers;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Kofflard.TestSupport;

/// <summary>
/// A fake AI: WireMock speaking the OpenAI-compatible chat API at <c>/v1/chat/completions</c>, so the app
/// talks to it through its real provider code (design §10.3). No test calls a real model.
/// </summary>
/// <remarks>
/// Answers are chosen by the musician in the request (<see cref="Answer"/>); anything else gets
/// <see cref="Default"/>. A connection test ("Reply with OK.") gets "OK". A request that asks to stream gets
/// the same answer as server-sent events, cut into small chunks, as a provider streams it.
/// </remarks>
public sealed class FakeAi : IDisposable
{
    /// <summary>The model a test connects with.</summary>
    public const string Model = Kofflard.Infrastructure.LaunchSettings.TestModel;

    private readonly WireMockServer _server;

    private FakeAi(WireMockServer server)
    {
        _server = server;
        Reset();
    }

    /// <summary>The server's base URL, e.g. <c>http://localhost:51234</c>: the "local" provider's endpoint.</summary>
    public string BaseUrl => _server.Urls[0];

    /// <summary>The answer a musician nobody set gets: Nick Drake's.</summary>
    public static string Default => Canned("nick-drake");

    /// <summary>Starts a fake on a free port.</summary>
    public static FakeAi Start() => new(WireMockServer.Start());

    /// <summary>A canned answer from <c>Answers/&lt;name&gt;.json</c>.</summary>
    public static string Canned(string name)
    {
        using var stream = typeof(FakeAi).Assembly.GetManifestResourceStream($"Kofflard.Answers.{name}.json")
            ?? throw new ArgumentException($"There is no canned answer '{name}'.", nameof(name));
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>Back to the defaults: every musician gets <see cref="Default"/>, the connection test gets OK.</summary>
    public void Reset()
    {
        _server.Reset();
        foreach (var stream in new[] { false, true })
        {
            _server
                .Given(Chat(stream, body => Says(body, "Reply with OK.")))
                .AtPriority(1)
                .RespondWith(Completion("OK", stream));
            _server
                .Given(Chat(stream))
                .AtPriority(100)
                .RespondWith(Completion(Default, stream));
        }
    }

    /// <summary>Answers a search for <paramref name="musician"/> with <paramref name="content"/>.</summary>
    public FakeAi Answer(string musician, string content)
    {
        foreach (var stream in new[] { false, true })
        {
            _server
                .Given(Chat(stream, body => Says(body, $"Musician: \"{musician}\"")))
                .AtPriority(10)
                .RespondWith(Completion(content, stream));
        }

        return this;
    }

    /// <summary>Refuses a reasoning effort, as a model that doesn't reason does: a 400 that names it.</summary>
    public FakeAi RefuseReasoning()
    {
        _server
            .Given(Chat().WithBody((string? body) => body?.Contains("reasoning_effort", StringComparison.Ordinal) == true))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new { error = new { message = "Unrecognized request argument supplied: reasoning_effort", type = "invalid_request_error" } })));
        return this;
    }

    /// <summary>Refuses to stream, as some providers do for some models: a 400 that names "stream".</summary>
    public FakeAi RefuseStreaming()
    {
        _server
            .Given(Chat(stream: true))
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(400)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new { error = new { message = "Unsupported value: 'stream' does not support true with this model.", type = "invalid_request_error" } })));
        return this;
    }

    /// <summary>Answers every request with an HTTP error, as a provider refusing a key (401) or out of credit (402) does.</summary>
    public FakeAi FailWith(int status, string message = "error")
    {
        _server
            .Given(Chat())
            .AtPriority(0)
            .RespondWith(Response.Create()
                .WithStatusCode(status)
                .WithHeader("Content-Type", "application/json")
                .WithBody(JsonSerializer.Serialize(new { error = new { message, type = "invalid_request_error" } })));
        return this;
    }

    /// <summary>Answers every request after <paramref name="delay"/>.</summary>
    public FakeAi Delay(TimeSpan delay, string content)
    {
        foreach (var stream in new[] { false, true })
        {
            _server
                .Given(Chat(stream))
                .AtPriority(5)
                .RespondWith(Completion(content, stream).WithDelay(delay));
        }

        return this;
    }

    /// <summary>Answers the first request with <paramref name="first"/> and every later one with <paramref name="then"/>.</summary>
    public FakeAi InSequence(string first, string then)
    {
        const string scenario = "sequence";
        foreach (var stream in new[] { false, true })
        {
            _server.Given(Chat(stream)).InScenario(scenario).WillSetStateTo("answered").AtPriority(2).RespondWith(Completion(first, stream));
            _server.Given(Chat(stream)).InScenario(scenario).WhenStateIs("answered").AtPriority(2).RespondWith(Completion(then, stream));
        }

        return this;
    }

    /// <summary>The bodies of the chat requests received, oldest first.</summary>
    public IReadOnlyList<string> Requests
        => [.. _server.LogEntries
            .Where(entry => entry.RequestMessage?.Path?.EndsWith("/chat/completions", StringComparison.Ordinal) == true)
            .Select(entry => entry.RequestMessage!.Body ?? string.Empty)];

    /// <summary>The path of the last chat request.</summary>
    public string? LastPath => _server.LogEntries.LastOrDefault()?.RequestMessage?.Path;

    /// <summary>The headers of the last chat request.</summary>
    public IDictionary<string, WireMock.Types.WireMockList<string>>? LastHeaders
        => _server.LogEntries.LastOrDefault()?.RequestMessage?.Headers;

    /// <inheritdoc />
    public void Dispose() => _server.Dispose();

    // Whether a message of the request contains the text: the body is JSON, so its quotes are escaped.
    private static bool Says(string? body, string text)
    {
        if (body is null)
        {
            return false;
        }

        try
        {
            using var json = JsonDocument.Parse(body);
            return json.RootElement.GetProperty("messages").EnumerateArray()
                .Any(message => message.TryGetProperty("content", out var content)
                    && content.ValueKind == JsonValueKind.String
                    && content.GetString()!.Contains(text, StringComparison.Ordinal));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // Whether the request asks for a streamed answer.
    private static bool Streams(string? body)
    {
        try
        {
            using var json = JsonDocument.Parse(body ?? "{}");
            return json.RootElement.TryGetProperty("stream", out var stream) && stream.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    // Any base path: /v1/ for a local server, /openai/v1/ for Azure. Streamed requests and the others apart.
    private static IRequestBuilder Chat(bool stream, Func<string?, bool>? and = null)
        => Request.Create()
            .WithPath(new WildcardMatcher("*/v1/chat/completions"))
            .UsingPost()
            .WithBody((string? body) => Streams(body) == stream && (and?.Invoke(body) ?? true));

    private static IRequestBuilder Chat() => Request.Create().WithPath(new WildcardMatcher("*/v1/chat/completions")).UsingPost();

    private static IResponseBuilder Completion(string content, bool stream) => stream ? Streamed(content) : Completion(content);

    /// <summary>
    /// The same answer streamed: server-sent chunks of a few characters each, then a finish chunk and
    /// <c>[DONE]</c>, as the OpenAI-compatible API streams.
    /// </summary>
    private static IResponseBuilder Streamed(string content)
    {
        const int size = 24;
        var events = new StringBuilder();
        void Send(object delta, string? finish)
            => events.Append("data: ").Append(JsonSerializer.Serialize(new
            {
                id = "chatcmpl-kofflard",
                @object = "chat.completion.chunk",
                created = 1_790_000_000,
                model = Model,
                choices = new[] { new { index = 0, delta, finish_reason = finish } },
            })).Append("\n\n");

        Send(new { role = "assistant", content = string.Empty }, null);
        for (var start = 0; start < content.Length; start += size)
        {
            Send(new { content = content.Substring(start, Math.Min(size, content.Length - start)) }, null);
        }

        Send(new { }, "stop");
        events.Append("data: [DONE]\n\n");

        return Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "text/event-stream")
            .WithBody(events.ToString());
    }

    /// <summary>A chat completion whose message content is <paramref name="content"/>.</summary>
    private static IResponseBuilder Completion(string content)
        => Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", "application/json")
            .WithBody(JsonSerializer.Serialize(new
            {
                id = "chatcmpl-kofflard",
                @object = "chat.completion",
                created = 1_790_000_000,
                model = Model,
                choices = new[]
                {
                    new { index = 0, message = new { role = "assistant", content }, finish_reason = "stop" },
                },
                usage = new { prompt_tokens = 10, completion_tokens = 10, total_tokens = 20 },
            }));
}
