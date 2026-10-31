using Kofflard.Core.Ai;
using Kofflard.Core.Models;
using Kofflard.Core.Services;
using Kofflard.Infrastructure.Data;

namespace Kofflard.IntegrationTests;

/// <summary>The SQLite stores on a fresh file per test.</summary>
public sealed class StoreTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private readonly string _folder = Path.Combine(Path.GetTempPath(), "kofflard-tests", Guid.NewGuid().ToString("N"));
    private readonly KofflardDatabase _database;

    public StoreTests()
    {
        _database = new KofflardDatabase(Path.Combine(_folder, "kofflard.db"));
    }

    [Fact]
    public async Task Settings_StartAsDefaults_AndRoundTrip()
    {
        var store = new SqliteSettingsStore(_database);
        Assert.Equal(AppSettings.Default, await store.LoadAsync());

        var changed = new AppSettings(new AiConnection("azure", "https://res.openai.azure.com", "gpt-4.1-mini"), MusicService.Spotify, true, Lane.Wild);
        await store.SaveAsync(changed);

        Assert.Equal(changed, await new SqliteSettingsStore(_database).LoadAsync());
    }

    [Theory]
    [InlineData("1", Lane.Safe)]
    [InlineData("2", Lane.Curious)]
    [InlineData("3", Lane.Curious)]
    [InlineData("4", Lane.Wild)]
    [InlineData("5", Lane.Wild)]
    [InlineData("curious", Lane.Curious)]
    [InlineData("Sideways", Lane.Safe)]
    public async Task Settings_AnOldLevel_ReadsAsItsLane_AndABadValueAsSafe(string stored, Lane expected)
    {
        var store = new SqliteSettingsStore(_database);
        await store.SaveAsync(AppSettings.Default);
        await using (var connection = await _database.OpenAsync())
        await using (var command = connection.Command("UPDATE settings SET value = $value WHERE key = 'adventure';", ("$value", stored)))
        {
            await command.ExecuteNonQueryAsync();
        }

        Assert.Equal(expected, (await store.LoadAsync()).Adventure);
    }

    [Fact]
    public async Task Settings_OnAFreshFile_StartWithTheLaunchsConnection()
    {
        var test = new AiConnection("local", "http://localhost:5000", "kofflard-test");

        Assert.Equal(test, (await new SqliteSettingsStore(_database, test).LoadAsync()).Connection);
    }

    [Fact]
    public async Task History_KeepsOneSearchPerName_NewestFirst()
    {
        var history = new SqliteHistoryStore(_database);
        await history.AddSearchAsync(new RecentSearch("Nick Drake", Now));
        await history.AddSearchAsync(new RecentSearch("Khruangbin", Now.AddMinutes(1)));
        await history.AddSearchAsync(new RecentSearch("nick drake", Now.AddMinutes(2)));

        var recent = await history.RecentSearchesAsync(20);

        Assert.Equal(["nick drake", "Khruangbin"], recent.Select(r => r.Artist));
    }

    [Fact]
    public async Task History_ListsOpenedMusiciansFirst()
    {
        var history = new SqliteHistoryStore(_database);
        await history.MarkShownAsync(["Vashti Bunyan", "Bert Jansch"], Now);
        await history.MarkShownAsync(["Linda Perhacs"], Now.AddMinutes(1));
        await history.MarkOpenedAsync("Bert Jansch", Now);

        Assert.Equal(["Bert Jansch", "Linda Perhacs", "Vashti Bunyan"], await history.ShownAsync(100));
    }

    [Fact]
    public async Task History_CachesResults_ForAsLongAsAsked()
    {
        var history = new SqliteHistoryStore(_database);
        var result = new DiscoveryResult(
            DiscoveryStatus.Ok,
            new Musician("Nick Drake", null),
            [new Suggestion("Vashti Bunyan", Lane.Safe, "why", null, ["folk"], "Just Another Diamond Day")],
            []);
        await history.CacheAsync("key", result, Now);

        var cached = await history.CachedAsync("key", Now.AddHours(-1));
        Assert.Equal("Vashti Bunyan", cached!.Suggestions.Single().Name);
        Assert.Null(await history.CachedAsync("key", Now.AddHours(1)));

        await history.ClearAsync();
        Assert.Null(await history.CachedAsync("key", Now.AddHours(-1)));
    }

    [Fact]
    public async Task Saved_RoundTrips_AndRemoves()
    {
        var saved = new SqliteSavedStore(_database);
        await saved.SaveAsync(new SavedMusician("Björk", "why", ["art pop"], "Homogenic", "7w2", Now));

        var all = await saved.AllAsync();
        Assert.Equal("Björk", all.Single().Name);
        Assert.Equal(["art pop"], all.Single().Tags);
        Assert.Contains("bjork", await saved.NamesAsync());

        await saved.RemoveAsync("BJORK");
        Assert.Empty(await saved.AllAsync());
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }
}
