using Bruinet.Core.Testing;
using Bruinet.Maui.Configuration;
using Bruinet.Maui.Enums;
using Bruinet.Maui.Headless;
using Bruinet.Maui.Testing;
using Kofflard.App.Services;
using Kofflard.Core.Ai;
using Kofflard.Infrastructure;
using Kofflard.Infrastructure.Ai;
using Kofflard.Infrastructure.Data;
using Kofflard.Infrastructure.Platform;
using Kofflard.TestSupport;
using Kofflard.UITests.Pages;
using Microsoft.Extensions.DependencyInjection;
using KofflardApp = Kofflard.App.App;
using KofflardShell = Kofflard.App.AppShell;

namespace Kofflard.UITests;

/// <summary>
/// Kofflard, hermetic: its own database, a fake AI (<see cref="FakeAi"/>) it is connected to from the
/// start, a fixed clock, and a launch log instead of a browser, so a test can check what would have
/// opened without anything opening.
/// </summary>
/// <remarks>
/// <para>
/// As in Bruinet's Todo suite, everything is arranged in <see cref="CreateTestContextOptions"/> (or
/// <see cref="CreateHeadlessOptions"/>), which the base constructor calls before it launches the app, so
/// what a subclass needs is built there, not in field initialisers.
/// </para>
/// <para>
/// One app per collection, shared by its tests. Each test starts with <see cref="StartTest"/>, which
/// resets the fake AI, clears the launch log and returns to the search page.
/// </para>
/// </remarks>
public class KofflardAppFixture : MauiTestFixtureBase
{
    /// <summary>The app's Android package name (<c>ApplicationId</c>).</summary>
    public const string AndroidPackage = "nl.jonkeda.kofflard";

    /// <summary>The fixed "now" every test runs at.</summary>
    public static readonly DateTimeOffset Now = new(2026, 10, 31, 9, 0, 0, TimeSpan.Zero);

    private Arrangement? _arrangement;

    /// <summary>Launches the app and waits for its first page.</summary>
    public KofflardAppFixture()
    {
        Arrived();
    }

    /// <summary>
    /// The design the app shows. The desktop one (Aurora D) for this suite, on every head, a phone's
    /// included; <see cref="PhoneAppFixture"/> shows the phone's (.my/design/single-page-phone.md).
    /// </summary>
    protected virtual KofflardLayout Layout => KofflardLayout.Desktop;

    /// <summary>The fake AI the app is connected to, unless the run is live.</summary>
    public FakeAi Ai => Arranged.Ai;

    /// <summary>
    /// Whether the app talks to the live AI (<c>KOFFLARD_LIVE_AI=1</c>, <see cref="LiveAi"/>): for a demo
    /// video with real answers. Scenarios that check specific names are for the fake AI only.
    /// </summary>
    public bool IsLive => Arranged.Live is not null;

    /// <summary>The search page, where every test starts.</summary>
    public SearchPage Search => new(Context);

    /// <summary>Every link the app would have opened, oldest first: <c>music &lt;service&gt; &lt;url&gt;</c> or <c>web &lt;url&gt;</c>.</summary>
    public IReadOnlyList<string> OpenedLinks => RecordingLinks.Read(Arranged.LaunchLog);

    private Arrangement Arranged => _arrangement
        ?? throw new InvalidOperationException("The fixture was used before the app was arranged.");

    /// <summary>Puts back what earlier tests may have changed outside the app, and returns to the search page.</summary>
    public SearchPage StartTest()
    {
        Reset();
        return ReturnToSearch();
    }

    /// <summary>Waits for the app's first page.</summary>
    protected virtual void Arrived() => SearchPage.Arrived(Context);

    /// <summary>Puts back what earlier tests may have changed outside the app: the AI, the history, the settings, the launch log.</summary>
    protected void Reset()
    {
        LetTheLastTestFinish();
        Ai.Reset();
        ForgetHistory();
        DefaultSettings();
        if (File.Exists(Arranged.LaunchLog))
        {
            File.Delete(Arranged.LaunchLog);
        }
    }

    /// <summary>Waits until the launch log holds a line starting with <paramref name="start"/>.</summary>
    public string WaitForOpenedLink(string start)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(TestConstants.PageTimeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            LetTheLastTestFinish();
            if (OpenedLinks.FirstOrDefault(line => line.StartsWith(start, StringComparison.Ordinal)) is { } line)
            {
                return line;
            }

            Thread.Sleep(50);
        }

