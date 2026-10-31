using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Kofflard.Core.Ai;
using Kofflard.Core.Services;

namespace Kofflard.Infrastructure.Ai;

/// <summary>
/// Runs an OAuth browser flow: opens the provider's page and returns the query of the redirect back.
/// </summary>
public interface IOAuthBroker
{
    /// <summary>
    /// Opens the page <paramref name="authorizeUrl"/> makes from the callback URL this broker listens on,
    /// and waits for the redirect.
    /// </summary>
    /// <returns>The callback's query values, or null when the user cancelled.</returns>
    Task<IReadOnlyDictionary<string, string>?> AuthenticateAsync(Func<Uri, Uri> authorizeUrl, CancellationToken cancellationToken = default);
}

/// <summary>
/// OpenRouter's OAuth with PKCE (design §6.3): the user signs in on OpenRouter and the app receives a
/// key billed to the user's own credits. No client registration is needed.
/// </summary>
public sealed class OpenRouterSignIn(IOAuthBroker broker, HttpClient http, Uri baseUrl) : IOpenRouterSignIn
{
    /// <inheritdoc />
    public async Task<string?> SignInAsync(CancellationToken cancellationToken = default)
    {
        var verifier = Pkce.CreateVerifier();
        var challenge = Pkce.Challenge(verifier);

        IReadOnlyDictionary<string, string>? callback;
        try
        {
            callback = await broker.AuthenticateAsync(
                callbackUrl => new Uri(baseUrl,
                    $"auth?callback_url={Uri.EscapeDataString(callbackUrl.ToString())}"
                    + $"&code_challenge={challenge}&code_challenge_method=S256&key_label=Kofflard"),
                cancellationToken);
        }
        catch (HttpListenerException failure)
        {
            throw new AiException(AiFailure.Unreachable, "Kofflard could not wait for the sign-in to come back.", failure);
        }

        if (callback is null)
        {
            return null;
        }

        if (!callback.TryGetValue("code", out var code) || code.Length == 0)
        {
            throw new AiException(AiFailure.InvalidKey, "OpenRouter did not complete the sign-in.");
        }

        return await ExchangeAsync(code, verifier, cancellationToken);
    }

    /// <summary>Exchanges the sign-in's code for a key.</summary>
    internal async Task<string> ExchangeAsync(string code, string verifier, CancellationToken cancellationToken)
    {
        HttpResponseMessage response;
        try
        {
            response = await http.PostAsJsonAsync(
                new Uri(baseUrl, "api/v1/auth/keys"),
                new KeyRequest(code, verifier, "S256"),
                cancellationToken);
        }
        catch (HttpRequestException failure)
        {
            throw new AiException(AiFailure.Unreachable, "OpenRouter could not be reached.", failure);
        }

        using (response)
        {
            if (!response.IsSuccessStatusCode)
            {
                throw new AiException(
                    AiFailure.InvalidKey,
                    response.StatusCode == HttpStatusCode.Forbidden
                        ? "The OpenRouter sign-in expired or was already used. Sign in again."
                        : $"OpenRouter refused the sign-in ({(int)response.StatusCode}).");
            }

            var answer = await response.Content.ReadFromJsonAsync<KeyResponse>(cancellationToken);
            return answer?.Key is { Length: > 0 } key
                ? key
                : throw new AiException(AiFailure.InvalidKey, "OpenRouter signed you in but sent no key.");
        }
    }

    private sealed record KeyRequest(
        [property: JsonPropertyName("code")] string Code,
        [property: JsonPropertyName("code_verifier")] string CodeVerifier,
        [property: JsonPropertyName("code_challenge_method")] string CodeChallengeMethod);

    private sealed record KeyResponse([property: JsonPropertyName("key")] string? Key);
}

/// <summary>PKCE (RFC 7636): the verifier stays in memory, only its S256 challenge leaves the device.</summary>
public static class Pkce
{
    /// <summary>A fresh verifier: 32 random bytes, base64url.</summary>
    public static string CreateVerifier() => Base64Url(RandomNumberGenerator.GetBytes(32));

    /// <summary>The S256 challenge of <paramref name="verifier"/>.</summary>
    public static string Challenge(string verifier) => Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));

    private static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}

/// <summary>
/// The desktop broker (design §6.3): a listener on <c>http://localhost:&lt;free port&gt;/callback/</c> and the
/// system browser. Used on Windows, where <c>WebAuthenticator</c> is not reliable for unpackaged apps.
/// </summary>
public sealed class LoopbackOAuthBroker(IWebBrowser browser) : IOAuthBroker
{
    /// <summary>How long the user has to sign in.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMinutes(5);

    private const string Done = """
        <!doctype html><html><head><meta charset="utf-8"><title>Kofflard</title></head>
        <body style="font-family:sans-serif;text-align:center;margin-top:20vh">
        <h1>You're signed in</h1><p>You can close this tab and return to Kofflard.</p></body></html>
        """;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<string, string>?> AuthenticateAsync(Func<Uri, Uri> authorizeUrl, CancellationToken cancellationToken = default)
    {
        var port = FreePort();
        var callback = new Uri($"http://localhost:{port}/callback/");

        using var listener = new HttpListener();
        listener.Prefixes.Add(callback.ToString());
        listener.Start();

        await browser.OpenAsync(authorizeUrl(callback));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);

        HttpListenerContext context;
        try
        {
            context = await listener.GetContextAsync().WaitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            return null;
        }

        var query = context.Request.QueryString;
        var values = query.AllKeys
            .Where(key => key is not null)
            .ToDictionary(key => key!, key => query[key] ?? string.Empty);

        var body = Encoding.UTF8.GetBytes(Done);
        context.Response.ContentType = "text/html; charset=utf-8";
        context.Response.ContentLength64 = body.Length;
        await context.Response.OutputStream.WriteAsync(body, cancellationToken);
        context.Response.Close();

        return values;
    }

    private static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        var port = ((IPEndPoint)socket.LocalEndpoint).Port;
        socket.Stop();
        return port;
    }
}
