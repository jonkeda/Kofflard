using Kofflard.Core.Models;
using Kofflard.Core.Rules;
using Kofflard.Core.Services;

namespace Kofflard.UnitTests;

/// <summary>Settings in memory.</summary>
internal sealed class MemorySettings(AppSettings? start = null) : ISettingsStore
{
    public AppSettings Current { get; private set; } = start ?? AppSettings.Default;

    public Task<AppSettings> LoadAsync() => Task.FromResult(Current);

    public Task SaveAsync(AppSettings settings)
    {
        Current = settings;
        return Task.CompletedTask;
    }
}

/// <summary>History in memory.</summary>
internal sealed class MemoryHistory : IHistoryStore
{
    public List<RecentSearch> Searches { get; } = [];

    public List<string> Shown { get; } = [];

    public List<string> Opened { get; } = [];

    public Dictionary<string, (DiscoveryResult Result, DateTimeOffset At)> Cache { get; } = [];

    public Task AddSearchAsync(RecentSearch search)
    {
        Searches.RemoveAll(s => Names.Same(s.Artist, search.Artist));
        Searches.Insert(0, search);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<RecentSearch>> RecentSearchesAsync(int count) => Task.FromResult<IReadOnlyList<RecentSearch>>([.. Searches.Take(count)]);

    public Task MarkShownAsync(IEnumerable<string> names, DateTimeOffset at)
    {
        Shown.InsertRange(0, names);
        return Task.CompletedTask;
    }

    public Task MarkOpenedAsync(string name, DateTimeOffset at)
    {
        Opened.Add(name);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<string>> ShownAsync(int count) => Task.FromResult<IReadOnlyList<string>>([.. Shown.Take(count)]);

    public Task<DiscoveryResult?> CachedAsync(string key, DateTimeOffset since)
        => Task.FromResult(Cache.TryGetValue(key, out var entry) && entry.At >= since ? entry.Result : null);

    public Task CacheAsync(string key, DiscoveryResult result, DateTimeOffset at)
    {
        Cache[key] = (result, at);
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Searches.Clear();
        Shown.Clear();
        Cache.Clear();
        return Task.CompletedTask;
    }
}

/// <summary>Saved musicians in memory.</summary>
internal sealed class MemorySaved : ISavedStore
{
    public List<SavedMusician> Items { get; } = [];

    public Task SaveAsync(SavedMusician musician)
    {
        Items.RemoveAll(m => Names.Same(m.Name, musician.Name));
        Items.Insert(0, musician);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string name)
    {
        Items.RemoveAll(m => Names.Same(m.Name, name));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SavedMusician>> AllAsync() => Task.FromResult<IReadOnlyList<SavedMusician>>([.. Items]);

    public Task<IReadOnlySet<string>> NamesAsync() => Task.FromResult<IReadOnlySet<string>>(Items.Select(m => Names.Normalise(m.Name)).ToHashSet());
}

/// <summary>Records links instead of opening them.</summary>
internal sealed class RecordingLauncher : IMusicLauncher, IWebBrowser
{
    public List<MusicLink> Links { get; } = [];

    public List<Uri> Pages { get; } = [];

    public Task<LaunchOutcome> OpenAsync(MusicLink link, CancellationToken cancellationToken = default)
    {
        Links.Add(link);
        return Task.FromResult(LaunchOutcome.OpenedApp);
    }

    public Task OpenAsync(Uri url)
    {
        Pages.Add(url);
        return Task.CompletedTask;
    }
}

/// <summary>Records navigation.</summary>
internal sealed class RecordingNavigator : INavigator
{
    public List<(string Route, IReadOnlyDictionary<string, object>? Parameters)> Visits { get; } = [];

    public Task GoToAsync(string route, IReadOnlyDictionary<string, object>? parameters = null)
    {
        Visits.Add((route, parameters));
        return Task.CompletedTask;
    }

    public Task GoBackAsync()
    {
        Visits.Add(("..", null));
        return Task.CompletedTask;
    }
}

/// <summary>Answers every question with <see cref="Answer"/>.</summary>
internal sealed class ScriptedDialogs : IDialogs
{
    public bool Answer { get; set; } = true;

    public List<string> Shown { get; } = [];

    public Task<bool> ConfirmAsync(string title, string message, string accept, string cancel)
    {
        Shown.Add(title);
        return Task.FromResult(Answer);
    }

    public Task AlertAsync(string title, string message, string ok = "OK")
    {
        Shown.Add(title);
        return Task.CompletedTask;
    }
}
