using System.Text.Json;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Services;
using Kofflard.Core.ViewModels;
using Kofflard.Infrastructure;
using Kofflard.Infrastructure.Ai;
using Kofflard.Infrastructure.Data;
using Kofflard.Infrastructure.Music;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Kofflard.IntegrationTests;

/// <summary>MusicBrainz, OpenRouter's key exchange, and the composition root.</summary>
public sealed class ServiceTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kofflard-tests", Guid.NewGuid().ToString("N"));
    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly HttpClient _http = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 10, 31, 9, 0, 0, TimeSpan.Zero));

    [Fact]
    public async Task MusicBrainz_Verifies_AnExactName_AndFindsItsSpotifyId()
    {
        Search("Vashti Bunyan", ("c2f1", "Vashti Bunyan", 100));
        _server.Given(Request.Create().WithPath("/ws/2/artist/c2f1").UsingGet())
            .RespondWith(Json(new
            {
                relations = new[]
                {
                    new { type = "discogs", url = new { resource = "https://www.discogs.com/artist/1" } },
                    new { type = "free streaming", url = new { resource = "https://open.spotify.com/artist/4aFvP?si=x" } },
                },
            }));

        var verification = await Verifier().VerifyAsync("Vashti Bunyan");

        Assert.Equal(VerificationState.Verified, verification.State);
        Assert.Equal("c2f1", verification.MusicBrainzId);
        Assert.Equal("4aFvP", verification.SpotifyArtistId);
        Assert.Contains("Kofflard/", _server.LogEntries.First().RequestMessage!.Headers!["User-Agent"].Single(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task MusicBrainz_DoesNotVerify_ALookAlikeOrALowScore()
    {
        Search("Vashti Bunyon", ("x1", "Vashti Bunyan", 100), ("x2", "Vashti Bunyon", 60));

        Assert.Equal(VerificationState.NotFound, (await Verifier().VerifyAsync("Vashti Bunyon")).State);
    }

    [Fact]
    public async Task MusicBrainz_AsksOnce_ThenUsesItsCache()
    {
        Search("Bert Jansch", ("b1", "Bert Jansch", 100));
        _server.Given(Request.Create().WithPath("/ws/2/artist/b1").UsingGet()).RespondWith(Json(new { relations = Array.Empty<object>() }));
        var verifier = Verifier();

        await verifier.VerifyAsync("Bert Jansch");
        var again = await verifier.VerifyAsync("bert jansch");

        Assert.Equal(VerificationState.Verified, again.State);
        Assert.Equal(2, _server.LogEntries.Count());
    }

    [Fact]
    public async Task OpenRouter_SignIn_SendsTheChallenge_AndExchangesTheCodeForAKey()
    {
        _server.Given(Request.Create().WithPath("/api/v1/auth/keys").UsingPost()).RespondWith(Json(new { key = "sk-or-v1-abc" }));
        var broker = new CodeBroker("the-code");
        var signIn = new OpenRouterSignIn(broker, _http, new Uri(_server.Urls[0] + "/"));

        var key = await signIn.SignInAsync();

        Assert.Equal("sk-or-v1-abc", key);
        Assert.Contains("code_challenge_method=S256", broker.Opened!.Query, StringComparison.Ordinal);
        Assert.Contains("callback_url=http%3A%2F%2Flocalhost%3A1234%2Fcallback%2F", broker.Opened.Query, StringComparison.Ordinal);

        using var body = JsonDocument.Parse(_server.LogEntries.Single().RequestMessage!.Body!);
        Assert.Equal("the-code", body.RootElement.GetProperty("code").GetString());
        var verifier = body.RootElement.GetProperty("code_verifier").GetString()!;
        Assert.Contains($"code_challenge={Pkce.Challenge(verifier)}", broker.Opened.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenRouter_AnExpiredCode_IsExplained()
    {
        _server.Given(Request.Create().WithPath("/api/v1/auth/keys").UsingPost()).RespondWith(Response.Create().WithStatusCode(403));
        var signIn = new OpenRouterSignIn(new CodeBroker("old"), _http, new Uri(_server.Urls[0] + "/"));

        var failure = await Assert.ThrowsAsync<AiException>(() => signIn.SignInAsync());

        Assert.Contains("expired", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Composition_ResolvesEveryViewModel_AndStartsConnected_UnderTest()
    {
        var links = new Kofflard.Infrastructure.Platform.RecordingLinks(Path.Combine(_folder, "links.txt"));
        var services = new ServiceCollection();
        services.AddSingleton<INavigator>(new NoNavigation());
        services.AddSingleton<IDialogs>(new NoNavigation());
        services.AddKofflardServices(
            _ => new LaunchSettings { DatabasePath = Path.Combine(_folder, "kofflard.db"), AiBaseUrl = "http://localhost:1", AiKey = "test" },
            new KofflardPlatform(_ => new InMemorySecretStore(), _ => links, _ => links, _ => new CodeBroker("x")));
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });

        provider.GetRequiredService<SearchViewModel>();
        provider.GetRequiredService<ResultsViewModel>();
        provider.GetRequiredService<ConnectViewModel>();
        provider.GetRequiredService<SettingsViewModel>();
        provider.GetRequiredService<SavedViewModel>();

        var connected = await provider.GetRequiredService<AiConnections>().GetWithSecretAsync();
        Assert.Equal("local", connected!.Value.Connection.ProviderId);
        Assert.Equal(LaunchSettings.TestModel, connected.Value.Connection.Model);
    }

    public void Dispose()
    {
        _server.Dispose();
        _http.Dispose();
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private MusicBrainzVerifier Verifier()
        => new(_http, new Uri(_server.Urls[0] + "/"), new SqliteVerificationCache(new KofflardDatabase(Path.Combine(_folder, "kofflard.db"))), _time, TimeSpan.Zero);

    private void Search(string name, params (string Id, string Name, int Score)[] artists)
        => _server.Given(Request.Create().WithPath("/ws/2/artist").UsingGet().WithParam("query", $"artist:\"{name}\""))
            .RespondWith(Json(new { artists = artists.Select(a => new { id = a.Id, name = a.Name, score = a.Score }) }));

    private static IResponseBuilder Json(object body)
        => Response.Create().WithStatusCode(200).WithHeader("Content-Type", "application/json").WithBody(JsonSerializer.Serialize(body));

    private sealed class CodeBroker(string code) : IOAuthBroker
    {
        public Uri? Opened { get; private set; }

        public Task<IReadOnlyDictionary<string, string>?> AuthenticateAsync(Func<Uri, Uri> authorizeUrl, CancellationToken cancellationToken = default)
        {
            Opened = authorizeUrl(new Uri("http://localhost:1234/callback/"));
            return Task.FromResult<IReadOnlyDictionary<string, string>?>(new Dictionary<string, string> { ["code"] = code });
        }
    }

    private sealed class NoNavigation : INavigator, IDialogs
    {
        public Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null) => Task.CompletedTask;

        public Task GoBackAsync() => Task.CompletedTask;

        public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel) => Task.FromResult(false);

        public Task AlertAsync(string title, string message, string ok = "OK") => Task.CompletedTask;
    }
}
