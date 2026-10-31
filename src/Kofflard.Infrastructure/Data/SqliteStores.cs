using System.Globalization;
using System.Text.Json;
using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.Infrastructure.Data;

/// <summary>The settings, as key-value rows (design §3.3).</summary>
/// <param name="database">The app's database.</param>
/// <param name="initialConnection">
/// The connection a fresh database starts with: a test's fake AI (<see cref="LaunchSettings.AiBaseUrl"/>), or null.
/// </param>
public sealed class SqliteSettingsStore(KofflardDatabase database, AiConnection? initialConnection = null) : ISettingsStore
{
    private const string ProviderKey = "ai.provider";
    private const string EndpointKey = "ai.endpoint";
    private const string ModelKey = "ai.model";
    private const string OpenWithKey = "open_with";
    private const string CheckNamesKey = "check_names";
    private const string AdventureKey = "adventure";

    /// <inheritdoc />
    public async Task<AppSettings> LoadAsync()
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command("SELECT key, value FROM settings;");
        await using var reader = await command.ExecuteReaderAsync();

        var values = new Dictionary<string, string>();
        while (await reader.ReadAsync())
        {
            values[reader.GetString(0)] = reader.GetString(1);
        }

        if (values.Count == 0)
        {
            return AppSettings.Default with { Connection = initialConnection };
        }

