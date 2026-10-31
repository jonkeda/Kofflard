using Kofflard.Core.Services;

namespace Kofflard.Core.Ai;

/// <summary>
/// The current AI connection: its non-secret part in settings, its secret in secure storage.
/// </summary>
public sealed class AiConnections(ISettingsStore settings, ISecretStore secrets)
{
    /// <summary>The name the secret is kept under.</summary>
    public const string SecretName = "kofflard.ai.secret";

    /// <summary>The connection, or null when none is set up.</summary>
    public async Task<AiConnection?> GetAsync()
    {
        var current = await settings.LoadAsync();
        return current.Connection;
    }

    /// <summary>The connection and its secret, or null when none is set up or its secret is missing.</summary>
    public async Task<(AiConnection Connection, string? Secret)?> GetWithSecretAsync()
    {
        if (await GetAsync() is not { } connection)
        {
            return null;
        }

        var secret = connection.Provider.NeedsSecret ? await secrets.GetAsync(SecretName) : null;
        if (connection.Provider.NeedsSecret && string.IsNullOrEmpty(secret))
        {
            return null;
        }

        return (connection, secret);
    }

    /// <summary>Whether an AI is connected.</summary>
    public async Task<bool> IsConnectedAsync() => await GetWithSecretAsync() is not null;

    /// <summary>Keeps <paramref name="connection"/> and its secret, replacing the previous one.</summary>
    public async Task SaveAsync(AiConnection connection, string? secret)
    {
        if (connection.Provider.NeedsSecret && !string.IsNullOrEmpty(secret))
        {
            await secrets.SetAsync(SecretName, secret);
        }
        else
        {
            await secrets.RemoveAsync(SecretName);
        }

        var current = await settings.LoadAsync();
        await settings.SaveAsync(current with { Connection = connection });
    }

    /// <summary>Forgets the connection and its secret.</summary>
    public async Task DisconnectAsync()
    {
        await secrets.RemoveAsync(SecretName);
        var current = await settings.LoadAsync();
        await settings.SaveAsync(current with { Connection = null });
    }
}

/// <summary>A secret store that keeps secrets in memory: tests, and the headless head.</summary>
public sealed class InMemorySecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _secrets = [];

    /// <inheritdoc />
    public Task<string?> GetAsync(string name)
    {
        lock (_secrets)
        {
            return Task.FromResult(_secrets.TryGetValue(name, out var value) ? value : null);
        }
    }

    /// <inheritdoc />
    public Task SetAsync(string name, string value)
    {
        lock (_secrets)
        {
            _secrets[name] = value;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(string name)
    {
        lock (_secrets)
        {
            _secrets.Remove(name);
        }

        return Task.CompletedTask;
    }
}
