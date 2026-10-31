using System.Globalization;

namespace Kofflard.Infrastructure;

/// <summary>
/// Everything about the app that a test run may change, in one place (design §9).
/// </summary>
/// <remarks>
/// Read once at startup, as in Bruinet's Todo sample: environment variables on Windows, launch-intent
/// extras on Android. A normal launch sets none of them and gets the defaults.
/// </remarks>
public sealed record LaunchSettings
{
    /// <summary>The prefix every launch value has.</summary>
    public const string Prefix = "KOFFLARD_";

    /// <summary>The SQLite file.</summary>
    public const string DatabasePathVariable = "KOFFLARD_DB_PATH";

    /// <summary>A fixed "now", round-trip format.</summary>
    public const string FixedNowVariable = "KOFFLARD_NOW";

    /// <summary>An OpenAI-compatible endpoint to connect to at start, skipping Connect (a test's fake AI).</summary>
    public const string AiBaseUrlVariable = "KOFFLARD_AI_BASEURL";

    /// <summary>The key for <see cref="AiBaseUrlVariable"/>.</summary>
    public const string AiKeyVariable = "KOFFLARD_AI_KEY";

    /// <summary>The provider of <see cref="AiBaseUrlVariable"/>: "local" (the fake AI) unless a test connects to the live one ("azure").</summary>
    public const string AiProviderVariable = "KOFFLARD_AI_PROVIDER";

    /// <summary>The model or deployment of <see cref="AiBaseUrlVariable"/>.</summary>
    public const string AiModelVariable = "KOFFLARD_AI_MODEL";

    /// <summary>MusicBrainz's base URL; a stub under test.</summary>
    public const string MusicBrainzBaseUrlVariable = "KOFFLARD_MUSICBRAINZ_BASEURL";

    /// <summary>OpenRouter's base URL, for sign-in; a stub under test.</summary>
    public const string OpenRouterBaseUrlVariable = "KOFFLARD_OPENROUTER_BASEURL";

    /// <summary>A file the app writes every link it would open to, instead of opening it.</summary>
    public const string LaunchLogVariable = "KOFFLARD_LAUNCH_LOG";

    /// <summary>The window's size on Windows, as <c>1280x800</c>: the same frame in every recorded take. Read by the app.</summary>
    public const string WindowVariable = "KOFFLARD_WINDOW";

    /// <summary>The model a pre-connected fake AI is asked for.</summary>
    public const string TestModel = "kofflard-test";

    /// <summary>The SQLite file.</summary>
    public required string DatabasePath { get; init; }

    /// <summary>A fixed "now"; null for the real clock.</summary>
    public DateTimeOffset? FixedNow { get; init; }

    /// <summary>An OpenAI-compatible endpoint to connect to at start; null for a normal launch.</summary>
    public string? AiBaseUrl { get; init; }

    /// <summary>The key for <see cref="AiBaseUrl"/>.</summary>
    public string? AiKey { get; init; }

    /// <summary>The provider of <see cref="AiBaseUrl"/>.</summary>
    public string AiProvider { get; init; } = "local";

    /// <summary>The model or deployment of <see cref="AiBaseUrl"/>.</summary>
    public string AiModel { get; init; } = TestModel;

    /// <summary>MusicBrainz's base URL.</summary>
    public string MusicBrainzBaseUrl { get; init; } = "https://musicbrainz.org/";

    /// <summary>OpenRouter's base URL.</summary>
    public string OpenRouterBaseUrl { get; init; } = "https://openrouter.ai/";

    /// <summary>The launch log; null to open links for real.</summary>
    public string? LaunchLog { get; init; }

    /// <summary>How long an AI request may take.</summary>
    public TimeSpan AiTimeout { get; init; } = TimeSpan.FromSeconds(60);

    /// <summary>Whether the app starts connected to a test's AI: the fake one, or the live one.</summary>
    public bool IsPreConnected => !string.IsNullOrWhiteSpace(AiBaseUrl);

    /// <summary>Builds settings from named values, with the defaults for anything not set.</summary>
    /// <param name="read">Reads one value by name; null when not set.</param>
    /// <param name="defaultDatabasePath">The app's own database file.</param>
    public static LaunchSettings From(Func<string, string?> read, string defaultDatabasePath)
    {
        var defaults = new LaunchSettings { DatabasePath = defaultDatabasePath };
        return new LaunchSettings
        {
            DatabasePath = Value(read, DatabasePathVariable) ?? defaultDatabasePath,
            FixedNow = Value(read, FixedNowVariable) is { } now
                ? DateTimeOffset.Parse(now, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind)
                : null,
            AiBaseUrl = Value(read, AiBaseUrlVariable),
            AiKey = Value(read, AiKeyVariable),
            AiProvider = Value(read, AiProviderVariable) ?? defaults.AiProvider,
            AiModel = Value(read, AiModelVariable) ?? defaults.AiModel,
            MusicBrainzBaseUrl = Value(read, MusicBrainzBaseUrlVariable) ?? defaults.MusicBrainzBaseUrl,
            OpenRouterBaseUrl = Value(read, OpenRouterBaseUrlVariable) ?? defaults.OpenRouterBaseUrl,
            LaunchLog = Value(read, LaunchLogVariable),
        };
    }

    /// <summary>The settings as launch values: what a test passes to the app it launches.</summary>
    public IReadOnlyDictionary<string, string> ToVariables()
    {
        var values = new Dictionary<string, string> { [DatabasePathVariable] = DatabasePath };
        Add(values, FixedNowVariable, FixedNow?.ToString("O", CultureInfo.InvariantCulture));
        Add(values, AiBaseUrlVariable, AiBaseUrl);
        Add(values, AiKeyVariable, AiKey);
        Add(values, AiProviderVariable, AiProvider);
        Add(values, AiModelVariable, AiModel);
        Add(values, MusicBrainzBaseUrlVariable, MusicBrainzBaseUrl);
        Add(values, OpenRouterBaseUrlVariable, OpenRouterBaseUrl);
        Add(values, LaunchLogVariable, LaunchLog);
        return values;
    }

    private static string? Value(Func<string, string?> read, string name)
        => read(name) is { } value && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static void Add(Dictionary<string, string> values, string name, string? value)
    {
        if (value is not null)
        {
            values[name] = value;
        }
    }
}