        var defaults = AppSettings.Default;
        return new AppSettings(
            values.TryGetValue(ProviderKey, out var provider) && values.TryGetValue(ModelKey, out var model)
                ? new AiConnection(provider, values.GetValueOrDefault(EndpointKey) is { Length: > 0 } endpoint ? endpoint : null, model)
                : null,
            values.TryGetValue(OpenWithKey, out var openWith) && Enum.TryParse<MusicService>(openWith, out var service) ? service : defaults.OpenWith,
            values.TryGetValue(CheckNamesKey, out var check) ? check == "true" : defaults.CheckNames,
            ReadAdventure(values.GetValueOrDefault(AdventureKey)) ?? defaults.Adventure);
    }

    /// <inheritdoc />
    public async Task SaveAsync(AppSettings settings)
    {
        var values = new Dictionary<string, string>
        {
            [OpenWithKey] = settings.OpenWith.ToString(),
            [CheckNamesKey] = settings.CheckNames ? "true" : "false",
            [AdventureKey] = settings.Adventure.ToString(),
        };

        if (settings.Connection is { } ai)
        {
            values[ProviderKey] = ai.ProviderId;
            values[EndpointKey] = ai.Endpoint ?? string.Empty;
            values[ModelKey] = ai.Model;
        }

        await using var connection = await database.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await using (var clear = connection.Command("DELETE FROM settings;"))
        {
            await clear.ExecuteNonQueryAsync();
        }

        foreach (var (key, value) in values)
        {
            await using var insert = connection.Command("INSERT INTO settings (key, value) VALUES ($key, $value);", ("$key", key), ("$value", value));
            await insert.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    // "Curious"; or a level of 1-5 from before the slider moved to the results: 1 is Safe, 2-3 Curious,
    // 4-5 Wild (.my/design/adventure-as-outcomes.md). Anything else reads as not set.
    private static Lane? ReadAdventure(string? value)
    {
        if (int.TryParse(value, CultureInfo.InvariantCulture, out var level))
        {
            return level switch
            {
                <= 1 => Lane.Safe,
                <= 3 => Lane.Curious,
                _ => Lane.Wild,
            };
        }

        return Enum.TryParse<Lane>(value, ignoreCase: true, out var lane) && Enum.IsDefined(lane) ? lane : null;
    }
}

/// <summary>Searches, what was shown, and the result cache (design §9).</summary>
public sealed class SqliteHistoryStore(KofflardDatabase database) : IHistoryStore
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <inheritdoc />
    public async Task AddSearchAsync(RecentSearch search)
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command(
            """
            INSERT INTO searches (normalised, artist, adventure, at) VALUES ($normalised, $artist, 0, $at)
            ON CONFLICT(normalised) DO UPDATE SET artist = $artist, at = $at;
            """,
            ("$normalised", Names.Normalise(search.Artist)),
            ("$artist", search.Artist),
            ("$at", SqliteCommands.Stamp(search.At)));
        await command.ExecuteNonQueryAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RecentSearch>> RecentSearchesAsync(int count)
    {
        await using var connection = await database.OpenAsync();
        // The adventure column is from before the slider moved to the results; it is no longer read.
        await using var command = connection.Command("SELECT artist, at FROM searches ORDER BY at DESC LIMIT $count;", ("$count", count));
        await using var reader = await command.ExecuteReaderAsync();

        var searches = new List<RecentSearch>();
        while (await reader.ReadAsync())
        {
            searches.Add(new RecentSearch(reader.GetString(0), SqliteCommands.ReadStamp(reader.GetString(1))));
        }

        return searches;
    }

    /// <inheritdoc />
    public async Task MarkShownAsync(IEnumerable<string> names, DateTimeOffset at)
    {
        await using var connection = await database.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        foreach (var name in names)
        {
            await using var command = connection.Command(
                """
                INSERT INTO shown (normalised, name, first_shown_at, last_shown_at) VALUES ($normalised, $name, $at, $at)
                ON CONFLICT(normalised) DO UPDATE SET last_shown_at = $at;
                """,
                ("$normalised", Names.Normalise(name)),
                ("$name", name),
                ("$at", SqliteCommands.Stamp(at)));
            await command.ExecuteNonQueryAsync();
        }

        await transaction.CommitAsync();
    }

    /// <inheritdoc />
    public async Task MarkOpenedAsync(string name, DateTimeOffset at)
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command(
            """
            INSERT INTO shown (normalised, name, first_shown_at, last_shown_at, opened) VALUES ($normalised, $name, $at, $at, 1)
            ON CONFLICT(normalised) DO UPDATE SET opened = 1, last_shown_at = $at;
            """,
            ("$normalised", Names.Normalise(name)),
            ("$name", name),
            ("$at", SqliteCommands.Stamp(at)));
        await command.ExecuteNonQueryAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> ShownAsync(int count)
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command(
            "SELECT name FROM shown ORDER BY opened DESC, last_shown_at DESC LIMIT $count;",
            ("$count", count));
        await using var reader = await command.ExecuteReaderAsync();

        var names = new List<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }

    /// <inheritdoc />
    public async Task<DiscoveryResult?> CachedAsync(string key, DateTimeOffset since)
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command(
            "SELECT json, at FROM result_cache WHERE key = $key;",
            ("$key", key));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || SqliteCommands.ReadStamp(reader.GetString(1)) < since)
        {
            return null;
        }

        return JsonSerializer.Deserialize<DiscoveryResult>(reader.GetString(0), Json);
    }

    /// <inheritdoc />
    public async Task CacheAsync(string key, DiscoveryResult result, DateTimeOffset at)
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command(
            """
            INSERT INTO result_cache (key, json, at) VALUES ($key, $json, $at)
            ON CONFLICT(key) DO UPDATE SET json = $json, at = $at;
            """,
            ("$key", key),
            ("$json", JsonSerializer.Serialize(result, Json)),
            ("$at", SqliteCommands.Stamp(at)));
        await command.ExecuteNonQueryAsync();
    }

    /// <inheritdoc />
    public async Task ClearAsync()
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command("DELETE FROM searches; DELETE FROM shown; DELETE FROM result_cache;");
        await command.ExecuteNonQueryAsync();
    }
}

