using Kofflard.Core.Models;

namespace Kofflard.Core.Ai;

/// <summary>
/// Secrets: API keys and tokens. <c>SecureStorage</c> in the app (Keychain, Keystore, DPAPI); in
/// memory in tests. Never SQLite, never <c>Preferences</c>, never a log (design §6.2).
/// </summary>
public interface ISecretStore
{
    /// <summary>The secret kept under <paramref name="name"/>, or null.</summary>
    Task<string?> GetAsync(string name);

    /// <summary>Keeps <paramref name="value"/> under <paramref name="name"/>.</summary>
    Task SetAsync(string name, string value);

    /// <summary>Forgets the secret under <paramref name="name"/>. Nothing happens when there is none.</summary>
    Task RemoveAsync(string name);
}

/// <summary>What a request to the AI went wrong on, in terms the user can act on.</summary>
public enum AiFailure
{
    /// <summary>No AI is connected yet.</summary>
    NotConnected,

    /// <summary>The provider refused the key or token.</summary>
    InvalidKey,

    /// <summary>The account has no credit or quota left.</summary>
    NoCredit,

    /// <summary>Too many requests; try again shortly.</summary>
    RateLimited,

    /// <summary>The model or deployment does not exist for this account.</summary>
    ModelNotFound,

    /// <summary>The provider could not be reached.</summary>
    Unreachable,

    /// <summary>The provider failed on its side.</summary>
    Unavailable,

    /// <summary>The answer could not be used, even after one retry.</summary>
    BadAnswer,
}

/// <summary>A request to the AI failed; <see cref="Exception.Message"/> is for the user.</summary>
public sealed class AiException(AiFailure failure, string message, Exception? inner = null) : Exception(message, inner)
{
    /// <summary>What went wrong.</summary>
    public AiFailure Failure { get; } = failure;
}

/// <summary>Asks the connected AI for musicians (design §7). Validation happens inside.</summary>
public interface IDiscoveryEngine
{
    /// <summary>Asks for musicians like <paramref name="artist"/>.</summary>
    /// <exception cref="AiException">The request failed; the message is for the user.</exception>
    Task<DiscoveryResult> DiscoverAsync(
        AiConnection connection,
        string? secret,
        string artist,
        IReadOnlyCollection<string> exclude,
        CancellationToken cancellationToken = default);

    /// <summary>One tiny request that shows the connection works (design §6.2).</summary>
    /// <exception cref="AiException">It does not; the message says why.</exception>
    Task TestAsync(AiConnection connection, string? secret, CancellationToken cancellationToken = default);
}

/// <summary>Signs in to OpenRouter (OAuth with PKCE) and returns the key it hands over (design §6.3).</summary>
public interface IOpenRouterSignIn
{
    /// <summary>The key, or null when the user cancelled.</summary>
    /// <exception cref="AiException">Signing in failed.</exception>
    Task<string?> SignInAsync(CancellationToken cancellationToken = default);
}
