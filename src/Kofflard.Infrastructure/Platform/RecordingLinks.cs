using Kofflard.Core.Models;
using Kofflard.Core.Services;

namespace Kofflard.Infrastructure.Platform;

/// <summary>
/// A launcher and browser that write each link to a file instead of opening it
/// (<see cref="LaunchSettings.LaunchLog"/>), so a UI test can check what would have opened.
/// </summary>
/// <remarks>One line per link: <c>music &lt;service&gt; &lt;web url&gt;</c> or <c>web &lt;url&gt;</c>.</remarks>
public sealed class RecordingLinks(string path) : IMusicLauncher, IWebBrowser
{
    private readonly object _gate = new();

    /// <inheritdoc />
    public Task<LaunchOutcome> OpenAsync(MusicLink link, CancellationToken cancellationToken = default)
    {
        Append($"music {link.Service} {link.WebUrl.AbsoluteUri}");
        return Task.FromResult(LaunchOutcome.OpenedBrowser);
    }

    /// <inheritdoc />
    public Task OpenAsync(Uri url)
    {
        Append($"web {url.AbsoluteUri}");
        return Task.CompletedTask;
    }

    /// <summary>The lines written so far, oldest first.</summary>
    public static IReadOnlyList<string> Read(string path)
        => File.Exists(path) ? File.ReadAllLines(path) : [];

    private void Append(string line)
    {
        lock (_gate)
        {
            File.AppendAllLines(path, [line]);
        }
    }
}

/// <summary>A clock that always says the same time, for a test's <see cref="LaunchSettings.FixedNow"/>.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => now.ToUniversalTime();
}