/// <summary>The saved musicians.</summary>
public sealed class SqliteSavedStore(KofflardDatabase database) : ISavedStore
{
    /// <inheritdoc />
    public async Task SaveAsync(SavedMusician musician)
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command(
            """
            INSERT INTO saved (normalised, name, why, tags, known_for, spotify_id, saved_at)
            VALUES ($normalised, $name, $why, $tags, $known_for, $spotify_id, $saved_at)
            ON CONFLICT(normalised) DO UPDATE SET name = $name, why = $why, tags = $tags,
                known_for = $known_for, spotify_id = $spotify_id, saved_at = $saved_at;
            """,
            ("$normalised", Names.Normalise(musician.Name)),
            ("$name", musician.Name),
            ("$why", musician.Why),
            ("$tags", JsonSerializer.Serialize(musician.Tags)),
            ("$known_for", musician.KnownFor),
            ("$spotify_id", musician.SpotifyArtistId),
            ("$saved_at", SqliteCommands.Stamp(musician.SavedAt)));
        await command.ExecuteNonQueryAsync();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string name)
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command("DELETE FROM saved WHERE normalised = $normalised;", ("$normalised", Names.Normalise(name)));
        await command.ExecuteNonQueryAsync();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<SavedMusician>> AllAsync()
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command("SELECT name, why, tags, known_for, spotify_id, saved_at FROM saved ORDER BY saved_at DESC;");
        await using var reader = await command.ExecuteReaderAsync();

        var saved = new List<SavedMusician>();
        while (await reader.ReadAsync())
        {
            saved.Add(new SavedMusician(
                reader.GetString(0),
                reader.GetString(1),
                JsonSerializer.Deserialize<List<string>>(reader.GetString(2)) ?? [],
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.IsDBNull(4) ? null : reader.GetString(4),
                SqliteCommands.ReadStamp(reader.GetString(5))));
        }

        return saved;
    }

    /// <inheritdoc />
    public async Task<IReadOnlySet<string>> NamesAsync()
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command("SELECT normalised FROM saved;");
        await using var reader = await command.ExecuteReaderAsync();

        var names = new HashSet<string>();
        while (await reader.ReadAsync())
        {
            names.Add(reader.GetString(0));
        }

        return names;
    }
}

/// <summary>The MusicBrainz outcomes, kept 30 days (design §8.4).</summary>
public sealed class SqliteVerificationCache(KofflardDatabase database)
{
    /// <summary>How long an outcome is trusted.</summary>
    public static readonly TimeSpan KeepFor = TimeSpan.FromDays(30);

    /// <summary>The outcome for this name, if checked since <paramref name="since"/>.</summary>
    public async Task<Verification?> GetAsync(string name, DateTimeOffset since)
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command(
            "SELECT state, mbid, spotify_id, at FROM verified WHERE normalised = $normalised;",
            ("$normalised", Names.Normalise(name)));
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || SqliteCommands.ReadStamp(reader.GetString(3)) < since)
        {
            return null;
        }

        return new Verification(
            Enum.Parse<VerificationState>(reader.GetString(0)),
            reader.IsDBNull(1) ? null : reader.GetString(1),
            reader.IsDBNull(2) ? null : reader.GetString(2));
    }

    /// <summary>Keeps the outcome for this name.</summary>
    public async Task SetAsync(string name, Verification verification, DateTimeOffset at)
    {
        await using var connection = await database.OpenAsync();
        await using var command = connection.Command(
            """
            INSERT INTO verified (normalised, state, mbid, spotify_id, at) VALUES ($normalised, $state, $mbid, $spotify, $at)
            ON CONFLICT(normalised) DO UPDATE SET state = $state, mbid = $mbid, spotify_id = $spotify, at = $at;
            """,
            ("$normalised", Names.Normalise(name)),
            ("$state", verification.State.ToString()),
            ("$mbid", verification.MusicBrainzId),
            ("$spotify", verification.SpotifyArtistId),
            ("$at", SqliteCommands.Stamp(at)));
        await command.ExecuteNonQueryAsync();
    }
}
