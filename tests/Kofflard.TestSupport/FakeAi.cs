using System.Reflection;
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
/// <see cref="Default"/>. A connection test ("Reply with OK.") gets "OK".
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
        _server
            .Given(Chat().WithBody((string? body) => Says(body, "Reply with OK.")))
            .AtPriority(1)
            .RespondWith(Completion("OK"));
        _server
            .Given(Chat())
            .AtPriority(100)
            .RespondWith(Completion(Default));
    }

    /// <summary>Answers a search for <paramref name="musician"/> with <paramref name="content"/>.</summary>
    public FakeAi Answer(string musician, string content)
    {
        _server
            .Given(Chat().WithBody((string? body) => Says(body, $"Musician: \"{musician}\"")))
            .AtPriority(10)
            .RespondWith(Completion(content));
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
        _server
            .Given(Chat())
            .AtPriority(5)
            .RespondWith(Completion(content).WithDelay(delay));
        return this;
    }

    /// <summary>Answers the first request with <paramref name="first"/> and every later one with <paramref name="then"/>.</summary>
    public FakeAi InSequence(string first, string then)
    {
        const string scenario = "sequence";
        _server.Given(Chat()).InScenario(scenario).WillSetStateTo("answered").AtPriority(2).RespondWith(Completion(first));
        _server.Given(Chat()).InScenario(scenario).WhenStateIs("answered").AtPriority(2).RespondWith(Completion(then));
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

    // Any base path: /v1/ for a local server, /openai/v1/ for Azure.
    private static IRequestBuilder Chat() => Request.Create().WithPath(new WildcardMatcher("*/v1/chat/completions")).UsingPost();

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