        throw new AssertionException($"Expected a link starting '{start}'. Opened: {string.Join(" | ", OpenedLinks)}");
    }

    /// <inheritdoc />
    protected override MauiTestContextOptions CreateTestContextOptions()
    {
        var settings = Arrange();
        var options = base.CreateTestContextOptions();
        foreach (var (name, value) in LaunchValues(settings, Platform))
        {
            options.DriverOptions!.LaunchSettings[name] = value;
        }

        return options;
    }

    /// <summary>
    /// The MauiHeadless head (<c>BRUINET_HEAD=headless</c>): the app composed as its <c>MauiProgram</c> composes
    /// it (<see cref="KofflardAppServices.AddKofflardApp"/>), with the launch settings handed over directly.
    /// </summary>
    protected override MauiTestContextOptions CreateHeadlessOptions()
    {
        var settings = _arrangement?.Settings ?? Arrange();

        // What UseMauiCommunityToolkit() does first on a device: the toolkit's assembly loaded, so the XAML
        // parser knows toolkit:StateContainer (as in Todo).
        _ = typeof(CommunityToolkit.Maui.Layouts.StateContainer).Assembly;
        return MauiHeadlessHead.Options(
            services => services.GetRequiredService<KofflardShell>(),
            options => options.Application = services => new KofflardApp(services.GetRequiredService<KofflardShell>()),
            context => context.Services.AddKofflardApp(
                _ => settings,
                new KofflardPlatform(
                    _ => new InMemorySecretStore(),
                    _ => new RecordingLinks(settings.LaunchLog!),
                    _ => new RecordingLinks(settings.LaunchLog!),
                    _ => new NoSignIn())));
    }

    /// <inheritdoc />
    protected override void ConfigureAndroidOptions(MauiDriverOptions options)
    {
        base.ConfigureAndroidOptions(options);
        options.AdditionalCapabilities["autoGrantPermissions"] = true;
        options.AdditionalCapabilities["appWaitPackage"] = AndroidPackage;
        options.AdditionalCapabilities["enforceAppInstall"] = true;
    }

    /// <inheritdoc />
    /// <remarks>The app built in this repository, Debug preferred, found by walking up to <c>Kofflard.slnx</c>.</remarks>
    protected override string GetDefaultAppPath(MauiPlatform platform)
    {
        var (fileName, framework) = platform switch
        {
            MauiPlatform.Windows => ("Kofflard.App.exe", "net10.0-windows"),
            MauiPlatform.Android => ($"{AndroidPackage}-Signed.apk", "net10.0-android"),
            _ => throw new NotSupportedException($"The Kofflard UI tests run on Windows and Android, not {platform}."),
        };

        var root = new DirectoryInfo(Bruinet.Core.Composition.AssemblyFile.FolderOf(typeof(KofflardAppFixture).Assembly));
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Kofflard.slnx")))
        {
            root = root.Parent;
        }

        var bin = Path.Combine(root?.FullName ?? throw new DirectoryNotFoundException("No folder above the tests holds Kofflard.slnx."), "src", "Kofflard.App", "bin");
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var folder = Path.Combine(bin, configuration);
            var app = Directory.Exists(folder)
                ? Directory.EnumerateFiles(folder, fileName, SearchOption.AllDirectories)
                    .FirstOrDefault(path => path.Contains(framework, StringComparison.OrdinalIgnoreCase))
                : null;
            if (app is not null)
            {
                return app;
            }
        }

        throw new FileNotFoundException($"Kofflard is not built for {platform}: dotnet build src/Kofflard.App -f {framework}…", bin);
    }

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _arrangement is { } arranged)
        {
            arranged.Ai.Dispose();
            try
            {
                Directory.Delete(arranged.Folder, recursive: true);
            }
            catch (IOException)
            {
                // The app may still hold the database for a moment; the temp folder is cleaned up later.
            }
        }
    }

    // What was shown is excluded from later searches (system-level novelty), and the fake AI always
    // suggests the same musicians: each test starts with none shown. The app's file is on this machine
    // on Windows and headless; on Android the app's own data is cleared at install.
    private void ForgetHistory()
    {
        if (Platform is MauiPlatform.Windows or MauiPlatform.Headless)
        {
            new SqliteHistoryStore(new KofflardDatabase(Arranged.Settings.DatabasePath)).ClearAsync().GetAwaiter().GetResult();
        }
    }

    // Open with YouTube, no name checks: what a new install has, connected to the test's AI.
    private void DefaultSettings()
    {
        if (Platform is MauiPlatform.Windows or MauiPlatform.Headless)
        {
            var test = new Kofflard.Core.Ai.AiConnection(Arranged.Settings.AiProvider, Arranged.Settings.AiBaseUrl, Arranged.Settings.AiModel);
            new SqliteSettingsStore(new KofflardDatabase(Arranged.Settings.DatabasePath))
                .SaveAsync(Kofflard.Core.Services.AppSettings.Default with { Connection = test })
                .GetAwaiter().GetResult();
        }
    }

    // Headless the app works only when a test lets it: what the last test started finishes here.
    private void LetTheLastTestFinish()
    {
        if (Context.Driver is MauiHeadlessDriver head)
        {
            head.TestContext.Dispatcher.WaitForQuiet(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(3));
        }
    }

    private LaunchSettings Arrange()
    {
        var folder = Path.Combine(Path.GetTempPath(), "kofflard-ui", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var ai = FakeAi.Start();
        var live = LiveAi.Requested ? LiveAi.Load() : null;
        var settings = new LaunchSettings
        {
            DatabasePath = Path.Combine(folder, "kofflard.db"),
            FixedNow = Now,
            AiBaseUrl = live?.Connection.Endpoint ?? ai.BaseUrl,
            AiKey = live?.Key ?? "test-key",
            // Azure, as a new user's first choice: the fake AI answers on Azure's /openai/v1/ path too, so
            // Connect opens on Azure and a scene needs no dropdown (WinUI's left one open: 2026-10-31).
            AiProvider = live?.Connection.ProviderId ?? Kofflard.Core.Ai.AiProviders.Azure.Id,
            AiModel = live?.Connection.Model ?? LaunchSettings.TestModel,
            // Nothing listens there: name checks are off by default, and a test that turns them on gets "unverified".
            MusicBrainzBaseUrl = "http://127.0.0.1:1/",
            LaunchLog = Path.Combine(folder, "opened.txt"),
            Layout = Layout,
        };

        _arrangement = new Arrangement(folder, ai, settings, settings.LaunchLog, live);
        return settings;
    }

    // On Android the device cannot reach this machine's files, nor localhost: the database and the launch
    // log are left out, and the fake AI's address is the emulator's address for this machine.
    private static Dictionary<string, string> LaunchValues(LaunchSettings settings, MauiPlatform platform)
    {
        var values = new Dictionary<string, string>(settings.ToVariables());
        if (platform == MauiPlatform.Android && !settings.AiBaseUrl!.Contains("azure.com", StringComparison.OrdinalIgnoreCase))
        {
            values.Remove(LaunchSettings.DatabasePathVariable);
            values.Remove(LaunchSettings.LaunchLogVariable);
            values[LaunchSettings.AiBaseUrlVariable] = Bruinet.Appium.AndroidHost.EmulatorUrl(settings.AiBaseUrl!);
        }

        if (platform == MauiPlatform.Windows)
        {
            // The same frame in every take of a recorded video.
            values[LaunchSettings.WindowVariable] = "1280x800";
        }

        return values;
    }

    // Leaves whatever page or dialog an earlier test ended on, through the pages' own controls.
    private SearchPage ReturnToSearch()
    {
        var appRoot = new AppRoot(Context);
        for (var round = 0; round < 6; round++)
        {
            var dialog = new ContentDialog<AppRoot>(appRoot);
            var search = new SearchPage(Context);
            if (dialog.IsExists())
            {
                dialog.DialogButton("Cancel").Click();
            }
            else if (search.IsLoaded())
            {
                return search;
            }
            else
            {
                new ResultsPage(Context).BackButton.Click();
            }

            search.WaitLoaded(true, TestConstants.PageTimeoutMs / 4);
        }

        return SearchPage.Arrived(Context);
    }

    private sealed record Arrangement(string Folder, FakeAi Ai, LaunchSettings Settings, string LaunchLog, LiveAi? Live);

    private sealed class NoSignIn : IOAuthBroker
    {
        public Task<IReadOnlyDictionary<string, string>?> AuthenticateAsync(Func<Uri, Uri> authorizeUrl, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, string>?>(null);
    }
}

/// <summary>Tests against one <see cref="KofflardAppFixture"/>.</summary>
[CollectionDefinition(Name)]
public sealed class KofflardCollection : ICollectionFixture<KofflardAppFixture>
{
    /// <summary>The collection's name.</summary>
    public const string Name = "Kofflard";
}
